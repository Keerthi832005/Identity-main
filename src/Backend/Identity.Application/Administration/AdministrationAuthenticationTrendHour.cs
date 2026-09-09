namespace Identity.Application.Administration;

public sealed record AdministrationAuthenticationTrendHour(
    DateTime Hour,
    int Succeeded,
    int Failed);
