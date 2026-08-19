using CombatSimulation.Models;
using CombatSimulation.Models.Unity;
using CombatSimulation.Services.DamageTrees;
using CombatSimulation.Services.Targets;
using CombatSimulation.Services.Unity;
using CombatSimulation.ViewModels;
using CombatSimulation.Services.MySql;

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
            ("视图模型构造不访问数据库且释放事件", ViewModelConstructionAndDisposalAreSideEffectSafe)
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

        viewModel.Dispose();
        AssertEqual(0, unityService.SubscriberCount);
        AssertEqual(0, unityService.DisposeCount);
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"期望 {expected}，实际 {actual}。");
        }
    }

    private sealed class InMemoryTargetInfoRepository : ITargetInfoRepository
    {
        public int LoadCount { get; private set; }

        public IReadOnlyList<TargetInfoItem> LoadTargets()
        {
            LoadCount++;
            return Array.Empty<TargetInfoItem>();
        }

        public CombatDatabaseSnapshot CreateDatabaseSnapshot() => new();
        public void AddTarget(TargetInfoItem target) => throw new NotSupportedException();
        public void UpdateTarget(TargetInfoItem target) => throw new NotSupportedException();
        public void DeleteTarget(string targetCode) => throw new NotSupportedException();
        public void AddSystem(string targetCode, TargetSystemInfoItem system) => throw new NotSupportedException();
        public void UpdateSystem(string targetCode, string originalSystemCode, TargetSystemInfoItem system) => throw new NotSupportedException();
        public string? DeleteSystem(string targetCode, string systemCode) => throw new NotSupportedException();
        public void AddPart(string targetCode, TargetPartInfoItem part) => throw new NotSupportedException();
        public void UpdatePart(string targetCode, string originalPartCode, TargetPartInfoItem part) => throw new NotSupportedException();
        public string? DeletePart(string targetCode, string partCode) => throw new NotSupportedException();
    }

    private sealed class InMemoryDamageTreeRepository : IDamageTreeRepository
    {
        public IReadOnlyList<DamageTreeInfoItem> LoadDamageTrees(
            string targetCode,
            IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode = null) =>
            Array.Empty<DamageTreeInfoItem>();

        public void AddDamageTree(DamageTreeInfoItem tree) => throw new NotSupportedException();
        public void UpdateDamageTree(DamageTreeInfoItem tree) => throw new NotSupportedException();
        public void DeleteDamageTree(string damageTreeCode) => throw new NotSupportedException();
        public void AddNode(DamageTreeNodeItem node) => throw new NotSupportedException();
        public void UpdateNode(string originalNodeCode, DamageTreeNodeItem node) => throw new NotSupportedException();
        public void DeleteNode(string damageTreeCode, string nodeCode) => throw new NotSupportedException();
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
        public Task UpdateTargetPartAsync(Target_Part_Info_By_MySQL part, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadDatabaseSnapshotAsync(CombatDatabaseSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendEventAsync(string command, object? data = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendRequestAsync(string command, object? data = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
