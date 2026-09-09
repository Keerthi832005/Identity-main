namespace Identity.Application.Authentication;

// Deliberately limited pre-login lookup. Never return the administration user model here.
public sealed record TerminalEmployeeSuggestion(string EmployeeCode, string DisplayName);

public interface ITerminalEmployeeDirectory
{
    Task<IReadOnlyList<TerminalEmployeeSuggestion>> Search(
        string clientId, string search, CancellationToken cancellationToken);
}
