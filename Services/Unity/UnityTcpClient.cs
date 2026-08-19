using CombatSimulation.Models.Unity;
using Newtonsoft.Json;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CombatSimulation.Services.Unity;

/// <summary>
/// Unity TCP 客户端。
/// </summary>
/// <remarks>
/// 该类只负责 TCP 连接、长度帧编码/解码、JSON 序列化/反序列化和消息收发。
/// 它不负责 Unity 进程启动、窗口嵌入、request/response 的 msgId 匹配和自动重连策略。
/// </remarks>
internal sealed class UnityTcpClient
{
    /// <summary>
    /// Unity TCP Server 地址。当前 Unity 与 WPF 在同一台机器上运行。
    /// </summary>
    private const string Host = "127.0.0.1";

    /// <summary>
    /// Unity TCP Server 监听端口。
    /// </summary>
    private const int Port = 8899;

    /// <summary>
    /// 消息帧头长度。协议使用 4 字节小端整数表示 payload 字节数。
    /// </summary>
    private const int HeaderLength = 4;

    /// <summary>
    /// 单个 JSON payload 的最大字节数，用于避免异常长度造成内存分配风险。
    /// </summary>
    private const int MaxPayloadLength = 10 * 1024 * 1024;

    /// <summary>
    /// 单次 TCP 连接尝试的超时时间。
    /// </summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 关闭连接时等待接收循环退出的最长时间。
    /// </summary>
    private static readonly TimeSpan ReceiveLoopStopTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// UTF-8 编码器。禁用 BOM，启用非法字节检测。
    /// </summary>
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>
    /// Unity 消息 JSON 序列化配置。
    /// </summary>
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Include,
        Formatting = Formatting.None
    };

    /// <summary>
    /// 保护发送流程，避免多个消息同时写入同一个 NetworkStream 导致帧交叉。
    /// </summary>
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveTask;
    private UnityConnectionState _state = UnityConnectionState.Disconnected;
    private bool _closing;

    /// <summary>
    /// 获取当前是否已经建立可用 TCP 连接。
    /// </summary>
    public bool IsConnected =>
        _state == UnityConnectionState.Connected &&
        _tcpClient is { Connected: true } &&
        _stream != null;

    /// <summary>
    /// 接收到完整 Unity 消息时触发。
    /// </summary>
    public event EventHandler<UnityMessage>? MessageReceived;

    /// <summary>
    /// TCP 连接异常断开时触发。
    /// </summary>
    public event EventHandler<Exception>? ConnectionLost;

    /// <summary>
    /// 连接 Unity TCP Server，并启动后台接收循环。
    /// </summary>
    /// <param name="cancellationToken">连接过程取消令牌。</param>
    /// <exception cref="TimeoutException">连接超过 <see cref="ConnectTimeout"/> 时抛出。</exception>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await CloseAsync(setDisconnectedState: false).ConfigureAwait(false);
        SetState(UnityConnectionState.Connecting);

        var client = new TcpClient { NoDelay = true };
        using var connectTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeoutCts.CancelAfter(ConnectTimeout);

        try
        {
            await client.ConnectAsync(Host, Port, connectTimeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            throw new TimeoutException($"连接 Unity TCP Server 超时：{Host}:{Port}");
        }
        catch
        {
            client.Dispose();
            throw;
        }

        _tcpClient = client;
        _stream = client.GetStream();
        _connectionCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_connectionCts.Token));

        SetState(UnityConnectionState.Connected);
        Log($"Unity TCP 已连接：{Host}:{Port}");
    }

    /// <summary>
    /// 发送一条 Unity 消息。
    /// </summary>
    /// <param name="message">待发送的协议消息。</param>
    /// <param name="cancellationToken">发送过程取消令牌。</param>
    /// <remarks>
    /// 发送前会把 <see cref="UnityMessage"/> 序列化为 JSON，再加上 4 字节长度帧头。
    /// </remarks>
    public async Task SendAsync(UnityMessage message, CancellationToken cancellationToken)
    {
        NetworkStream stream = _stream ?? throw new IOException("Unity TCP 尚未连接。");
        byte[] frame = Encode(message);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(frame.AsMemory(0, frame.Length), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Unity TCP 发送失败：{ex.Message}");
            SetState(UnityConnectionState.Disconnected);
            ConnectionLost?.Invoke(this, new IOException("Unity TCP 发送失败。", ex));
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 关闭当前 TCP 连接，并停止后台接收循环。
    /// </summary>
    /// <param name="setDisconnectedState">是否显式把状态设置为 Disconnected。</param>
    public async Task CloseAsync(bool setDisconnectedState)
    {
        _closing = true;

        CancellationTokenSource? cts = _connectionCts;
        _connectionCts = null;
        cts?.Cancel();
        cts?.Dispose();

        NetworkStream? stream = _stream;
        _stream = null;
        stream?.Dispose();

        TcpClient? client = _tcpClient;
        _tcpClient = null;
        client?.Dispose();

        Task? receiveTask = _receiveTask;
        _receiveTask = null;

        if (receiveTask != null)
        {
            try
            {
                await receiveTask.WaitAsync(ReceiveLoopStopTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Log("Unity TCP 接收循环未在退出超时内结束，跳过等待。");
            }
            catch
            {
                // 关闭连接阶段忽略接收循环异常，避免退出流程被接收线程异常阻塞。
            }
        }

        if (setDisconnectedState)
        {
            SetState(UnityConnectionState.Disconnected);
        }

        _closing = false;
    }

    /// <summary>
    /// 把状态标记为正在重连。
    /// </summary>
    public void MarkReconnecting()
    {
        SetState(UnityConnectionState.Reconnecting);
    }

    /// <summary>
    /// 把状态标记为故障。
    /// </summary>
    public void MarkFaulted()
    {
        SetState(UnityConnectionState.Faulted);
    }

    /// <summary>
    /// 后台接收循环。持续读取完整长度帧，并把反序列化后的消息抛给上层。
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;

        try
        {
            NetworkStream stream = _stream ?? throw new IOException("Unity TCP 接收流为空。");

            while (!cancellationToken.IsCancellationRequested)
            {
                UnityMessage? message = await ReadMessageAsync(stream, cancellationToken).ConfigureAwait(false);

                if (message == null)
                {
                    failure = new EndOfStreamException("Unity TCP 连接已由对端关闭。");
                    break;
                }

                MessageReceived?.Invoke(this, message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        if (failure != null && !_closing && !cancellationToken.IsCancellationRequested)
        {
            Log($"Unity TCP 接收中断：{failure.Message}");
            SetState(UnityConnectionState.Disconnected);
            ConnectionLost?.Invoke(this, new IOException("Unity TCP 连接已中断。", failure));
        }
    }

    /// <summary>
    /// 将 Unity 消息编码为长度帧。
    /// </summary>
    private static byte[] Encode(UnityMessage message)
    {
        ValidateMessage(message);

        string json = JsonConvert.SerializeObject(message, JsonSettings);
        byte[] payload = Utf8.GetBytes(json);
        ValidatePayloadLength(payload.Length);

        byte[] frame = new byte[HeaderLength + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, HeaderLength), payload.Length);
        payload.CopyTo(frame.AsSpan(HeaderLength));
        return frame;
    }

    /// <summary>
    /// 从流中读取一条完整 Unity 消息。
    /// </summary>
    /// <returns>
    /// 读取到的消息；如果在读取帧头前流已经结束，则返回 null。
    /// </returns>
    private static async Task<UnityMessage?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderLength];
        bool hasHeader = await ReadExactAsync(stream, header.AsMemory(0, HeaderLength), true, cancellationToken).ConfigureAwait(false);

        if (!hasHeader)
        {
            return null;
        }

        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        ValidatePayloadLength(payloadLength);

        byte[] payload = new byte[payloadLength];
        await ReadExactAsync(stream, payload.AsMemory(0, payloadLength), false, cancellationToken).ConfigureAwait(false);

        string json = Utf8.GetString(payload);
        UnityMessage? message = JsonConvert.DeserializeObject<UnityMessage>(json, JsonSettings);

        if (message == null)
        {
            throw new InvalidDataException("Unity 消息 JSON 反序列化结果为空。");
        }

        ValidateMessage(message);
        return message;
    }

    /// <summary>
    /// 从流中精确读取指定长度的数据。
    /// </summary>
    /// <param name="returnFalseOnEndBeforeAnyByte">
    /// 如果为 true，并且在读取任何字节前已经到达流尾，则返回 false；否则流尾视为异常。
    /// </param>
    private static async Task<bool> ReadExactAsync(
        Stream stream,
        Memory<byte> buffer,
        bool returnFalseOnEndBeforeAnyByte,
        CancellationToken cancellationToken)
    {
        int offset = 0;

        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.Slice(offset), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                if (offset == 0 && returnFalseOnEndBeforeAnyByte)
                {
                    return false;
                }

                throw new EndOfStreamException("读取 Unity TCP 消息帧时连接中断。");
            }

            offset += read;
        }

        return true;
    }

    /// <summary>
    /// 校验 payload 长度是否合法。
    /// </summary>
    private static void ValidatePayloadLength(int payloadLength)
    {
        if (payloadLength <= 0 || payloadLength > MaxPayloadLength)
        {
            throw new InvalidDataException($"Unity 消息长度非法：{payloadLength} 字节。");
        }
    }

    /// <summary>
    /// 校验 Unity 消息是否满足当前协议的最小字段要求。
    /// </summary>
    private static void ValidateMessage(UnityMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.Type))
        {
            throw new InvalidDataException("Unity 消息缺少 type 字段。");
        }

        if (!UnityMessageTypes.IsKnown(message.Type))
        {
            throw new InvalidDataException($"Unity 消息 type 不受支持：{message.Type}");
        }

        if (!string.Equals(message.Type, UnityMessageTypes.Event, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(message.MsgId))
        {
            throw new InvalidDataException($"Unity {message.Type} 消息缺少 msgId 字段。");
        }

        if (string.IsNullOrWhiteSpace(message.Command))
        {
            throw new InvalidDataException($"Unity {message.Type} 消息缺少 command 字段。");
        }
    }

    /// <summary>
    /// 更新连接状态。
    /// </summary>
    private void SetState(UnityConnectionState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
    }

    /// <summary>
    /// 写入 TCP 客户端调试日志。
    /// </summary>
    private void Log(string message)
    {
        Debug.WriteLine($"[UnityTcpClient] {message}");
    }
}
