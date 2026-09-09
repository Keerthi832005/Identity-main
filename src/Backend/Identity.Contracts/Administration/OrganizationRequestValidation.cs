using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

internal static class OrganizationRequestValidation
{
    public static IEnumerable<ValidationResult> Address(OrganizationAddressData? address)
    {
        if (address is null) yield break;
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(address, new ValidationContext(address), results, validateAllProperties: true);
        foreach (var result in results)
            yield return new ValidationResult(result.ErrorMessage, result.MemberNames.Select(name => "Address." + name));
    }
}
