using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record EnrollTotpRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string MethodName,
    bool IsPrimary);
