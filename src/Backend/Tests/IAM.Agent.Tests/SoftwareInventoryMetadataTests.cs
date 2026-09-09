namespace IAM.Agent.Tests;

public sealed class SoftwareInventoryMetadataTests
{
    [Theory]
    [InlineData("20260831", 2026, 8, 31)]
    [InlineData("20240229", 2024, 2, 29)]
    public void Reads_only_reported_calendar_dates(string value, int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), SoftwareInventoryMetadata.ReadInstallDate(value));

    [Theory]
    [InlineData(null)]
    [InlineData(20260831)]
    [InlineData("")]
    [InlineData("20230229")]
    [InlineData("2026-08-31")]
    [InlineData("20260831123000")]
    [InlineData(" 20260831")]
    public void Does_not_invent_missing_or_malformed_install_dates(object? value) =>
        Assert.Null(SoftwareInventoryMetadata.ReadInstallDate(value));

    [Fact]
    public void Converts_unsigned_registry_kib_to_bytes_without_overflow()
    {
        Assert.Equal(1024L, SoftwareInventoryMetadata.ReadEstimatedSizeBytes(1));
        Assert.Equal(0L, SoftwareInventoryMetadata.ReadEstimatedSizeBytes(0));
        Assert.Equal(4_398_046_510_080L, SoftwareInventoryMetadata.ReadEstimatedSizeBytes(-1));
        Assert.Null(SoftwareInventoryMetadata.ReadEstimatedSizeBytes(null));
        Assert.Null(SoftwareInventoryMetadata.ReadEstimatedSizeBytes("1024"));
        Assert.Null(SoftwareInventoryMetadata.ReadEstimatedSizeBytes(-1L));
    }
}
