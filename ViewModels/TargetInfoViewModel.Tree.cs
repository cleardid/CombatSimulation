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
        public void SelectStructureNode(TargetStructureTreeNode? node)
        {
            SynchronizeStructureNodeSelection(node);

            if (!ReferenceEquals(SelectedStructureNode, node))
            {
                SelectedStructureNode = node;
            }
            else
            {
                RefreshSelectedDetailRows(node);
            }
        }

        private void SynchronizeStructureNodeSelection(TargetStructureTreeNode? node)
        {
            if (_isSynchronizingStructureNodeSelection)
            {
                return;
            }

            _isSynchronizingStructureNodeSelection = true;
            try
            {
                if (SelectedTarget != null)
                {
                    ClearSelection(SelectedTarget.StructureTreeNodes);
                }

                if (node != null)
                {
                    node.IsSelected = true;
                }
            }
            finally
            {
                _isSynchronizingStructureNodeSelection = false;
            }
        }

        private static void ClearSelection(IEnumerable<TargetStructureTreeNode> nodes)
        {
            foreach (TargetStructureTreeNode node in nodes)
            {
                node.IsSelected = false;
                ClearSelection(node.Children);
            }
        }

        private TargetStructureTreeNode? FindParentNode(IEnumerable<TargetStructureTreeNode> nodes, TargetStructureTreeNode targetNode)
        {
            foreach (TargetStructureTreeNode node in nodes)
            {
                if (node.Children.Contains(targetNode))
                {
                    return node;
                }

                TargetStructureTreeNode? childParent = FindParentNode(node.Children, targetNode);
                if (childParent != null)
                {
                    return childParent;
                }
            }

            return null;
        }

        private string CreateUniqueSystemCode()
        {
            string code;
            do
            {
                code = Guid.NewGuid().ToString("N");
            }
            while (IsSystemCodeUsed(code, except: null));

            return code;
        }

        private string CreateUniquePartCode()
        {
            string code;
            do
            {
                code = Guid.NewGuid().ToString("N");
            }
            while (IsPartCodeUsed(code, except: null));

            return code;
        }

        private string CreateUniqueTargetCode()
        {
            string code;
            do
            {
                code = Guid.NewGuid().ToString("N");
            }
            while (Targets.Any(target => string.Equals(target.Code, code, StringComparison.Ordinal)));

            return code;
        }

        private bool IsSystemCodeUsed(string systemCode, TargetSystemInfoItem? except)
        {
            if (SelectedTarget == null || string.IsNullOrWhiteSpace(systemCode))
            {
                return false;
            }

            return EnumerateSystems(SelectedTarget.Systems)
                .Any(system => !ReferenceEquals(system, except) && string.Equals(system.SystemCode, systemCode, StringComparison.Ordinal));
        }

        private bool IsPartCodeUsed(string partCode, TargetPartInfoItem? except)
        {
            if (SelectedTarget == null || string.IsNullOrWhiteSpace(partCode))
            {
                return false;
            }

            return EnumerateSystems(SelectedTarget.Systems)
                .SelectMany(system => system.Parts)
                .Any(part => !ReferenceEquals(part, except) && string.Equals(part.PartCode, partCode, StringComparison.Ordinal));
        }

        private static IEnumerable<TargetSystemInfoItem> EnumerateSystems(IEnumerable<TargetSystemInfoItem> systems)
        {
            foreach (TargetSystemInfoItem system in systems)
            {
                yield return system;

                foreach (TargetSystemInfoItem childSystem in EnumerateSystems(system.ChildSystems))
                {
                    yield return childSystem;
                }
            }
        }

        private static TargetStructureTreeNode? FindPreferredStructureNode(TargetInfoItem? target)
        {
            if (target == null)
            {
                return null;
            }

            TargetStructureTreeNode? selectedNode = FindNode(target.StructureTreeNodes, node => node.IsSelected);
            if (selectedNode != null)
            {
                return selectedNode;
            }

            TargetStructureTreeNode? firstPartNode = FindNode(target.StructureTreeNodes, node => node.Part != null);
            if (firstPartNode != null)
            {
                firstPartNode.IsSelected = true;
                return firstPartNode;
            }

            return target.StructureTreeNodes.FirstOrDefault();
        }

        private static TargetStructureTreeNode? FindNode(IEnumerable<TargetStructureTreeNode> nodes, Func<TargetStructureTreeNode, bool> predicate)
        {
            foreach (TargetStructureTreeNode node in nodes)
            {
                if (predicate(node))
                {
                    return node;
                }

                TargetStructureTreeNode? childResult = FindNode(node.Children, predicate);
                if (childResult != null)
                {
                    return childResult;
                }
            }

            return null;
        }

        private static int CountSystems(TargetInfoItem target)
        {
            return target.Systems.Sum(system => 1 + CountChildSystems(system));
        }

        private static int CountChildSystems(TargetSystemInfoItem system)
        {
            return system.ChildSystems.Sum(child => 1 + CountChildSystems(child));
        }

        private static int CountParts(TargetInfoItem target)
        {
            return target.Systems.Sum(CountParts);
        }

        private static int CountParts(TargetSystemInfoItem system)
        {
            return system.Parts.Count + system.ChildSystems.Sum(CountParts);
        }

        private static TargetPartParameterItem Parameter(int index, string name, double value, string unit = "mm")
        {
            return new TargetPartParameterItem
            {
                Index = index,
                Name = name,
                Value = value,
                Unit = unit
            };
        }

        private static TargetStructureTreeNode CreateSystemNode(TargetSystemInfoItem system)
        {
            TargetStructureTreeNode node = TargetStructureTreeNode.ForSystem(system);

            foreach (TargetSystemInfoItem childSystem in system.ChildSystems)
            {
                node.Children.Add(CreateSystemNode(childSystem));
            }

            foreach (TargetPartInfoItem part in system.Parts)
            {
                node.Children.Add(TargetStructureTreeNode.ForPart(part));
            }

            return node;
        }
    }
}
