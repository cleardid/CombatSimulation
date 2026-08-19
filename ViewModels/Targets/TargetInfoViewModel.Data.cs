using CombatSimulation.Models;
using CombatSimulation.Services.MySql;
using System.Diagnostics;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// 目标类型筛选条件变化时刷新目标列表。
    /// </summary>
    partial void OnSelectedTargetCategoryChanged(string value)
    {
        FilteredTargets.Refresh();

        // 如果当前选中的目标不属于新的类型筛选范围，则自动切换到筛选结果中的第一个目标。
        if (!IsSelectedTargetInCurrentFilter())
        {
            SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
        }
    }

    /// <summary>
    /// 在后台线程从 MySQL 仓储读取目标、系统和部件数据，再回到 UI 线程更新集合。
    /// </summary>
    private async Task LoadTargetsFromRepositoryAsync()
    {
        StatusText = "正在读取目标数据库...";
        try
        {
            IReadOnlyList<TargetInfoItem> storedTargets =
                await RunRepositoryOperationAsync(_targetInfoRepository.LoadTargets);

            Targets.Clear();
            foreach (TargetInfoItem target in storedTargets)
            {
                // 数据库读取出来的是模型树，界面还需要对应的 TargetStructureTreeNode 树节点。
                EnsureTargetStructureTree(target);
                Targets.Add(target);
            }

            RebuildTargetCategories();
            SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
            if (SelectedTarget == null)
            {
                RefreshSelectedDetailRows(null);
            }

            _targetLoadStatusText = Targets.Count == 0
                ? "数据库中暂无目标信息"
                : $"已从数据库读取 {Targets.Count} 个目标";
            StatusText = _targetLoadStatusText;
            MySqlLog.Log(_targetLoadStatusText);
        }
        catch (Exception ex)
        {
            _targetLoadStatusText = $"读取目标数据库失败：{ex.Message}";
            StatusText = _targetLoadStatusText;
            MySqlLog.LogWarning($"读取目标数据库失败：{ex}");
            Debug.WriteLine($"[TargetInfoViewModel] 读取目标数据库失败：{ex}");
        }
        finally
        {
            IsDatabaseReady = true;
        }
    }

    /// <summary>
    /// 在后台线程串行执行数据库写操作，避免阻塞 WPF UI 线程并减少仓储实例并发访问。
    /// </summary>
    private async Task RunRepositoryOperationAsync(Action operation)
    {
        await _repositoryOperationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(operation).ConfigureAwait(false);
        }
        finally
        {
            _repositoryOperationLock.Release();
        }
    }

    /// <summary>
    /// 在后台线程串行执行需要返回业务结果的数据库操作。
    /// </summary>
    private async Task<TResult> RunRepositoryOperationAsync<TResult>(Func<TResult> operation)
    {
        await _repositoryOperationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(operation).ConfigureAwait(false);
        }
        finally
        {
            _repositoryOperationLock.Release();
        }
    }

    /// <summary>
    /// 判断当前选中目标是否仍在当前筛选结果中。
    /// </summary>
    private bool IsSelectedTargetInCurrentFilter()
    {
        if (SelectedTarget == null)
        {
            return false;
        }

        return FilterTargetByCategory(SelectedTarget);
    }

    /// <summary>
    /// 目标类型筛选谓词，供 ICollectionView 调用。
    /// </summary>
    private bool FilterTargetByCategory(object item)
    {
        if (item is not TargetInfoItem target)
        {
            return false;
        }

        return SelectedTargetCategory == AllTargetCategory ||
               SelectedTargetCategory == target.Category;
    }

    /// <summary>
    /// 根据当前目标集合重建类型筛选下拉列表。
    /// </summary>
    private void RebuildTargetCategories(bool refreshFilteredTargets = true)
    {
        string selectedCategory = string.IsNullOrWhiteSpace(SelectedTargetCategory)
            ? AllTargetCategory
            : SelectedTargetCategory;

        TargetCategories.Clear();
        TargetCategories.Add(AllTargetCategory);

        foreach (string category in Targets
                     .Select(target => target.Category.Trim())
                     .Where(category => !string.IsNullOrWhiteSpace(category))
                     .Distinct())
        {
            TargetCategories.Add(category);
        }

        // 如果原类型已经不存在，自动回退到“全部类型”。
        SelectedTargetCategory = TargetCategories.Contains(selectedCategory)
            ? selectedCategory
            : AllTargetCategory;

        if (refreshFilteredTargets)
        {
            FilteredTargets.Refresh();
        }
    }

    /// <summary>
    /// 校验目标名称、类型和唯一标识。
    /// </summary>
    private bool ValidateTarget(TargetInfoItem target, TargetInfoItem? except)
    {
        if (string.IsNullOrWhiteSpace(target.Name))
        {
            StatusText = "请输入目标名称";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Category))
        {
            StatusText = "请输入目标种类";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Code))
        {
            target.Code = CreateUniqueTargetCode();
        }

        bool codeUsed = Targets.Any(existingTarget =>
            !ReferenceEquals(existingTarget, except) &&
            string.Equals(existingTarget.Code, target.Code, StringComparison.Ordinal));

        if (codeUsed)
        {
            StatusText = $"目标唯一标识已存在：{target.Code}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 去除目标字符串字段前后的空白字符。
    /// </summary>
    private static void NormalizeTarget(TargetInfoItem target)
    {
        target.Name = target.Name.Trim();
        target.Category = target.Category.Trim();
        target.Description = target.Description.Trim();
        target.Code = target.Code.Trim();
    }

    /// <summary>
    /// 确保目标模型存在结构树根节点。
    /// </summary>
    /// <remarks>
    /// 数据库层只关心目标、系统和部件的父子关系；WPF TreeView 需要额外的 TargetStructureTreeNode 包装。
    /// </remarks>
    private static void EnsureTargetStructureTree(TargetInfoItem target)
    {
        if (target.StructureTreeNodes.Count > 0)
        {
            return;
        }

        TargetStructureTreeNode root = TargetStructureTreeNode.ForTarget(target);
        foreach (TargetSystemInfoItem system in target.Systems)
        {
            root.Children.Add(CreateSystemNode(system));
        }

        target.StructureTreeNodes.Add(root);
    }
}
