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
        partial void OnSelectedTargetCategoryChanged(string value)
        {
            FilteredTargets.Refresh();

            if (!IsSelectedTargetInCurrentFilter())
            {
                SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
            }
        }

        private void LoadTargetsFromRepository()
        {
            Targets.Clear();

            try
            {
                IReadOnlyList<TargetInfoItem> storedTargets = _targetInfoRepository.LoadTargets();
                foreach (TargetInfoItem target in storedTargets)
                {
                    EnsureTargetStructureTree(target);
                    Targets.Add(target);
                }

                _targetLoadStatusText = Targets.Count == 0
                    ? "数据库中暂无目标信息"
                    : $"已从数据库读取 {Targets.Count} 个目标";
                StatusText = _targetLoadStatusText;
            }
            catch (Exception ex)
            {
                _targetLoadStatusText = $"读取目标数据库失败：{ex.Message}";
                StatusText = _targetLoadStatusText;
                Debug.WriteLine($"[TargetInfoViewModel] 读取目标数据库失败：{ex}");
            }
        }

        private bool IsSelectedTargetInCurrentFilter()
        {
            if (SelectedTarget == null)
            {
                return false;
            }

            return FilterTargetByCategory(SelectedTarget);
        }

        private bool FilterTargetByCategory(object item)
        {
            if (item is not TargetInfoItem target)
            {
                return false;
            }

            return SelectedTargetCategory == AllTargetCategory ||
                   SelectedTargetCategory == target.Category;
        }

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

            SelectedTargetCategory = TargetCategories.Contains(selectedCategory)
                ? selectedCategory
                : AllTargetCategory;

            if (refreshFilteredTargets)
            {
                FilteredTargets.Refresh();
            }
        }

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

        private static void NormalizeTarget(TargetInfoItem target)
        {
            target.Name = target.Name.Trim();
            target.Category = target.Category.Trim();
            target.Description = target.Description.Trim();
            target.Code = target.Code.Trim();
        }

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
}
