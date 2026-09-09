namespace Identity.Application.Authentication;

public sealed record TerminalTokenIdentity(
    long UserId,
    long ApplicationId,
    string ClientId,
    int SecurityVersion,
    int AuthorizationVersion);

public interface ITerminalAccessTokenValidator
{
    TerminalTokenIdentity? Validate(string accessToken, string audience, DateTime now);
}
