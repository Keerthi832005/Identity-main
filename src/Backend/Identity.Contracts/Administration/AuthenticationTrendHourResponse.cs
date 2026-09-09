namespace Identity.Contracts.Administration;

public sealed record AuthenticationTrendHourResponse(
    DateTime Hour,
    int Succeeded,
    int Failed);
