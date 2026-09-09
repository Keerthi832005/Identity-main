namespace Identity.Application.Administration;

/// <summary>
/// Batched lookup of organization units by code. The type comes back with the id because the
/// parent-child rule is a type pairing, so knowing the parent exists is not enough.
/// </summary>
public interface IOrganizationCodeResolver
{
    Task<IReadOnlyDictionary<string, OrganizationUnitRef>> FindUnitsByCodes(
        IReadOnlyCollection<string> unitCodes,
        CancellationToken cancellationToken);
}
