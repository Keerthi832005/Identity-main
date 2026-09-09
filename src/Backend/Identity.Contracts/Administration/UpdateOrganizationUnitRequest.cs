using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Identity.Contracts.Administration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateOrganizationUnitRequest(
 [property: Required, MinLength(8), MaxLength(8)] byte[] RowVersion,
 [property: Required, StringLength(50, MinimumLength = 1)] string UnitCode,
 [property: Required, StringLength(200, MinimumLength = 1)] string UnitName,
 [property: StringLength(500)] string? Description,
 [property: Required] OrganizationAddressData? Address) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => OrganizationRequestValidation.Address(Address);
}
