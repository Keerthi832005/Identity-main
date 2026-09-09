using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Identity.Contracts.Administration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrganizationAddressData(
 [property: StringLength(250)] string? AddressLine1 = null,
 [property: StringLength(250)] string? AddressLine2 = null,
 [property: StringLength(250)] string? AddressLine3 = null,
 [property: StringLength(100)] string? City = null,
 [property: StringLength(100)] string? District = null,
 [property: StringLength(100)] string? StateName = null,
 [property: StringLength(20)] string? PostalCode = null,
 [property: StringLength(3)] string? CountryCode = null,
 [property: Range(typeof(decimal), "-90", "90")] decimal? Latitude = null,
 [property: Range(typeof(decimal), "-180", "180")] decimal? Longitude = null);
