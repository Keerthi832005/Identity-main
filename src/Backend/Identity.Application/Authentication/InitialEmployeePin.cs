namespace Identity.Application.Authentication;

public static class InitialEmployeePin
{
    // Preserve leading zeroes; never invent a PIN for a non-numeric suffix.
    public static string? FromEmployeeCode(string employeeCode)
    {
        var code = employeeCode.Trim();
        return code.Length >= 4 && code[^4..].All(c => c is >= '0' and <= '9')
            ? code[^4..] : null;
    }
}
