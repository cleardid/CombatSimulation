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
        partial void OnSelectedTargetChanged(TargetInfoItem? value)
        {
            DetachCheckStateHandlers(_checkStateSubscriptionTarget);
            _checkStateSubscriptionTarget = value;
            AttachCheckStateHandlers(value);

            StatusText = value == null
                ? "未选择目标"
                : $"当前目标：{value.Name}";

            _targetSelectionCts?.Cancel();
            _targetSelectionCts = new CancellationTokenSource();
            _ = SendSelectedTargetToUnityAsync(value, _targetSelectionCts.Token);
            ScheduleCheckedPartUnitySync();

            SelectedStructureNode = FindPreferredStructureNode(value);
        }

        partial void OnSelectedStructureNodeChanged(TargetStructureTreeNode? value)
        {
            SynchronizeStructureNodeSelection(value);
            RefreshSelectedDetailRows(value);

            _partHighlightCts?.Cancel();

            if (value?.Part != null)
            {
                _partHighlightCts = new CancellationTokenSource();
                _ = SendHighlightedPartToUnityAsync(value.Part, _partHighlightCts.Token);
            }
        }

        partial void OnSelectedInfoPanelChanged(string value)
        {
            StatusText = IsStructureInfoSelected
                ? "当前位于目标结构信息模块"
                : "当前位于毁伤树信息模块";
        }

        [RelayCommand]
        private void ShowStructureInfo()
        {
            SelectedInfoPanel = StructureInfoPanel;
        }

        [RelayCommand]
        private void ShowDamageTreeInfo()
        {
            SelectedInfoPanel = DamageTreeInfoPanel;
        }

        [RelayCommand]
        private void RequestAddTarget()
        {
            TargetEditRequested?.Invoke(this, new TargetEditRequestedEventArgs(CreateTargetDraft(), originalTarget: null, isEditMode: false));
        }

        [RelayCommand]
        private void RequestModifyTarget()
        {
            if (SelectedTarget == null)
            {
                RequestOperationMessage("请选择需要修改的目标。");
                return;
            }

            TargetEditRequested?.Invoke(this, new TargetEditRequestedEventArgs(SelectedTarget, SelectedTarget, isEditMode: true));
        }

        [RelayCommand]
        private void RequestDeleteTarget()
        {
            if (SelectedTarget == null)
            {
                RequestOperationMessage("请选择需要删除的目标。");
                return;
            }

            TargetDeleteRequested?.Invoke(this, new TargetDeleteRequestedEventArgs(SelectedTarget));
        }

        [RelayCommand]
        private void RequestModifyStructureNode(TargetStructureTreeNode? node)
        {
            if (node == null || node.Target != null)
            {
                return;
            }

            SelectStructureNode(node);
            StructureNodeEditRequested?.Invoke(this, new StructureNodeEditRequestedEventArgs(node));
        }

        [RelayCommand]
        private void RequestDeleteStructureNode(TargetStructureTreeNode? node)
        {
            if (node == null || node.Target != null)
            {
                return;
            }

            SelectStructureNode(node);
            StructureNodeDeleteRequested?.Invoke(this, new StructureNodeDeleteRequestedEventArgs(node));
        }

        [RelayCommand]
        private void RequestAddChildSystem(TargetStructureTreeNode? parentNode)
        {
            if (parentNode?.System == null && parentNode?.Target == null)
            {
                RequestOperationMessage("请选择目标根节点或系统节点后再添加子系统。");
                return;
            }

            SelectStructureNode(parentNode);
            StructureChildSystemAddRequested?.Invoke(this, new StructureChildSystemAddRequestedEventArgs(parentNode, CreateChildSystemDraft(parentNode)));
        }

        [RelayCommand]
        private void RequestAddChildPart(TargetStructureTreeNode? parentNode)
        {
            if (parentNode?.System == null)
            {
                RequestOperationMessage("请选择系统节点后再添加底层部件。");
                return;
            }

            SelectStructureNode(parentNode);
            StructureChildPartAddRequested?.Invoke(this, new StructureChildPartAddRequestedEventArgs(parentNode, CreateChildPartDraft(parentNode)));
        }

        private void RequestOperationMessage(string message)
        {
            OperationMessageRequested?.Invoke(this, new OperationMessageRequestedEventArgs(message));
        }
    }
}
