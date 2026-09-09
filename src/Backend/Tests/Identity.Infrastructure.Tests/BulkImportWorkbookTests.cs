using Identity.Application.Administration;
using Identity.Application.BulkData;
using Identity.Infrastructure.Documents;

namespace Identity.Infrastructure.Tests;

/// <summary>
/// Reads the workbooks shipped in data/bulk-import through the real reader, so a file that would be
/// refused on upload fails here instead of in front of an administrator.
/// </summary>
public sealed class BulkImportWorkbookTests
{
    private static readonly string? Folder = FindFolder();

    [Theory]
    [InlineData("organization-units.xlsx", 102)]
    [InlineData("users.xlsx", 1032)]
    public void Shipped_workbook_reads_cleanly(string fileName, int expectedRows)
    {
        Assert.NotNull(Folder);

        var descriptor = fileName.StartsWith("organization", StringComparison.Ordinal)
            ? OrganizationBulkDescriptor.Descriptor
            : UsersBulkDescriptor.Descriptor;

        using var stream = File.OpenRead(Path.Combine(Folder, fileName));
        var result = new ExcelWorkbookReader().Read(stream, descriptor, BulkDocumentLimits.Default);

        Assert.Equal(descriptor.EntityKey, result.EntityKey);
        Assert.Equal(descriptor.TemplateVersion, result.TemplateVersion);
        Assert.Equal(expectedRows, result.Rows.Count);

        var malformed = result.Rows
            .SelectMany(row => row.Cells.Select(cell => (row.SourceRowNumber, cell)))
            .Where(entry => entry.cell.Malformed)
            .ToArray();
        Assert.Empty(malformed);

        foreach (var required in descriptor.Columns.Where(column => column.Required))
        {
            Assert.All(result.Rows, row => Assert.False(string.IsNullOrWhiteSpace(
                row.Cells.Single(cell => cell.ColumnId == required.ColumnId).Value)));
        }
    }

    private static string? FindFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "bulk-import");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
