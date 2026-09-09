using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Authentication;

public sealed record BrowserRefreshRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string ClientId);
