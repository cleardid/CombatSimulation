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
        private void RefreshSelectedDetailRows(TargetStructureTreeNode? node)
        {
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
                TargetPartInfoItem part = node.Part;

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

                foreach (TargetPartParameterItem parameter in part.Parameters.OrderBy(item => item.Index))
                {
                    SelectedStructureDetailRows.Add(new TargetDetailRow
                    {
                        Label = parameter.Name,
                        Value = parameter.DisplayText
                    });
                }

                if (part.ShapeType == "六面异形体")
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
                else
                {
                    AddRows(
                        SelectedPositionDetailRows,
                        new TargetDetailRow { Label = "中心X坐标", Value = $"{part.CenterX:0.###}" },
                        new TargetDetailRow { Label = "中心Y坐标", Value = $"{part.CenterY:0.###}" },
                        new TargetDetailRow { Label = "中心Z坐标", Value = $"{part.CenterZ:0.###}" });

                    AddRows(
                        SelectedAngleDetailRows,
                        new TargetDetailRow { Label = "X轴方向角度", Value = $"{part.RotationX:0.###}" },
                        new TargetDetailRow { Label = "Y轴方向角度", Value = $"{part.RotationY:0.###}" },
                        new TargetDetailRow { Label = "Z轴方向角度", Value = $"{part.RotationZ:0.###}" });
                }

                return;
            }

            if (node.System != null)
            {
                TargetSystemInfoItem system = node.System;

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

                return;
            }

            if (node.Target != null)
            {
                TargetInfoItem target = node.Target;

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
        }

        private static void AddRows(ObservableCollection<TargetDetailRow> target, params TargetDetailRow[] rows)
        {
            foreach (TargetDetailRow row in rows)
            {
                target.Add(row);
            }
        }
    }
}
