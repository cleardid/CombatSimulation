using CombatSimulation.Models;
using CombatSimulation.Models.Unity;
using CombatSimulation.Services.DamageTrees;
using CombatSimulation.Services.Targets;
using CombatSimulation.Services.Unity;
using CombatSimulation.ViewModels;
using Newtonsoft.Json;

namespace CombatSimulation.Tests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        (string Name, Action Test)[] tests =
        {
            ("颜色格式统一为 Unity 数据库格式", ColorFormatIsNormalized),
            ("毁伤等级历史文本可归一化", DamageLevelIsNormalized),
            ("毁伤树默认选择第一个空缺等级", FirstAvailableDamageLevelIsSelected),
            ("视图模型构造不访问数据库且释放事件", ViewModelConstructionAndDisposalAreSideEffectSafe),
            ("Unity快照使用独立协议DTO", UnitySnapshotUsesIndependentProtocolDtos),
            ("删除异常由视图模型接收并保留界面数据", DeleteExceptionIsHandledByViewModel),
            ("删除命令必须通过用户确认", DeleteCommandRequiresConfirmation)
        };

        int failedCount = 0;
        foreach ((string name, Action test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"[通过] {name}");
            }
            catch (Exception ex)
            {
                failedCount++;
                Console.Error.WriteLine($"[失败] {name}: {ex.Message}");
            }
        }

        Console.WriteLine($"共执行 {tests.Length} 项，失败 {failedCount} 项。");
        return failedCount == 0 ? 0 : 1;
    }

    private static void ColorFormatIsNormalized()
    {
        AssertEqual("#AABBCC", TargetPartColorFormat.ToWpfDisplayColor("aabbccff"));
        AssertEqual("#AABBCC0F", TargetPartColorFormat.ToUnityDatabaseColor("#aabbccff"));
        AssertEqual("#8080800F", TargetPartColorFormat.ToUnityDatabaseColor("not-a-color"));
    }

    private static void DamageLevelIsNormalized()
    {
        AssertEqual(DamageTreeDefaults.LightDamageLevel, DamageTreeDefaults.NormalizeDamageLevel(" 轻微 "));
        AssertEqual(DamageTreeDefaults.MediumDamageLevel, DamageTreeDefaults.NormalizeDamageLevel("中等毁伤"));
        AssertEqual(DamageTreeDefaults.HeavyDamageLevel, DamageTreeDefaults.NormalizeDamageLevel("重度"));
    }

    private static void FirstAvailableDamageLevelIsSelected()
    {
        DamageTreeInfoItem existing = new()
        {
            DamageLevelInfo = DamageTreeDefaults.LightDamageLevel
        };

        AssertEqual(
            DamageTreeDefaults.MediumDamageLevel,
            DamageTreeDefaults.FindFirstAvailableDamageLevel(new[] { existing }));
    }

    private static void ViewModelConstructionAndDisposalAreSideEffectSafe()
    {
        var targetRepository = new InMemoryTargetInfoRepository();
        var damageTreeRepository = new InMemoryDamageTreeRepository();
        var unityService = new RecordingUnityCommandService();

        var viewModel = new TargetInfoViewModel(targetRepository, damageTreeRepository, unityService);
        AssertEqual(0, targetRepository.LoadCount);
        AssertEqual(1, unityService.SubscriberCount);

        viewModel.InitializeAsync().GetAwaiter().GetResult();
        AssertEqual(1, targetRepository.LoadCount);

        viewModel.Dispose();
        AssertEqual(0, unityService.SubscriberCount);
        AssertEqual(0, unityService.DisposeCount);
    }

    private static void UnitySnapshotUsesIndependentProtocolDtos()
    {
        CombatDatabaseSnapshot snapshot = new();
        snapshot.TargetParts.Add(new UnityTargetPartRecord
        {
            PartCode = "part-1",
            PartColor = "#AABBCC0F",
            PartEquParam24 = 24f
        });

        string json = JsonConvert.SerializeObject(snapshot);
        if (!json.Contains("\"PartCode\":\"part-1\"", StringComparison.Ordinal)
            || !json.Contains("\"PartColor\":\"#AABBCC0F\"", StringComparison.Ordinal)
            || !json.Contains("\"PartEquParam24\":24.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unity 快照 JSON 契约不兼容：{json}");
        }

        AssertEqual("CombatSimulation.Models.Unity", snapshot.TargetParts[0].GetType().Namespace);
    }

    private static void DeleteExceptionIsHandledByViewModel()
    {
        var targetRepository = new InMemoryTargetInfoRepository
        {
            DeleteTargetException = new InvalidOperationException("目标仍被毁伤树引用")
        };
        var viewModel = new TargetInfoViewModel(
            targetRepository,
            new InMemoryDamageTreeRepository(),
            new RecordingUnityCommandService());
        TargetInfoItem target = new() { Code = "target-1", Name = "测试目标", Category = "测试" };
        viewModel.Targets.Add(target);

        bool succeeded = viewModel.DeleteTargetAsync(target).GetAwaiter().GetResult();

        AssertEqual(false, succeeded);
        AssertEqual(true, viewModel.Targets.Contains(target));
        AssertContains("目标仍被毁伤树引用", viewModel.StatusText);
        viewModel.Dispose();
    }

    private static void DeleteCommandRequiresConfirmation()
    {
        var targetRepository = new InMemoryTargetInfoRepository();
        var interactionService = new RecordingTargetInteractionService();
        var viewModel = new TargetInfoViewModel(
            targetRepository,
            new InMemoryDamageTreeRepository(),
            new RecordingUnityCommandService(),
            interactionService);
        TargetInfoItem target = new() { Code = "target-2", Name = "待确认目标", Category = "测试" };
        viewModel.Targets.Add(target);
        viewModel.SelectedTarget = target;

        interactionService.ConfirmDeletionResult = false;
        viewModel.RequestDeleteTargetCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        AssertEqual(0, targetRepository.DeleteTargetCount);
        AssertEqual(true, viewModel.Targets.Contains(target));

        interactionService.ConfirmDeletionResult = true;
        viewModel.RequestDeleteTargetCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        AssertEqual(1, targetRepository.DeleteTargetCount);
        AssertEqual(false, viewModel.Targets.Contains(target));
        viewModel.Dispose();
    }
    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"期望 {expected}，实际 {actual}。");
        }
    }
    private static void AssertContains(string expectedSubstring, string actual)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"期望文本包含“{expectedSubstring}”，实际为“{actual}”。");
        }
    }

    private sealed class InMemoryTargetInfoRepository : ITargetInfoRepository
    {
        public int LoadCount { get; private set; }

        public Task<IReadOnlyList<TargetInfoItem>> LoadTargetsAsync(CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult<IReadOnlyList<TargetInfoItem>>(Array.Empty<TargetInfoItem>());
        }

        public Task<CombatDatabaseSnapshot> CreateDatabaseSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CombatDatabaseSnapshot());

        public Task AddTargetAsync(TargetInfoItem target, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateTargetAsync(TargetInfoItem target, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public int DeleteTargetCount { get; private set; }
        public Exception? DeleteTargetException { get; init; }

        public Task DeleteTargetAsync(string targetCode, CancellationToken cancellationToken = default)
        {
            DeleteTargetCount++;
            return DeleteTargetException == null
                ? Task.CompletedTask
                : Task.FromException(DeleteTargetException);
        }
        public Task AddSystemAsync(string targetCode, TargetSystemInfoItem system, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateSystemAsync(string targetCode, string originalSystemCode, TargetSystemInfoItem system, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> DeleteSystemAsync(string targetCode, string systemCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddPartAsync(string targetCode, TargetPartInfoItem part, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdatePartAsync(string targetCode, string originalPartCode, TargetPartInfoItem part, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> DeletePartAsync(string targetCode, string partCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class InMemoryDamageTreeRepository : IDamageTreeRepository
    {
        public Task<IReadOnlyList<DamageTreeInfoItem>> LoadDamageTreesAsync(
            string targetCode,
            IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DamageTreeInfoItem>>(Array.Empty<DamageTreeInfoItem>());

        public Task AddDamageTreeAsync(DamageTreeInfoItem tree, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateDamageTreeAsync(DamageTreeInfoItem tree, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteDamageTreeAsync(string damageTreeCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddNodeAsync(DamageTreeNodeItem node, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateNodeAsync(string originalNodeCode, DamageTreeNodeItem node, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteNodeAsync(string damageTreeCode, string nodeCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class RecordingTargetInteractionService : ITargetInteractionService
    {
        public bool ConfirmDeletionResult { get; set; }

        public void ShowMessage(string message)
        {
        }

        public bool ConfirmDeletion(string message) => ConfirmDeletionResult;
        public TargetInfoItem? EditTarget(TargetInfoItem draft, bool isEditMode) => null;
        public TargetSystemInfoItem? EditSystem(TargetSystemInfoItem draft, bool isEditMode, IEnumerable<TargetInfoItem> allTargets) => null;
        public TargetPartInfoItem? EditPart(TargetPartInfoItem draft, bool isEditMode) => null;
        public DamageTreeInfoItem? EditDamageTree(DamageTreeInfoItem draft, string targetName, bool isEditMode) => null;
        public DamageTreeNodeItem? EditMiddleDamageNode(DamageTreeNodeItem draft, bool isEditMode) => null;
        public DamageTreeNodeItem? EditLeafDamageNode(DamageTreeNodeItem draft, IEnumerable<TargetStructureTreeNode> structureRoots, bool isEditMode) => null;
    }
    private sealed class RecordingUnityCommandService : IUnityCommandService, IDisposable
    {
        private EventHandler<UnityMessage>? _eventReceived;

        public int SubscriberCount { get; private set; }
        public int DisposeCount { get; private set; }

        public event EventHandler<UnityMessage>? EventReceived
        {
            add
            {
                _eventReceived += value;
                SubscriberCount++;
            }
            remove
            {
                _eventReceived -= value;
                SubscriberCount--;
            }
        }

        public Task ShowTargetAsync(string targetCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task HighlightPartAsync(string partCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ShowPartsAsync(IReadOnlyList<string> partCodes, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadDatabaseSnapshotAsync(CombatDatabaseSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendEventAsync(string command, object? data = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendRequestAsync(string command, object? data = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
