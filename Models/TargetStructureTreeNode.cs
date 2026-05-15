using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CombatSimulation.Models
{
    /// <summary>
    /// 结构树显示节点。用于把目标、系统、部件统一投影到 TreeView。
    /// </summary>
    public sealed partial class TargetStructureTreeNode : ObservableObject
    {
        [ObservableProperty]
        private string _nodeType = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _code = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isChecked = true;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private bool _isSelected;

        private bool _isUpdatingCheckState;

        public TargetInfoItem? Target { get; set; }

        public TargetSystemInfoItem? System { get; set; }

        public TargetPartInfoItem? Part { get; set; }

        public TargetStructureTreeNode? Parent { get; private set; }

        public ObservableCollection<TargetStructureTreeNode> Children { get; } = new();

        /// <summary>
        /// 节点勾选状态由用户直接切换后触发。
        /// 由父子联动造成的程序化勾选变化不会重复触发该事件。
        /// </summary>
        public event EventHandler? CheckStateChangedByUser;

        public TargetStructureTreeNode()
        {
            Children.CollectionChanged += OnChildrenCollectionChanged;
        }

        partial void OnIsCheckedChanged(bool value)
        {
            if (_isUpdatingCheckState)
            {
                return;
            }

            ApplyCheckStateFromUser(value);
            CheckStateChangedByUser?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyCheckStateFromUser(bool isChecked)
        {
            RunCheckStateUpdate(() =>
            {
                if (isChecked)
                {
                    // 主动勾选目标或系统时，该节点下的所有子系统和部件都应一起勾选。
                    // 主动勾选部件时没有子节点，因此只会向上勾选父系统和目标根节点。
                    SetDescendantCheckState(true);
                    SetAncestorCheckState(true);
                }
                else
                {
                    // 主动取消目标或系统时，该节点下的所有子系统和部件都应一起取消。
                    // 随后从父节点开始重新计算：只要还有任意子节点被勾选，父节点保持勾选；否则取消。
                    SetDescendantCheckState(false);
                    RefreshAncestorCheckStateByChildren();
                }
            });
        }

        private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RunCheckStateUpdate(() =>
            {
                if (e.OldItems != null)
                {
                    foreach (TargetStructureTreeNode child in e.OldItems.OfType<TargetStructureTreeNode>())
                    {
                        if (ReferenceEquals(child.Parent, this))
                        {
                            child.Parent = null;
                        }
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (TargetStructureTreeNode child in e.NewItems.OfType<TargetStructureTreeNode>())
                    {
                        child.Parent = this;
                        child.SetSubtreeCheckState(IsChecked);
                    }
                }

                RefreshCurrentAndAncestorCheckStateByChildren();
            });
        }

        private void SetAncestorCheckState(bool value)
        {
            TargetStructureTreeNode? parent = Parent;
            while (parent != null)
            {
                parent.SetSelfCheckState(value);
                parent = parent.Parent;
            }
        }

        private void RefreshAncestorCheckStateByChildren()
        {
            TargetStructureTreeNode? parent = Parent;
            while (parent != null)
            {
                bool shouldBeChecked = parent.Children.Any(child => child.IsChecked);
                parent.SetSelfCheckState(shouldBeChecked);
                parent = parent.Parent;
            }
        }

        private void RefreshCurrentAndAncestorCheckStateByChildren()
        {
            TargetStructureTreeNode? node = this;
            while (node != null)
            {
                if (node.Children.Count > 0 || node.System != null || node.Target != null)
                {
                    bool shouldBeChecked = node.Children.Any(child => child.IsChecked);
                    node.SetSelfCheckState(shouldBeChecked);
                }

                node = node.Parent;
            }
        }

        private void SetDescendantCheckState(bool value)
        {
            foreach (TargetStructureTreeNode child in Children)
            {
                child.SetSubtreeCheckState(value);
            }
        }

        private void SetSubtreeCheckState(bool value)
        {
            SetSelfCheckState(value);
            SetDescendantCheckState(value);
        }

        private void SetSelfCheckState(bool value)
        {
            if (IsChecked == value)
            {
                return;
            }

            _isUpdatingCheckState = true;
            try
            {
                SetProperty(ref _isChecked, value, nameof(IsChecked));
            }
            finally
            {
                _isUpdatingCheckState = false;
            }
        }

        private void RunCheckStateUpdate(Action updateAction)
        {
            if (_isUpdatingCheckState)
            {
                updateAction();
                return;
            }

            _isUpdatingCheckState = true;
            try
            {
                updateAction();
            }
            finally
            {
                _isUpdatingCheckState = false;
            }
        }

        public static TargetStructureTreeNode ForTarget(TargetInfoItem target)
        {
            return new TargetStructureTreeNode
            {
                NodeType = "目标",
                Name = target.Name,
                Code = target.Code,
                Description = target.Description,
                Target = target,
                IsExpanded = true
            };
        }

        public static TargetStructureTreeNode ForSystem(TargetSystemInfoItem system)
        {
            return new TargetStructureTreeNode
            {
                NodeType = "系统",
                Name = system.SystemName,
                Code = system.SystemCode,
                Description = system.SystemDescription,
                System = system,
                IsExpanded = true
            };
        }

        public static TargetStructureTreeNode ForPart(TargetPartInfoItem part)
        {
            return new TargetStructureTreeNode
            {
                NodeType = "部件",
                Name = part.PartName,
                Code = part.PartCode,
                Description = part.PartDescription,
                Part = part,
                IsExpanded = false
            };
        }

        public void SyncFromModel()
        {
            if (Target != null)
            {
                Name = Target.Name;
                Code = Target.Code;
                Description = Target.Description;
                return;
            }

            if (System != null)
            {
                Name = System.SystemName;
                Code = System.SystemCode;
                Description = System.SystemDescription;
                return;
            }

            if (Part != null)
            {
                Name = Part.PartName;
                Code = Part.PartCode;
                Description = Part.PartDescription;
            }
        }
    }
}
