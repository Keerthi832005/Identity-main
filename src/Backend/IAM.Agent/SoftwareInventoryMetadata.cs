using System.Globalization;

namespace IAM.Agent;

public static class SoftwareInventoryMetadata
{
    // Windows uninstall registration supplies a calendar date, not a timestamp.
    // Servicing or repair can replace the original installation date.
    public static DateOnly? ReadInstallDate(object? value) =>
        value is string text && DateOnly.TryParseExact(text, "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    // EstimatedSize is a DWORD in KiB. Registry APIs expose DWORDs as signed Int32.
    public static long? ReadEstimatedSizeBytes(object? value) => value switch
    {
        int size => (long)unchecked((uint)size) * 1024,
        uint size => (long)size * 1024,
        _ => null,
    };
}
