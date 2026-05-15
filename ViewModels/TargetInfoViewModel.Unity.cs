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
        private async Task SendSelectedTargetToUnityAsync(TargetInfoItem? target, CancellationToken cancellationToken)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.Code))
            {
                return;
            }

            try
            {
                await _unityTargetCommandService.ShowTargetAsync(target.Code, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                StatusText = $"Unity 显示目标命令发送失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] Unity 显示目标失败：{ex}");
            }
        }

        private async Task SendHighlightedPartToUnityAsync(TargetPartInfoItem part, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(part.PartCode))
            {
                return;
            }

            try
            {
                await _unityTargetCommandService.HighlightPartAsync(part.PartCode, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                StatusText = $"Unity 高亮部件命令发送失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] Unity 高亮部件失败：{ex}");
            }
        }

        private void ScheduleCheckedPartUnitySync()
        {
            _checkedPartSyncCts?.Cancel();

            CancellationTokenSource cts = new();
            _checkedPartSyncCts = cts;
            _ = SendCheckedPartsToUnityAsync(cts);
        }

        private async Task SendCheckedPartsToUnityAsync(CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(50, cts.Token).ConfigureAwait(true);
                List<string> checkedPartCodes = GetCheckedPartCodes(SelectedTarget);
                await _unityTargetCommandService.ShowPartsAsync(checkedPartCodes, cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                StatusText = $"Unity 显示部件命令发送失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] Unity 显示勾选部件失败：{ex}");
            }
            finally
            {
                if (ReferenceEquals(_checkedPartSyncCts, cts))
                {
                    _checkedPartSyncCts = null;
                }

                cts.Dispose();
            }
        }

        private void OnStructureNodeCheckStateChanged(object? sender, EventArgs e)
        {
            ScheduleCheckedPartUnitySync();
        }

        private void AttachCheckStateHandlers(TargetInfoItem? target)
        {
            if (target == null)
            {
                return;
            }

            foreach (TargetStructureTreeNode node in EnumerateNodes(target.StructureTreeNodes))
            {
                node.CheckStateChangedByUser += OnStructureNodeCheckStateChanged;
            }
        }

        private void DetachCheckStateHandlers(TargetInfoItem? target)
        {
            if (target == null)
            {
                return;
            }

            foreach (TargetStructureTreeNode node in EnumerateNodes(target.StructureTreeNodes))
            {
                node.CheckStateChangedByUser -= OnStructureNodeCheckStateChanged;
            }
        }

        private static List<string> GetCheckedPartCodes(TargetInfoItem? target)
        {
            if (target == null)
            {
                return new List<string>();
            }

            return EnumerateNodes(target.StructureTreeNodes)
                .Where(node => node.Part != null && node.IsChecked && !string.IsNullOrWhiteSpace(node.Part.PartCode))
                .Select(node => node.Part!.PartCode)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<TargetStructureTreeNode> EnumerateNodes(IEnumerable<TargetStructureTreeNode> nodes)
        {
            foreach (TargetStructureTreeNode node in nodes)
            {
                yield return node;

                foreach (TargetStructureTreeNode child in EnumerateNodes(node.Children))
                {
                    yield return child;
                }
            }
        }

        private void AttachCheckStateHandler(TargetStructureTreeNode node)
        {
            foreach (TargetStructureTreeNode child in EnumerateNodes(new[] { node }))
            {
                child.CheckStateChangedByUser += OnStructureNodeCheckStateChanged;
            }
        }

        private void DetachCheckStateHandler(TargetStructureTreeNode node)
        {
            foreach (TargetStructureTreeNode child in EnumerateNodes(new[] { node }))
            {
                child.CheckStateChangedByUser -= OnStructureNodeCheckStateChanged;
            }
        }
    }
}
