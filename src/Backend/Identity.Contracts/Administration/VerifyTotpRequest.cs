using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record VerifyTotpRequest(
    [property: Required, RegularExpression("^[0-9]{6}$")] string Code);
