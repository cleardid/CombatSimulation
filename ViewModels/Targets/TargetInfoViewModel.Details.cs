using CombatSimulation.Models;
using System.Collections.ObjectModel;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// 根据当前选中的目标、系统或部件节点刷新右侧四组详情信息。
    /// </summary>
    private void RefreshSelectedDetailRows(TargetStructureTreeNode? node)
    {
        // 详情区域使用 ObservableCollection 绑定。先清空再添加，可以让界面立即刷新。
        SelectedBasicDetailRows.Clear();
        SelectedStructureDetailRows.Clear();
        SelectedPositionDetailRows.Clear();
        SelectedAngleDetailRows.Clear();

        if (node == null)
        {
            AddRows(SelectedBasicDetailRows, new TargetDetailRow { Label = "当前节点", Value = "未选择" });
            AddRows(SelectedStructureDetailRows, new TargetDetailRow { Label = "结构信息", Value = "-" });
            AddRows(SelectedPositionDetailRows, new TargetDetailRow { Label = "中心X坐标", Value = "-" });
            AddRows(SelectedAngleDetailRows, new TargetDetailRow { Label = "X轴方向角度", Value = "-" });
            return;
        }

        if (node.Part != null)
        {
            RefreshPartDetailRows(node.Part);
            return;
        }

        if (node.System != null)
        {
            RefreshSystemDetailRows(node.System);
            return;
        }

        if (node.Target != null)
        {
            RefreshTargetDetailRows(node.Target);
        }
    }

    /// <summary>
    /// 刷新部件节点详情。
    /// </summary>
    private void RefreshPartDetailRows(TargetPartInfoItem part)
    {
        AddRows(
            SelectedBasicDetailRows,
            new TargetDetailRow { Label = "部件名称", Value = part.PartName },
            new TargetDetailRow { Label = "所属系统ID", Value = part.SystemCode },
            new TargetDetailRow { Label = "部件材料ID", Value = part.MaterialId },
            new TargetDetailRow { Label = "显示颜色", Value = part.DisplayColor },
            new TargetDetailRow { Label = "易损面积", Value = $"{part.VulnerableArea:0.###} m²" });

        AddRows(
            SelectedStructureDetailRows,
            new TargetDetailRow { Label = "部件形状", Value = part.ShapeType },
            new TargetDetailRow { Label = "等效厚度", Value = $"{part.EquivalentThickness:0.###} mm" });

        // 结构参数按形状定义读取。普通形状显示尺寸参数，六面异形体显示 24 个顶点坐标。
        foreach (TargetPartParameterDefinition definition in TargetPartShapeParameterDefinitions.GetStructureParameterDefinitions(part.ShapeType))
        {
            TargetPartParameterItem? parameter = part.Parameters.FirstOrDefault(item => item.Index == definition.Index);
            SelectedStructureDetailRows.Add(new TargetDetailRow
            {
                Label = definition.Name,
                Value = FormatPartParameterValue(parameter?.Value ?? 0, definition.Unit)
            });
        }

        if (TargetPartShapeParameterDefinitions.IsHexahedronShape(part.ShapeType))
        {
            // 六面异形体的 24 个参数已经是八个点坐标，不再单独展示中心和旋转角。
            AddEmptyPositionAndAngleRows();
        }
        else
        {
            AddRows(
                SelectedPositionDetailRows,
                new TargetDetailRow { Label = "中心X坐标", Value = $"{part.CenterX:0.###} mm" },
                new TargetDetailRow { Label = "中心Y坐标", Value = $"{part.CenterY:0.###} mm" },
                new TargetDetailRow { Label = "中心Z坐标", Value = $"{part.CenterZ:0.###} mm" });

            AddRows(
                SelectedAngleDetailRows,
                new TargetDetailRow { Label = "X轴方向角度", Value = $"{part.RotationX:0.###} °" },
                new TargetDetailRow { Label = "Y轴方向角度", Value = $"{part.RotationY:0.###} °" },
                new TargetDetailRow { Label = "Z轴方向角度", Value = $"{part.RotationZ:0.###} °" });
        }
    }

    /// <summary>
    /// 刷新系统节点详情。
    /// </summary>
    private void RefreshSystemDetailRows(TargetSystemInfoItem system)
    {
        AddRows(
            SelectedBasicDetailRows,
            new TargetDetailRow { Label = "系统名称", Value = system.SystemName },
            new TargetDetailRow { Label = "系统唯一标识", Value = system.SystemCode },
            new TargetDetailRow { Label = "顶系统", Value = system.IsTopSystem ? "是" : "否" },
            new TargetDetailRow { Label = "父系统", Value = system.ParentSystemCode });

        AddRows(
            SelectedStructureDetailRows,
            new TargetDetailRow { Label = "所属目标", Value = system.TargetCode },
            new TargetDetailRow { Label = "子系统数量", Value = CountChildSystems(system).ToString() },
            new TargetDetailRow { Label = "直属部件数量", Value = system.Parts.Count.ToString() });

        AddEmptyPositionAndAngleRows();
    }

    /// <summary>
    /// 刷新目标根节点详情。
    /// </summary>
    private void RefreshTargetDetailRows(TargetInfoItem target)
    {
        AddRows(
            SelectedBasicDetailRows,
            new TargetDetailRow { Label = "目标名称", Value = target.Name },
            new TargetDetailRow { Label = "唯一标识Code", Value = target.Code },
            new TargetDetailRow { Label = "种类", Value = target.Category },
            new TargetDetailRow { Label = "描述", Value = target.Description });

        AddRows(
            SelectedStructureDetailRows,
            new TargetDetailRow { Label = "顶系统数量", Value = target.Systems.Count(system => system.IsTopSystem).ToString() },
            new TargetDetailRow { Label = "系统总数", Value = CountSystems(target).ToString() },
            new TargetDetailRow { Label = "部件总数", Value = CountParts(target).ToString() });

        AddEmptyPositionAndAngleRows();
    }

    /// <summary>
    /// 为非部件节点或六面异形体添加空的位置和角度占位行。
    /// </summary>
    private void AddEmptyPositionAndAngleRows()
    {
        AddRows(
            SelectedPositionDetailRows,
            new TargetDetailRow { Label = "中心X坐标", Value = "-" },
            new TargetDetailRow { Label = "中心Y坐标", Value = "-" },
            new TargetDetailRow { Label = "中心Z坐标", Value = "-" });

        AddRows(
            SelectedAngleDetailRows,
            new TargetDetailRow { Label = "X轴方向角度", Value = "-" },
            new TargetDetailRow { Label = "Y轴方向角度", Value = "-" },
            new TargetDetailRow { Label = "Z轴方向角度", Value = "-" });
    }

    /// <summary>
    /// 格式化部件参数值。
    /// </summary>
    private static string FormatPartParameterValue(double value, string unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? $"{value:0.###}" : $"{value:0.###} {unit}";
    }

    /// <summary>
    /// 批量向详情集合中添加行。
    /// </summary>
    private static void AddRows(ObservableCollection<TargetDetailRow> target, params TargetDetailRow[] rows)
    {
        foreach (TargetDetailRow row in rows)
        {
            target.Add(row);
        }
    }
}
