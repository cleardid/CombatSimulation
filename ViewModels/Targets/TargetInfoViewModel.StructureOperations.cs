using CombatSimulation.Models;
using CombatSimulation.Services.MySql;
using CombatSimulation.Services.Targets;
using CombatSimulation.Services.Unity;
using System.Diagnostics;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// 根据当前选中的父节点创建一个系统编辑草稿。
    /// </summary>
    /// <remarks>
    /// 该方法只生成内存草稿，不写数据库。用户在弹窗中确认保存后，才会调用 AddChildSystemAsync。
    /// </remarks>
    public TargetSystemInfoItem CreateChildSystemDraft(TargetStructureTreeNode parentNode)
    {
        // 父节点是目标根节点时，新系统属于顶层系统；父节点是系统节点时，新系统属于子系统。
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

    /// <summary>
    /// 根据当前选中的系统节点创建一个部件编辑草稿。
    /// </summary>
    /// <remarks>
    /// 新建部件默认使用长方体参数模板。参数语义由 TargetPartShapeParameterDefinitions 统一维护。
    /// </remarks>
    public TargetPartInfoItem CreateChildPartDraft(TargetStructureTreeNode parentNode)
    {
        return new TargetPartInfoItem
        {
            PartCode = CreateUniquePartCode(),
            PartName = "新建部件",
            ShapeType = "长方体",
            PartDescription = string.Empty,
            MaterialId = "Fe",
            DisplayColor = TargetPartColorFormat.DefaultWpfDisplayColor,
            EquivalentThickness = 0,
            VulnerableArea = 0,
            SystemCode = parentNode.System?.SystemCode ?? string.Empty,
            CenterX = 0,
            CenterY = 0,
            CenterZ = 0,
            RotationX = 0,
            RotationY = 0,
            RotationZ = 0,
            Parameters = TargetPartShapeParameterDefinitions.CreateDefaultParameters("长方体")
        };
    }

    /// <summary>
    /// 添加顶层系统或子系统，并同步数据库、本地模型和结构树节点。
    /// </summary>
    public async Task<bool> AddChildSystemAsync(TargetStructureTreeNode parentNode, TargetSystemInfoItem newSystem)
    {
        if (SelectedTarget == null || (parentNode.System == null && parentNode.Target == null))
        {
            StatusText = "请选择目标根节点或系统节点后再添加子系统";
            return false;
        }

        if (IsSystemCodeUsed(newSystem.SystemCode, except: null))
        {
            StatusText = $"系统唯一标识已存在：{newSystem.SystemCode}";
            return false;
        }

        try
        {
            // 保存前重新校准父子关系，避免弹窗草稿被外部修改导致父系统写错。
            bool isTopSystem = parentNode.Target != null;
            newSystem.IsTopSystem = isTopSystem;
            newSystem.ParentSystemCode = isTopSystem ? "-1" : (parentNode.System?.SystemCode ?? "-1");
            newSystem.TargetCode = SelectedTarget.Code;

            // 先写数据库。数据库成功后再修改内存和界面，避免界面出现未持久化的数据。
            string targetCode = SelectedTarget.Code;
            await RunRepositoryOperationAsync(() => _targetInfoRepository.AddSystem(targetCode, newSystem));

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

            // 系统本身不是 Unity 部件，但系统表已经变化，需要刷新 Unity 全局数据库快照。
            ScheduleCheckedPartUnitySync();
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);

            StatusText = isTopSystem ? $"已添加顶层系统：{newSystem.SystemName}" : $"已添加子系统：{newSystem.SystemName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"添加子系统失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 添加子系统失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 添加底层部件，并同步数据库、本地模型、结构树节点和 Unity 显示部件集合。
    /// </summary>
    public async Task<bool> AddChildPartAsync(TargetStructureTreeNode parentNode, TargetPartInfoItem newPart)
    {
        if (SelectedTarget == null || parentNode.System == null)
        {
            StatusText = "请选择系统节点后再添加底层部件";
            return false;
        }

        if (IsPartCodeUsed(newPart.PartCode, except: null))
        {
            StatusText = $"部件唯一标识已存在：{newPart.PartCode}";
            return false;
        }

        try
        {
            TargetSystemInfoItem parentSystem = parentNode.System;
            newPart.SystemCode = parentSystem.SystemCode;

            // 先写数据库，避免本地树节点已经添加但数据库保存失败。
            string targetCode = SelectedTarget.Code;
            await RunRepositoryOperationAsync(() => _targetInfoRepository.AddPart(targetCode, newPart));

            parentSystem.Parts.Add(newPart);
            TargetStructureTreeNode newNode = TargetStructureTreeNode.ForPart(newPart);
            parentNode.Children.Add(newNode);
            AttachCheckStateHandler(newNode);
            parentNode.IsExpanded = true;
            SelectStructureNode(newNode);

            // 新部件默认可能处于勾选状态，因此需要通知 Unity 刷新快照和可见部件集合。
            ScheduleCheckedPartUnitySync();
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);

            StatusText = $"已添加底层部件：{newPart.PartName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"添加底层部件失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 添加底层部件失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 修改系统信息，并在系统编号变化时同步修正子系统和部件外键。
    /// </summary>
    public async Task<bool> UpdateSystemAsync(TargetStructureTreeNode node, TargetSystemInfoItem editedSystem)
    {
        if (SelectedTarget == null)
        {
            StatusText = "未选择目标，不能修改系统信息";
            return false;
        }

        if (node.System == null)
        {
            StatusText = "当前节点不是系统节点，不能修改系统信息";
            return false;
        }

        TargetSystemInfoItem system = node.System;
        if (IsSystemCodeUsed(editedSystem.SystemCode, system))
        {
            StatusText = $"系统唯一标识已存在：{editedSystem.SystemCode}";
            return false;
        }

        try
        {
            string oldSystemCode = system.SystemCode;

            // 仓储层负责同步数据库中的子系统 ParentCode 和部件 SystemCode。
            string targetCode = SelectedTarget.Code;
            await RunRepositoryOperationAsync(() => _targetInfoRepository.UpdateSystem(targetCode, oldSystemCode, editedSystem));

            // 数据库成功后再覆盖当前内存对象。这样 TreeView 绑定对象不会被整体替换。
            system.SystemName = editedSystem.SystemName;
            system.SystemCode = editedSystem.SystemCode;
            system.SystemDescription = editedSystem.SystemDescription;
            system.IsTopSystem = editedSystem.IsTopSystem;
            system.ParentSystemCode = editedSystem.ParentSystemCode;
            system.TargetCode = editedSystem.TargetCode;

            if (!string.Equals(oldSystemCode, system.SystemCode, StringComparison.Ordinal))
            {
                // 系统编号是子系统和部件的外键。本地对象也要和数据库保持一致。
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
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);
            StatusText = $"已修改系统：{system.SystemName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"修改系统失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 修改系统失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 修改单个部件。顺序为：Unity 确认成功、写数据库、刷新本地模型和界面。
    /// </summary>
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
            // Unity 端需要使用数据库结构作为 data，因此这里先把 WPF 模型转换为 Target_Part_Info_By_MySQL。
            Target_Part_Info_By_MySQL unityPartData = _targetInfoRepository.CreatePartCommandData(editedPart);
            await _unityCommandService.UpdateTargetPartAsync(unityPartData).ConfigureAwait(true);

            // Unity 已确认修改成功后，才允许持久化数据库，避免 Unity 和 MySQL 状态不一致。
            string targetCode = SelectedTarget.Code;
            await RunRepositoryOperationAsync(() => _targetInfoRepository.UpdatePart(targetCode, oldPartCode, editedPart));

            // 不替换 part 实例，直接复制属性。这样 TreeView、详情面板、弹窗引用都能继续使用同一对象。
            ApplyPartUpdate(part, editedPart);

            node.SyncFromModel();
            RefreshSelectedDetailRows(node);
            ScheduleCheckedPartUnitySync();
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);

            if (ReferenceEquals(SelectedStructureNode, node))
            {
                // 部件编码可能已变化，修改完成后重新高亮当前部件。
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

    /// <summary>
    /// 将编辑草稿中的部件信息复制回结构树正在绑定的部件对象。
    /// </summary>
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

        // 参数集合采用清空后重建，确保形状变化时旧参数不会残留。
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

    /// <summary>
    /// 删除结构树中的系统节点或部件节点，并同步数据库和 Unity 显示状态。
    /// </summary>
    public async Task<bool> DeleteStructureNodeAsync(TargetStructureTreeNode node)
    {
        if (SelectedTarget == null)
        {
            StatusText = "未选择目标，无法删除结构节点";
            return false;
        }

        try
        {
            TargetStructureTreeNode? parentNode = FindParentNode(SelectedTarget.StructureTreeNodes, node);
            if (node.Part != null)
            {
                if (parentNode?.System == null)
                {
                    StatusText = "未找到部件所属系统，删除失败";
                    return false;
                }

                string targetCode = SelectedTarget.Code;
                string partCode = node.Part.PartCode;
                await RunRepositoryOperationAsync(() => _targetInfoRepository.DeletePart(targetCode, partCode));

                // 数据库删除成功后再解绑勾选事件，防止节点移除过程中触发无效 Unity 同步。
                DetachCheckStateHandler(node);
                parentNode.System.Parts.Remove(node.Part);
                parentNode.Children.Remove(node);
                SelectStructureNode(parentNode);
                ScheduleCheckedPartUnitySync();
                NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);
                StatusText = $"已删除部件：{node.Name}";
                return true;
            }

            if (node.System != null)
            {
                if (parentNode == null)
                {
                    StatusText = "不能直接删除目标根节点";
                    return false;
                }

                // 删除系统会连带删除其子系统和部件。仓储层负责数据库级联，本地树负责移除根节点即可。
                string targetCode = SelectedTarget.Code;
                string systemCode = node.System.SystemCode;
                await RunRepositoryOperationAsync(() => _targetInfoRepository.DeleteSystem(targetCode, systemCode));

                DetachCheckStateHandler(node);

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
                NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: true);
                StatusText = $"已删除系统：{node.Name}";
                return true;
            }

            StatusText = "不能删除目标根节点";
            return false;
        }
        catch (Exception ex)
        {
            StatusText = $"删除结构节点失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 删除结构节点失败：{ex}");
            return false;
        }
    }
}
