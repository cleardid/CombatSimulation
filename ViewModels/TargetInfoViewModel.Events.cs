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
    public sealed class OperationMessageRequestedEventArgs : EventArgs
    {
        public OperationMessageRequestedEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }

    public sealed class TargetEditRequestedEventArgs : EventArgs
    {
        public TargetEditRequestedEventArgs(TargetInfoItem draft, TargetInfoItem? originalTarget, bool isEditMode)
        {
            Draft = draft;
            OriginalTarget = originalTarget;
            IsEditMode = isEditMode;
        }

        public TargetInfoItem Draft { get; }

        public TargetInfoItem? OriginalTarget { get; }

        public bool IsEditMode { get; }
    }

    public sealed class TargetDeleteRequestedEventArgs : EventArgs
    {
        public TargetDeleteRequestedEventArgs(TargetInfoItem target)
        {
            Target = target;
        }

        public TargetInfoItem Target { get; }
    }

    public sealed class StructureNodeEditRequestedEventArgs : EventArgs
    {
        public StructureNodeEditRequestedEventArgs(TargetStructureTreeNode node)
        {
            Node = node;
        }

        public TargetStructureTreeNode Node { get; }
    }

    public sealed class StructureNodeDeleteRequestedEventArgs : EventArgs
    {
        public StructureNodeDeleteRequestedEventArgs(TargetStructureTreeNode node)
        {
            Node = node;
        }

        public TargetStructureTreeNode Node { get; }
    }

    public sealed class StructureChildSystemAddRequestedEventArgs : EventArgs
    {
        public StructureChildSystemAddRequestedEventArgs(TargetStructureTreeNode parentNode, TargetSystemInfoItem draft)
        {
            ParentNode = parentNode;
            Draft = draft;
        }

        public TargetStructureTreeNode ParentNode { get; }

        public TargetSystemInfoItem Draft { get; }
    }

    public sealed class StructureChildPartAddRequestedEventArgs : EventArgs
    {
        public StructureChildPartAddRequestedEventArgs(TargetStructureTreeNode parentNode, TargetPartInfoItem draft)
        {
            ParentNode = parentNode;
            Draft = draft;
        }

        public TargetStructureTreeNode ParentNode { get; }

        public TargetPartInfoItem Draft { get; }
    }

}
