namespace Identity.Domain.Entities;

public sealed record OrganizationUnitAddress(
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? AddressLine3 = null,
    string? City = null,
    string? District = null,
    string? StateName = null,
    string? PostalCode = null,
    string? CountryCode = null,
    decimal? Latitude = null,
    decimal? Longitude = null);
