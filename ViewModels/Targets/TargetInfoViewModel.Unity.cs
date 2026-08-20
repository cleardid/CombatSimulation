using CombatSimulation.Models;
using CombatSimulation.Services.Unity;
using System.Windows;

namespace CombatSimulation.ViewModels;

/// <summary>
/// TargetInfoViewModel 仅保留与绑定对象相关的 Unity 状态投影；通信时序由 TargetUnitySyncCoordinator 负责。
/// </summary>
public sealed partial class TargetInfoViewModel
{
    private void NotifyDatabaseChangedForUnity(bool refreshCurrentDisplayAfterSync = true)
    {
        _unitySyncCoordinator.NotifyDatabaseChanged(refreshCurrentDisplayAfterSync);
    }

    private void ScheduleCheckedPartUnitySync()
    {
        _unitySyncCoordinator.ShowCheckedParts(GetCheckedPartCodes(SelectedTarget));
    }

    private UnityDisplayState CaptureUnityDisplayState()
    {
        System.Windows.Threading.Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(CaptureUnityDisplayStateCore);
        }

        return CaptureUnityDisplayStateCore();
    }

    private UnityDisplayState CaptureUnityDisplayStateCore()
    {
        return new UnityDisplayState(
            SelectedTarget?.Code ?? string.Empty,
            GetCheckedPartCodes(SelectedTarget),
            SelectedStructureNode?.Part?.PartCode ?? string.Empty);
    }

    private void SetStatusTextOnUiThread(string message)
    {
        System.Windows.Threading.Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            StatusText = message;
            return;
        }

        dispatcher.BeginInvoke(() => StatusText = message);
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