using CombatSimulation.Models;

namespace CombatSimulation.Services.Targets;

/// <summary>
/// 目标结构编辑领域服务，负责草稿、唯一标识校验和模型属性回写。
/// </summary>
public sealed class TargetStructureEditor
{
    public TargetInfoItem CreateTargetDraft(IEnumerable<TargetInfoItem> existingTargets)
    {
        ArgumentNullException.ThrowIfNull(existingTargets);
        return new TargetInfoItem
        {
            Code = CreateUniqueTargetCode(existingTargets),
            Name = "新建目标",
            Category = "未分类",
            Description = string.Empty
        };
    }

    public TargetSystemInfoItem CreateSystemDraft(
        TargetStructureTreeNode parentNode,
        TargetInfoItem? selectedTarget)
    {
        ArgumentNullException.ThrowIfNull(parentNode);

        bool isTopSystem = parentNode.Target != null;
        string targetCode = parentNode.Target?.Code
            ?? parentNode.System?.TargetCode
            ?? selectedTarget?.Code
            ?? string.Empty;
        string parentSystemCode = isTopSystem
            ? "-1"
            : parentNode.System?.SystemCode ?? "-1";

        return new TargetSystemInfoItem
        {
            SystemCode = CreateUniqueSystemCode(selectedTarget),
            SystemName = isTopSystem ? "新建顶层系统" : "新建子系统",
            SystemDescription = string.Empty,
            IsTopSystem = isTopSystem,
            ParentSystemCode = parentSystemCode,
            TargetCode = targetCode
        };
    }

    public TargetPartInfoItem CreatePartDraft(
        TargetStructureTreeNode parentNode,
        TargetInfoItem? selectedTarget)
    {
        ArgumentNullException.ThrowIfNull(parentNode);
        return new TargetPartInfoItem
        {
            PartCode = CreateUniquePartCode(selectedTarget),
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

    public TargetStructureValidationResult NormalizeAndValidateTarget(
        TargetInfoItem target,
        IEnumerable<TargetInfoItem> existingTargets,
        TargetInfoItem? except)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(existingTargets);

        target.Name = target.Name.Trim();
        target.Category = target.Category.Trim();
        target.Description = target.Description.Trim();
        target.Code = target.Code.Trim();

        if (string.IsNullOrWhiteSpace(target.Name))
        {
            return TargetStructureValidationResult.Failure("请输入目标名称");
        }

        if (string.IsNullOrWhiteSpace(target.Category))
        {
            return TargetStructureValidationResult.Failure("请输入目标种类");
        }

        if (string.IsNullOrWhiteSpace(target.Code))
        {
            target.Code = CreateUniqueTargetCode(existingTargets);
        }

        bool codeUsed = existingTargets.Any(existingTarget =>
            !ReferenceEquals(existingTarget, except)
            && string.Equals(existingTarget.Code, target.Code, StringComparison.Ordinal));
        return codeUsed
            ? TargetStructureValidationResult.Failure($"目标唯一标识已存在：{target.Code}")
            : TargetStructureValidationResult.Success;
    }

    public bool IsSystemCodeUsed(
        TargetInfoItem? target,
        string systemCode,
        TargetSystemInfoItem? except)
    {
        if (target == null || string.IsNullOrWhiteSpace(systemCode))
        {
            return false;
        }

        return EnumerateSystems(target.Systems).Any(system =>
            !ReferenceEquals(system, except)
            && string.Equals(system.SystemCode, systemCode, StringComparison.Ordinal));
    }

    public bool IsPartCodeUsed(
        TargetInfoItem? target,
        string partCode,
        TargetPartInfoItem? except)
    {
        if (target == null || string.IsNullOrWhiteSpace(partCode))
        {
            return false;
        }

        return EnumerateSystems(target.Systems)
            .SelectMany(system => system.Parts)
            .Any(part =>
                !ReferenceEquals(part, except)
                && string.Equals(part.PartCode, partCode, StringComparison.Ordinal));
    }

    public void ApplySystemUpdate(
        TargetSystemInfoItem target,
        TargetSystemInfoItem source,
        string originalSystemCode)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        target.SystemName = source.SystemName;
        target.SystemCode = source.SystemCode;
        target.SystemDescription = source.SystemDescription;
        target.IsTopSystem = source.IsTopSystem;
        target.ParentSystemCode = source.ParentSystemCode;
        target.TargetCode = source.TargetCode;

        if (string.Equals(originalSystemCode, target.SystemCode, StringComparison.Ordinal))
        {
            return;
        }

        foreach (TargetPartInfoItem part in target.Parts)
        {
            if (string.Equals(part.SystemCode, originalSystemCode, StringComparison.Ordinal))
            {
                part.SystemCode = target.SystemCode;
            }
        }

        foreach (TargetSystemInfoItem childSystem in target.ChildSystems)
        {
            if (string.Equals(childSystem.ParentSystemCode, originalSystemCode, StringComparison.Ordinal))
            {
                childSystem.ParentSystemCode = target.SystemCode;
            }
        }
    }

    public void ApplyPartUpdate(TargetPartInfoItem target, TargetPartInfoItem source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        target.PartCode = source.PartCode;
        target.PartName = source.PartName;
        target.ShapeType = source.ShapeType;
        target.PartDescription = source.PartDescription;
        target.MaterialId = source.MaterialId;
        target.DisplayColor = source.DisplayColor;
        target.EquivalentThickness = source.EquivalentThickness;
        target.VulnerableArea = source.VulnerableArea;
        target.SystemCode = source.SystemCode;
        target.CenterX = source.CenterX;
        target.CenterY = source.CenterY;
        target.CenterZ = source.CenterZ;
        target.RotationX = source.RotationX;
        target.RotationY = source.RotationY;
        target.RotationZ = source.RotationZ;

        target.Parameters.Clear();
        foreach (TargetPartParameterItem parameter in source.Parameters.OrderBy(item => item.Index))
        {
            target.Parameters.Add(new TargetPartParameterItem
            {
                Index = parameter.Index,
                Name = parameter.Name,
                Value = parameter.Value,
                Unit = parameter.Unit
            });
        }
    }

    private string CreateUniqueSystemCode(TargetInfoItem? target)
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N");
        }
        while (IsSystemCodeUsed(target, code, except: null));

        return code;
    }

    private string CreateUniquePartCode(TargetInfoItem? target)
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N");
        }
        while (IsPartCodeUsed(target, code, except: null));

        return code;
    }

    private static string CreateUniqueTargetCode(IEnumerable<TargetInfoItem> existingTargets)
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N");
        }
        while (existingTargets.Any(target =>
            string.Equals(target.Code, code, StringComparison.Ordinal)));

        return code;
    }

    private static IEnumerable<TargetSystemInfoItem> EnumerateSystems(
        IEnumerable<TargetSystemInfoItem> systems)
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
}

public readonly record struct TargetStructureValidationResult(bool IsValid, string ErrorMessage)
{
    public static TargetStructureValidationResult Success { get; } = new(true, string.Empty);

    public static TargetStructureValidationResult Failure(string errorMessage) =>
        new(false, errorMessage);
}
