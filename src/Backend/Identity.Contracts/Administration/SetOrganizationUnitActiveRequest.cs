using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Identity.Contracts.Administration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetOrganizationUnitActiveRequest(
 [property: Required] bool? IsActive,
 [property: Required, MinLength(8), MaxLength(8)] byte[] RowVersion);
