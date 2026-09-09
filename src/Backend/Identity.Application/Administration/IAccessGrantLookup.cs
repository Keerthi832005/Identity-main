namespace Identity.Application.Administration;

/// <summary>
/// The two existence checks access-grant validation needs. Narrow like the other bulk lookup ports,
/// so a validator can be tested without standing in for the whole administration store.
/// </summary>
public interface IAccessGrantLookup
{
    ValueTask<bool> HasApplicationGrant(
        long userId,
        long applicationId,
        CancellationToken cancellationToken);

    ValueTask<bool> HasRoleAssignment(
        long userId,
        long applicationId,
        long roleId,
        CancellationToken cancellationToken);
}
