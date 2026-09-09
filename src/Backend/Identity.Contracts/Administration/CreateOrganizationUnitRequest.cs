using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Identity.Contracts.Administration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrganizationUnitRequest(
 [property: Required, RegularExpression("^(Country|Region|State|Branch|Location|Department|Team)$")] string UnitType,
 [property: Range(typeof(long), "1", "9223372036854775807")] long ParentOrganizationUnitId,
 [property: Required, StringLength(50, MinimumLength = 1)] string UnitCode,
 [property: Required, StringLength(200, MinimumLength = 1)] string UnitName,
 [property: StringLength(500)] string? Description,
 OrganizationAddressData? Address) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => OrganizationRequestValidation.Address(Address);
}
