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
        public TargetSystemInfoItem CreateChildSystemDraft(TargetStructureTreeNode parentNode)
        {
            bool isTopSystem = parentNode.Target != null;
            string targetCode = parentNode.Target?.Code ?? parentNode.System?.TargetCode ?? SelectedTarget?.Code ?? string.Empty;
            string parentSystemCode = isTopSystem ? "-1" : (parentNode.System?.SystemCode ?? "-1");

            return new TargetSystemInfoItem
            {
                SystemCode = CreateUniqueSystemCode(),
                SystemName = isTopSystem ? "新建顶层系统" : "新建子系统",
                SystemDescription = string.Empty,
                IsTopSystem = isTopSystem,
                ParentSystemCode = parentSystemCode,
                TargetCode = targetCode
            };
        }

        public TargetPartInfoItem CreateChildPartDraft(TargetStructureTreeNode parentNode)
        {
            return new TargetPartInfoItem
            {
                PartCode = CreateUniquePartCode(),
                PartName = "新建部件",
                ShapeType = "长方体",
                PartDescription = string.Empty,
                MaterialId = "Fe",
                DisplayColor = "#808080",
                EquivalentThickness = 0,
                VulnerableArea = 0,
                SystemCode = parentNode.System?.SystemCode ?? string.Empty,
                CenterX = 0,
                CenterY = 0,
                CenterZ = 0,
                RotationX = 0,
                RotationY = 0,
                RotationZ = 0,
                Parameters = new ObservableCollection<TargetPartParameterItem>
                {
                    Parameter(1, "长", 0),
                    Parameter(2, "宽", 0),
                    Parameter(3, "高", 0)
                }
            };
        }

        public Task<bool> AddChildSystemAsync(TargetStructureTreeNode parentNode, TargetSystemInfoItem newSystem)
        {
            if (SelectedTarget == null || (parentNode.System == null && parentNode.Target == null))
            {
                StatusText = "请选择目标根节点或系统节点后再添加子系统";
                return Task.FromResult(false);
            }

            if (IsSystemCodeUsed(newSystem.SystemCode, except: null))
            {
                StatusText = $"系统唯一标识已存在：{newSystem.SystemCode}";
                return Task.FromResult(false);
            }

            try
            {
                bool isTopSystem = parentNode.Target != null;
                newSystem.IsTopSystem = isTopSystem;
                newSystem.ParentSystemCode = isTopSystem ? "-1" : (parentNode.System?.SystemCode ?? "-1");
                newSystem.TargetCode = SelectedTarget.Code;

                _targetInfoRepository.AddSystem(SelectedTarget.Code, newSystem);

                if (isTopSystem)
                {
                    SelectedTarget.Systems.Add(newSystem);
                }
                else
                {
                    parentNode.System!.ChildSystems.Add(newSystem);
                }

                TargetStructureTreeNode newNode = CreateSystemNode(newSystem);
                parentNode.Children.Add(newNode);
                AttachCheckStateHandler(newNode);
                parentNode.IsExpanded = true;
                SelectStructureNode(newNode);
                ScheduleCheckedPartUnitySync();

                StatusText = isTopSystem ? $"已添加顶层系统：{newSystem.SystemName}" : $"已添加子系统：{newSystem.SystemName}";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                StatusText = $"添加子系统失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 添加子系统失败：{ex}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> AddChildPartAsync(TargetStructureTreeNode parentNode, TargetPartInfoItem newPart)
        {
            if (SelectedTarget == null || parentNode.System == null)
            {
                StatusText = "请选择系统节点后再添加底层部件";
                return Task.FromResult(false);
            }

            if (IsPartCodeUsed(newPart.PartCode, except: null))
            {
                StatusText = $"部件唯一标识已存在：{newPart.PartCode}";
                return Task.FromResult(false);
            }

            try
            {
                TargetSystemInfoItem parentSystem = parentNode.System;
                newPart.SystemCode = parentSystem.SystemCode;

                _targetInfoRepository.AddPart(SelectedTarget.Code, newPart);

                parentSystem.Parts.Add(newPart);
                TargetStructureTreeNode newNode = TargetStructureTreeNode.ForPart(newPart);
                parentNode.Children.Add(newNode);
                AttachCheckStateHandler(newNode);
                parentNode.IsExpanded = true;
                SelectStructureNode(newNode);
                ScheduleCheckedPartUnitySync();

                StatusText = $"已添加底层部件：{newPart.PartName}";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                StatusText = $"添加底层部件失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 添加底层部件失败：{ex}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> UpdateSystemAsync(TargetStructureTreeNode node, TargetSystemInfoItem editedSystem)
        {
            if (SelectedTarget == null)
            {
                StatusText = "未选择目标，不能修改系统信息";
                return Task.FromResult(false);
            }

            if (node.System == null)
            {
                StatusText = "当前节点不是系统节点，不能修改系统信息";
                return Task.FromResult(false);
            }

            TargetSystemInfoItem system = node.System;
            if (IsSystemCodeUsed(editedSystem.SystemCode, system))
            {
                StatusText = $"系统唯一标识已存在：{editedSystem.SystemCode}";
                return Task.FromResult(false);
            }

            try
            {
                string oldSystemCode = system.SystemCode;
                _targetInfoRepository.UpdateSystem(SelectedTarget.Code, oldSystemCode, editedSystem);

                system.SystemName = editedSystem.SystemName;
                system.SystemCode = editedSystem.SystemCode;
                system.SystemDescription = editedSystem.SystemDescription;
                system.IsTopSystem = editedSystem.IsTopSystem;
                system.ParentSystemCode = editedSystem.ParentSystemCode;
                system.TargetCode = editedSystem.TargetCode;

                if (!string.Equals(oldSystemCode, system.SystemCode, StringComparison.Ordinal))
                {
                    foreach (TargetPartInfoItem part in system.Parts)
                    {
                        if (string.Equals(part.SystemCode, oldSystemCode, StringComparison.Ordinal))
                        {
                            part.SystemCode = system.SystemCode;
                        }
                    }

                    foreach (TargetSystemInfoItem childSystem in system.ChildSystems)
                    {
                        if (string.Equals(childSystem.ParentSystemCode, oldSystemCode, StringComparison.Ordinal))
                        {
                            childSystem.ParentSystemCode = system.SystemCode;
                        }
                    }
                }

                node.SyncFromModel();
                RefreshSelectedDetailRows(node);
                StatusText = $"已修改系统：{system.SystemName}";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                StatusText = $"修改系统失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 修改系统失败：{ex}");
                return Task.FromResult(false);
            }
        }

        public async Task<bool> UpdatePartAsync(TargetStructureTreeNode node, TargetPartInfoItem editedPart)
        {
            if (SelectedTarget == null)
            {
                StatusText = "未选择目标，不能修改部件信息";
                return false;
            }

            if (node.Part == null)
            {
                StatusText = "当前节点不是部件节点，不能修改部件信息";
                return false;
            }

            TargetPartInfoItem part = node.Part;
            if (IsPartCodeUsed(editedPart.PartCode, part))
            {
                StatusText = $"部件唯一标识已存在：{editedPart.PartCode}";
                return false;
            }

            try
            {
                string oldPartCode = part.PartCode;

                // 部件编辑必须先同步 Unity，Unity 成功后再写数据库和刷新本地模型。
                await _unityTargetCommandService.UpdateTargetPartAsync(editedPart).ConfigureAwait(true);
                _targetInfoRepository.UpdatePart(SelectedTarget.Code, oldPartCode, editedPart);

                ApplyPartUpdate(part, editedPart);

                node.SyncFromModel();
                RefreshSelectedDetailRows(node);
                ScheduleCheckedPartUnitySync();

                if (ReferenceEquals(SelectedStructureNode, node))
                {
                    await SendHighlightedPartToUnityAsync(part).ConfigureAwait(true);
                }

                StatusText = $"已修改部件：{part.PartName}";
                return true;
            }
            catch (Exception ex)
            {
                StatusText = $"修改部件失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 修改部件失败：{ex}");
                return false;
            }
        }

        private static void ApplyPartUpdate(TargetPartInfoItem part, TargetPartInfoItem editedPart)
        {
            part.PartCode = editedPart.PartCode;
            part.PartName = editedPart.PartName;
            part.ShapeType = editedPart.ShapeType;
            part.PartDescription = editedPart.PartDescription;
            part.MaterialId = editedPart.MaterialId;
            part.DisplayColor = editedPart.DisplayColor;
            part.EquivalentThickness = editedPart.EquivalentThickness;
            part.VulnerableArea = editedPart.VulnerableArea;
            part.SystemCode = editedPart.SystemCode;
            part.CenterX = editedPart.CenterX;
            part.CenterY = editedPart.CenterY;
            part.CenterZ = editedPart.CenterZ;
            part.RotationX = editedPart.RotationX;
            part.RotationY = editedPart.RotationY;
            part.RotationZ = editedPart.RotationZ;
            part.Parameters.Clear();

            foreach (TargetPartParameterItem parameter in editedPart.Parameters.OrderBy(item => item.Index))
            {
                part.Parameters.Add(new TargetPartParameterItem
                {
                    Index = parameter.Index,
                    Name = parameter.Name,
                    Value = parameter.Value,
                    Unit = parameter.Unit
                });
            }
        }

        public Task<bool> DeleteStructureNodeAsync(TargetStructureTreeNode node)
        {
            if (SelectedTarget == null)
            {
                StatusText = "未选择目标，无法删除结构节点";
                return Task.FromResult(false);
            }

            try
            {
                TargetStructureTreeNode? parentNode = FindParentNode(SelectedTarget.StructureTreeNodes, node);
                if (node.Part != null)
                {
                    if (parentNode?.System == null)
                    {
                        StatusText = "未找到部件所属系统，删除失败";
                        return Task.FromResult(false);
                    }

                    DetachCheckStateHandler(node);
                    _targetInfoRepository.DeletePart(SelectedTarget.Code, node.Part.PartCode);
                    parentNode.System.Parts.Remove(node.Part);
                    parentNode.Children.Remove(node);
                    SelectStructureNode(parentNode);
                    ScheduleCheckedPartUnitySync();
                    StatusText = $"已删除部件：{node.Name}";
                    return Task.FromResult(true);
                }

                if (node.System != null)
                {
                    if (parentNode == null)
                    {
                        StatusText = "不能直接删除目标根节点";
                        return Task.FromResult(false);
                    }

                    DetachCheckStateHandler(node);
                    _targetInfoRepository.DeleteSystem(SelectedTarget.Code, node.System.SystemCode);

                    if (parentNode.Target != null)
                    {
                        SelectedTarget.Systems.Remove(node.System);
                    }
                    else if (parentNode.System != null)
                    {
                        parentNode.System.ChildSystems.Remove(node.System);
                    }

                    parentNode.Children.Remove(node);
                    SelectStructureNode(parentNode);
                    ScheduleCheckedPartUnitySync();
                    StatusText = $"已删除系统：{node.Name}";
                    return Task.FromResult(true);
                }

                StatusText = "不能删除目标根节点";
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                StatusText = $"删除结构节点失败：{ex.Message}";
                Debug.WriteLine($"[TargetInfoViewModel] 删除结构节点失败：{ex}");
                return Task.FromResult(false);
            }
        }
    }
}
