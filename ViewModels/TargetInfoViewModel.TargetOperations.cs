using CombatSimulation.Models;
using CombatSimulation.Services.Targets;
using CombatSimulation.Services.Unity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;

namespace CombatSimulation.ViewModels
{
    public sealed partial class TargetInfoViewModel
    {
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

        public Task<bool> AddTargetAsync(TargetInfoItem newTarget)
        {
            NormalizeTarget(newTarget);

            if (!ValidateTarget(newTarget, except: null))
            {
                return Task.FromResult(false);
            }

            try
            {
                EnsureTargetStructureTree(newTarget);
                _targetInfoRepository.AddTarget(newTarget);

                Targets.Add(newTarget);
                RebuildTargetCategories();

                SelectedTargetCategory = newTarget.Category;
                FilteredTargets.Refresh();
                SelectedTarget = newTarget;
                StatusText = $"已添加目标：{newTarget.Name}";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                StatusText = $"添加目标失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 添加目标失败：{ex}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> UpdateTargetAsync(TargetInfoItem target, TargetInfoItem editedTarget)
        {
            if (!Targets.Contains(target))
            {
                StatusText = "当前目标不存在，无法修改";
                return Task.FromResult(false);
            }

            editedTarget.Code = target.Code;
            NormalizeTarget(editedTarget);

            if (!ValidateTarget(editedTarget, except: target))
            {
                return Task.FromResult(false);
            }

            try
            {
                target.Name = editedTarget.Name;
                target.Category = editedTarget.Category;
                target.Description = editedTarget.Description;

                _targetInfoRepository.UpdateTarget(target);

                foreach (TargetStructureTreeNode rootNode in target.StructureTreeNodes)
                {
                    rootNode.SyncFromModel();
                }

                RebuildTargetCategories();
                SelectedTargetCategory = target.Category;
                FilteredTargets.Refresh();
                SelectedTarget = target;
                RefreshSelectedDetailRows(SelectedStructureNode);
                StatusText = $"已修改目标：{target.Name}";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                StatusText = $"修改目标失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 修改目标失败：{ex}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> DeleteTargetAsync(TargetInfoItem target)
        {
            if (!Targets.Contains(target))
            {
                StatusText = "当前目标不存在，无法删除";
                return Task.FromResult(false);
            }

            try
            {
                bool deletedSelectedTarget = ReferenceEquals(SelectedTarget, target);
                DetachCheckStateHandlers(target);
                _targetInfoRepository.DeleteTarget(target.Code);

                Targets.Remove(target);
                RebuildTargetCategories();

                if (deletedSelectedTarget)
                {
                    SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
                }

                ScheduleCheckedPartUnitySync();
                StatusText = $"已删除目标：{target.Name}";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                StatusText = $"删除目标失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 删除目标失败：{ex}");
                return Task.FromResult(false);
            }
        }
    }
}
