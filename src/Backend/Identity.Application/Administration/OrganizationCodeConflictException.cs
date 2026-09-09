namespace Identity.Application.Administration;

public sealed class OrganizationCodeConflictException() : InvalidOperationException("This code is already used in the organization, including other unit types.");
