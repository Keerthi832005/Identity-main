using Identity.Application.BulkData;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

/// <summary>
/// Enforces the hierarchy the domain already defines. The allowed parent for each type comes from
/// <see cref="OrganizationUnit.ParentType"/>, so the bulk path cannot widen the rule: adding a type
/// pairing here would require changing the domain, which is where it belongs.
/// </summary>
public sealed class OrganizationBulkValidator(IOrganizationCodeResolver resolver) : IBulkEntityValidator
{
    public const string UnknownType = "unit.type-unknown";
    public const string ParentRequired = "unit.parent-required";
    public const string RootHasParent = "unit.root-has-parent";
    public const string UnknownParent = "unit.parent-unknown";
    public const string WrongParentType = "unit.parent-wrong-type";
    public const string ParentIsSelf = "unit.parent-self";
    public const string ParentCycle = "unit.parent-cycle";

    public string EntityKey => OrganizationBulkDescriptor.EntityKey;

    public async ValueTask<IReadOnlyList<BulkEntityRowVerdict>> Verify(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkRow> rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(rows);

        var codes = rows
            .SelectMany(row => new[]
            {
                Value(row, OrganizationBulkDescriptor.UnitCode),
                Value(row, OrganizationBulkDescriptor.ParentUnitCode),
            })
            .Where(code => code is not null)
            .Select(code => code!)
            .ToArray();
        var existing = await resolver
            .FindUnitsByCodes(codes, cancellationToken)
            .ConfigureAwait(false);

        var batchTypes = TypesInBatch(rows);
        var parents = ParentsInBatch(rows);
        var verdicts = new List<BulkEntityRowVerdict>(rows.Count);

        foreach (var row in rows)
        {
            var errors = new List<BulkCellError>();
            var code = Value(row, OrganizationBulkDescriptor.UnitCode);
            var parentCode = Value(row, OrganizationBulkDescriptor.ParentUnitCode);

            if (!TryParseType(Value(row, OrganizationBulkDescriptor.UnitType), out var type))
            {
                errors.Add(new BulkCellError(
                    row.SourceRowNumber,
                    OrganizationBulkDescriptor.UnitType,
                    UnknownType,
                    $"Unit type must be one of: {string.Join(", ", OrganizationBulkDescriptor.Types)}."));
                verdicts.Add(new BulkEntityRowVerdict(row.SourceRowNumber, false, errors));
                continue;
            }

            var expectedParentType = OrganizationUnit.ParentType(type);
            CheckParent(row, code, parentCode, type, expectedParentType, existing, batchTypes, parents, errors);

            verdicts.Add(new BulkEntityRowVerdict(
                row.SourceRowNumber,
                code is not null && existing.ContainsKey(code),
                errors));
        }

        return verdicts;
    }

    private static void CheckParent(
        BulkRow row,
        string? code,
        string? parentCode,
        OrganizationUnitType type,
        OrganizationUnitType? expectedParentType,
        IReadOnlyDictionary<string, OrganizationUnitRef> existing,
        IReadOnlyDictionary<string, OrganizationUnitType> batchTypes,
        IReadOnlyDictionary<string, string> parents,
        List<BulkCellError> errors)
    {
        if (expectedParentType is null)
        {
            if (parentCode is not null)
            {
                errors.Add(new BulkCellError(
                    row.SourceRowNumber,
                    OrganizationBulkDescriptor.ParentUnitCode,
                    RootHasParent,
                    "An Organization is the root of its hierarchy and cannot have a parent."));
            }

            return;
        }

        if (parentCode is null)
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                OrganizationBulkDescriptor.ParentUnitCode,
                ParentRequired,
                $"A {type} needs a parent, and its parent must be a {expectedParentType}."));
            return;
        }

        if (code is not null && string.Equals(code, parentCode, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                OrganizationBulkDescriptor.ParentUnitCode,
                ParentIsSelf,
                "A unit cannot be its own parent."));
            return;
        }

        /* A parent may be defined by another row in this same file, so both sources count. */
        OrganizationUnitType? actualParentType = existing.TryGetValue(parentCode, out var stored)
            ? stored.UnitType
            : batchTypes.TryGetValue(parentCode, out var staged)
                ? staged
                : null;

        if (actualParentType is null)
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                OrganizationBulkDescriptor.ParentUnitCode,
                UnknownParent,
                $"No unit with code {parentCode}. Add it as another row in this file, or correct the code."));
            return;
        }

        if (actualParentType != expectedParentType)
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                OrganizationBulkDescriptor.ParentUnitCode,
                WrongParentType,
                $"A {type} must sit under a {expectedParentType}, but {parentCode} is a {actualParentType}."));
            return;
        }

        if (code is not null && FormsCycle(code, parents))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                OrganizationBulkDescriptor.ParentUnitCode,
                ParentCycle,
                "These units reference each other in a loop. A hierarchy cannot contain a cycle."));
        }
    }

    private static bool TryParseType(string? value, out OrganizationUnitType type) =>
        Enum.TryParse(value, ignoreCase: true, out type)
        && OrganizationBulkDescriptor.Types.Contains(
            type.ToString(),
            StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, OrganizationUnitType> TypesInBatch(IReadOnlyList<BulkRow> rows)
    {
        var types = new Dictionary<string, OrganizationUnitType>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Value(row, OrganizationBulkDescriptor.UnitCode);
            if (code is null || types.ContainsKey(code)) continue;
            if (TryParseType(Value(row, OrganizationBulkDescriptor.UnitType), out var type))
            {
                types[code] = type;
            }
        }

        return types;
    }

    private static Dictionary<string, string> ParentsInBatch(IReadOnlyList<BulkRow> rows)
    {
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = Value(row, OrganizationBulkDescriptor.UnitCode);
            if (code is null || parents.ContainsKey(code)) continue;
            parents[code] = Value(row, OrganizationBulkDescriptor.ParentUnitCode) ?? string.Empty;
        }

        return parents;
    }

    private static bool FormsCycle(string code, IReadOnlyDictionary<string, string> parents)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = code;
        while (parents.TryGetValue(current, out var parent) && parent.Length > 0)
        {
            if (!seen.Add(current)) return true;
            if (string.Equals(parent, code, StringComparison.OrdinalIgnoreCase)) return true;
            current = parent;
        }

        return false;
    }

    private static string? Value(BulkRow row, string columnId) =>
        row.Cells.FirstOrDefault(cell => cell.ColumnId == columnId)?.Value is { } value
        && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
