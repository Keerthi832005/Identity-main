using System.Globalization;
using Identity.Application.BulkData;
using Identity.Application.Messaging;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

/// <summary>
/// Creates and updates units through the existing organization commands, so a bulk load keeps the same
/// atomic-creation, path and audit behaviour as a single-record write. Parents are applied before
/// their children regardless of file order, so one workbook can define a whole branch.
/// </summary>
public sealed class OrganizationBulkCommitter(
    IRequestDispatcher dispatcher,
    IOrganizationCodeResolver resolver) : IBulkEntityCommitter
{
    public string EntityKey => OrganizationBulkDescriptor.EntityKey;

    public IReadOnlyList<int> OrderForCommit(IReadOnlyList<BulkCommitCandidate> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var byCode = new Dictionary<string, BulkCommitCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (Read(row.Values, OrganizationBulkDescriptor.UnitCode) is { } code)
            {
                byCode.TryAdd(code, row);
            }
        }

        var ordered = new List<int>(rows.Count);
        var placed = new HashSet<int>();
        var walking = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Place(BulkCommitCandidate row)
        {
            if (!placed.Add(row.SourceRowNumber))
            {
                return;
            }

            var code = Read(row.Values, OrganizationBulkDescriptor.UnitCode);
            var parent = Read(row.Values, OrganizationBulkDescriptor.ParentUnitCode);

            /* The walking set stops a cycle the validator did not reject from recursing forever. */
            if (parent is not null
                && code is not null
                && walking.Add(code)
                && byCode.TryGetValue(parent, out var ancestor))
            {
                Place(ancestor);
            }

            ordered.Add(row.SourceRowNumber);
        }

        foreach (var row in rows)
        {
            Place(row);
        }

        return ordered;
    }

    public async ValueTask<long?> Apply(
        BulkEntityDescriptor descriptor,
        IReadOnlyDictionary<string, string?> values,
        bool isUpdate,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var code = Required(values, OrganizationBulkDescriptor.UnitCode);
        var name = Required(values, OrganizationBulkDescriptor.UnitName);
        var description = Read(values, OrganizationBulkDescriptor.Description);
        var address = Address(values);

        if (!Enum.TryParse<OrganizationUnitType>(
                Read(values, OrganizationBulkDescriptor.UnitType),
                ignoreCase: true,
                out var type) || !Enum.IsDefined(type))
        {
            throw new AdministrationException($"Staged row has an unusable unit type for {code}.");
        }

        if (isUpdate)
        {
            return await UpdateExisting(code, name, type, description, address, values, context, cancellationToken)
                .ConfigureAwait(false);
        }

        if (type == OrganizationUnitType.Organization)
        {
            var created = await dispatcher.Send(
                new CreateOrganizationCommand(code, name, context, description, address),
                cancellationToken);
            return created.OrganizationUnitId;
        }

        var parentCode = Read(values, OrganizationBulkDescriptor.ParentUnitCode)
            ?? throw new AdministrationException($"A {type} needs a parent unit code.");

        /* Resolved at apply time: a parent created earlier in this same commit did not exist when
           the batch was validated. */
        var parents = await resolver
            .FindUnitsByCodes([parentCode], cancellationToken)
            .ConfigureAwait(false);
        if (!parents.TryGetValue(parentCode, out var parent))
        {
            throw new AdministrationException(
                $"Parent unit {parentCode} was not created before its children.");
        }

        var result = await dispatcher.Send(
            new CreateOrganizationUnitCommand(
                parent.OrganizationId,
                parent.OrganizationUnitId,
                type,
                code,
                name,
                context,
                description,
                address),
            cancellationToken);
        return result.OrganizationUnitId;
    }

    private async ValueTask<long> UpdateExisting(
        string code,
        string name,
        OrganizationUnitType type,
        string? description,
        OrganizationUnitAddress address,
        IReadOnlyDictionary<string, string?> values,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        var parentCode = Read(values, OrganizationBulkDescriptor.ParentUnitCode);
        var units = await resolver.FindUnitsByCodes(
            parentCode is null ? [code] : [code, parentCode], cancellationToken).ConfigureAwait(false);
        if (!units.TryGetValue(code, out var existing))
        {
            throw new AdministrationException("The unit to update no longer exists. Review the import again.");
        }

        var current = await dispatcher.Send(
            new GetOrganizationUnitQuery(existing.OrganizationId, existing.OrganizationUnitId, context),
            cancellationToken).ConfigureAwait(false);

        // Bulk updates use the same detail command and concurrency token as the individual editor.
        // A changed parent/type must not silently become a move or a duplicate creation.
        if (current.UnitType != type
            || (type == OrganizationUnitType.Organization
                ? parentCode is not null
                : parentCode is null || !units.TryGetValue(parentCode, out var parent)
                    || parent.OrganizationId != current.OrganizationId
                    || parent.OrganizationUnitId != current.ParentOrganizationUnitId))
        {
            throw new AdministrationException("An import cannot change an existing unit's type or parent.");
        }

        var updated = await dispatcher.Send(new UpdateOrganizationUnitCommand(
            current.OrganizationId, current.OrganizationUnitId, code, name, description, address,
            current.RowVersion, context), cancellationToken).ConfigureAwait(false);
        return updated.OrganizationUnitId;
    }

    private static OrganizationUnitAddress Address(IReadOnlyDictionary<string, string?> values) => new(
        Read(values, OrganizationBulkDescriptor.AddressLine1),
        Read(values, OrganizationBulkDescriptor.AddressLine2),
        Read(values, OrganizationBulkDescriptor.AddressLine3),
        Read(values, OrganizationBulkDescriptor.City),
        Read(values, OrganizationBulkDescriptor.District),
        Read(values, OrganizationBulkDescriptor.StateName),
        Read(values, OrganizationBulkDescriptor.PostalCode),
        Read(values, OrganizationBulkDescriptor.CountryCode),
        Coordinate(values, OrganizationBulkDescriptor.Latitude),
        Coordinate(values, OrganizationBulkDescriptor.Longitude));

    private static decimal? Coordinate(IReadOnlyDictionary<string, string?> values, string columnId) =>
        Read(values, columnId) is { } value
        && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private static string Required(IReadOnlyDictionary<string, string?> values, string columnId) =>
        Read(values, columnId)
        ?? throw new AdministrationException($"Staged row is missing {columnId}.");

    private static string? Read(IReadOnlyDictionary<string, string?> values, string columnId) =>
        values.TryGetValue(columnId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
