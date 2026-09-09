using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record SetPinRequest(
    [property: Required, RegularExpression("^[0-9]{4}$")] string Pin);
