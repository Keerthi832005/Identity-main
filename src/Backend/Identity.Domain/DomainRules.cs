namespace Identity.Domain;

internal static class DomainRules
{
    public static string Required(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Value cannot exceed {maximumLength} characters.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, parameterName, maximumLength);
    }

    public static long Positive(long value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be positive.");
        }

        return value;
    }

    public static byte[] Hash(byte[] value, string parameterName, int requiredLength)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length != requiredLength)
        {
            throw new ArgumentException($"Value must be {requiredLength} bytes.", parameterName);
        }

        return [.. value];
    }
}
