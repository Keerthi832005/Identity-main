namespace Identity.Application.Administration;

public sealed class AdministrationException(string message) : InvalidOperationException(message);
