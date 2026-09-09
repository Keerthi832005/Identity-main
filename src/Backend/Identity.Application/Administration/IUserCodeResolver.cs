namespace Identity.Application.Administration;

/// <summary>
/// Resolves employee codes to user ids in one query. Narrow on purpose: bulk validation needs only
/// this, and depending on the whole administration store would make it untestable without a stub of
/// everything else.
/// </summary>
public interface IUserCodeResolver
{
    Task<IReadOnlyDictionary<string, long>> FindUserIdsByEmployeeCodes(
        IReadOnlyCollection<string> employeeCodes,
        CancellationToken cancellationToken);
}
