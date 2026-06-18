using CombatSimulation.Models;
using System.Diagnostics;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// 创建新增目标弹窗使用的目标草稿。
    /// </summary>
    public TargetInfoItem CreateTargetDraft()
    {
        return new TargetInfoItem
        {
            Code = CreateUniqueTargetCode(),
            Name = "新建目标",
            Category = "未分类",
            Description = string.Empty
        };
    }

    /// <summary>
    /// 新增目标，并同步数据库、目标列表和类型筛选列表。
    /// </summary>
    public async Task<bool> AddTargetAsync(TargetInfoItem newTarget)
    {
        NormalizeTarget(newTarget);

        if (!ValidateTarget(newTarget, except: null))
        {
            return false;
        }

        try
        {
            // 目标即使没有系统和部件，也需要创建结构树根节点供界面显示。
            EnsureTargetStructureTree(newTarget);

            // 先写数据库，保存成功后再加入界面集合。
            await RunRepositoryOperationAsync(() => _targetInfoRepository.AddTarget(newTarget));

            Targets.Add(newTarget);
            RebuildTargetCategories();

            // 新增后自动切换到该目标，并保持目标类型筛选条件可见。
            SelectedTargetCategory = newTarget.Category;
            FilteredTargets.Refresh();
            SelectedTarget = newTarget;

            // 选中项已切换到新增目标后再捕获 Unity 显示状态，避免快照完成后恢复到旧目标。
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);
            StatusText = $"已添加目标：{newTarget.Name}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"添加目标失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 添加目标失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 修改目标基础信息。
    /// </summary>
    public async Task<bool> UpdateTargetAsync(TargetInfoItem target, TargetInfoItem editedTarget)
    {
        if (!Targets.Contains(target))
        {
            StatusText = "当前目标不存在，无法修改";
            return false;
        }

        // 目标唯一标识不允许通过基础信息弹窗修改，避免影响系统和部件外键。
        editedTarget.Code = target.Code;
        NormalizeTarget(editedTarget);

        if (!ValidateTarget(editedTarget, except: target))
        {
            return false;
        }

        try
        {
            await RunRepositoryOperationAsync(() => _targetInfoRepository.UpdateTarget(editedTarget));

            // 直接修改当前绑定对象，避免替换 SelectedTarget 导致结构树和详情区域引用断开。
            target.Name = editedTarget.Name;
            target.Category = editedTarget.Category;
            target.Description = editedTarget.Description;

            foreach (TargetStructureTreeNode rootNode in target.StructureTreeNodes)
            {
                rootNode.SyncFromModel();
            }

            RebuildTargetCategories();
            SelectedTargetCategory = target.Category;
            FilteredTargets.Refresh();
            SelectedTarget = target;
            RefreshSelectedDetailRows(SelectedStructureNode);
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);
            StatusText = $"已修改目标：{target.Name}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"修改目标失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 修改目标失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 删除目标及其全部结构数据。
    /// </summary>
    public async Task<bool> DeleteTargetAsync(TargetInfoItem target)
    {
        if (!Targets.Contains(target))
        {
            StatusText = "当前目标不存在，无法删除";
            return false;
        }

        try
        {
            bool deletedSelectedTarget = ReferenceEquals(SelectedTarget, target);

            await RunRepositoryOperationAsync(() => _targetInfoRepository.DeleteTarget(target.Code));

            // 数据库删除成功后再解除勾选事件并移除界面数据，避免失败时界面状态丢失。
            DetachCheckStateHandlers(target);
            Targets.Remove(target);
            RebuildTargetCategories();

            // 如果删除的是当前目标，自动切换到筛选后的第一个目标。
            if (deletedSelectedTarget)
            {
                SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
            }

            // 删除目标后，Unity 侧应按新的当前目标或空目标状态刷新可见部件集合。
            ScheduleCheckedPartUnitySync();
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);
            StatusText = $"已删除目标：{target.Name}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"删除目标失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 删除目标失败：{ex}");
            return false;
        }
    }
}
