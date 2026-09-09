using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Authentication;

public sealed record CompleteMfaRequest(
    Guid MfaChallengeId,
    [property: Required, RegularExpression("^[0-9]{6}$")] string Code);
