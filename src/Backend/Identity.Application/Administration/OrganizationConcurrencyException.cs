namespace Identity.Application.Administration;

public sealed class OrganizationConcurrencyException() : InvalidOperationException("This unit has changed. Reload its latest details before retrying.");
