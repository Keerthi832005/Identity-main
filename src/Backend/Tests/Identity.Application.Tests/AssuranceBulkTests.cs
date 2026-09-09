using Identity.Application.Administration;
using Identity.Application.BulkData;

namespace Identity.Application.Tests;

public sealed class AssuranceBulkTests
{
    [Fact]
    public void TheAuditTrailIsExportOnly()
    {
        Assert.False(AssuranceBulkDescriptors.AuditEvents.AllowsImport);
    }

    [Fact]
    public void SessionsAreExportOnly()
    {
        Assert.False(AssuranceBulkDescriptors.Sessions.AllowsImport);
    }

    [Fact]
    public void EveryWritableEntityStillAllowsImport()
    {
        /* Guards the default: a new descriptor is importable unless it says otherwise, so this
           catches an accidental AllowsImport: false on an entity that needs to be writable. */
        BulkEntityDescriptor[] writable =
        [
            UsersBulkDescriptor.Descriptor,
            CatalogBulkDescriptors.Modules,
            CatalogBulkDescriptors.Capabilities,
            CatalogBulkDescriptors.Roles,
            OrganizationBulkDescriptor.Descriptor,
        ];

        Assert.All(writable, descriptor => Assert.True(descriptor.AllowsImport));
    }

    [Fact]
    public void NoAssuranceTemplateExposesSecretMaterial()
    {
        var forbidden = new[]
        {
            "password", "pin", "secret", "otp", "totp", "token hash", "refresh",
        };
        BulkEntityDescriptor[] descriptors =
        [
            AssuranceBulkDescriptors.AuditEvents,
            AssuranceBulkDescriptors.Sessions,
        ];

        foreach (var column in descriptors.SelectMany(descriptor => descriptor.Columns))
        {
            Assert.DoesNotContain(
                forbidden,
                word => column.ColumnId.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || column.Header.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AnExportOnlyDescriptorStillCarriesUsableColumns()
    {
        /* Export-only must not mean unusable: an auditor needs the correlation id to trace an event
           back to a request. */
        Assert.Contains(
            AssuranceBulkDescriptors.AuditEvents.Columns,
            column => column.ColumnId == "correlationId");
        Assert.NotEmpty(AssuranceBulkDescriptors.Sessions.Columns);
    }
}
