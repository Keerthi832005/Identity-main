using Identity.Application.BulkData;
using Identity.Infrastructure.Documents;

namespace Identity.Infrastructure.Tests;

public sealed class ExcelWorkbookTests
{
    private static readonly BulkEntityDescriptor Users = new(
        "users",
        "Users",
        1,
        [
            new BulkColumnDefinition(
                "employeeCode",
                "Employee code",
                BulkColumnType.Text,
                Required: true,
                HelpText: "The employee code used to sign in."),
            new BulkColumnDefinition("displayName", "Display name", BulkColumnType.Text, true),
            new BulkColumnDefinition("email", "Contact email", BulkColumnType.Text, false),
            new BulkColumnDefinition(
                "managerEmployeeCode",
                "Manager employee code",
                BulkColumnType.Text,
                Required: false,
                ReferenceListKey: "managers"),
            new BulkColumnDefinition(
                "isActive",
                "Active",
                BulkColumnType.Boolean,
                Required: true,
                AllowedValues: ["Yes", "No"]),
            new BulkColumnDefinition("startedOn", "Started on", BulkColumnType.Date, false),
            new BulkColumnDefinition("seatCount", "Seats", BulkColumnType.Number, false),
        ],
        "Fill one user per row. Do not change the header row.");

    private static readonly BulkReferenceList Managers =
        new("managers", ["INDE02904", "INDE03275"]);

    private static BulkExportRow Row(
        string code,
        string name,
        string? email = null,
        string? manager = null,
        string active = "Yes",
        string? started = null,
        string? seats = null) =>
        new(new Dictionary<string, string?>
        {
            ["employeeCode"] = code,
            ["displayName"] = name,
            ["email"] = email,
            ["managerEmployeeCode"] = manager,
            ["isActive"] = active,
            ["startedOn"] = started,
            ["seatCount"] = seats,
        });

    private static string? Cell(BulkRow row, string columnId) =>
        row.Cells.Single(cell => cell.ColumnId == columnId).Value;

    [Fact]
    public void Template_IsReadableAndCarriesNoRows()
    {
        using var stream = new ExcelWorkbookWriter().CreateTemplate(Users, [Managers]);

        var result = new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default);

        Assert.Equal("users", result.EntityKey);
        Assert.Equal(1, result.TemplateVersion);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Export_RoundTripsEveryValue()
    {
        var rows = new[]
        {
            Row("INDE05512", "Harish Venkat", "harish.v@in.fujitec.com", "INDE02904", "Yes", null, "3"),
            Row("INDE05513", "Meera Nair", null, "INDE03275", "No"),
        };

        using var stream = new ExcelWorkbookWriter().CreateExport(Users, [Managers], rows);
        var result = new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("INDE05512", Cell(result.Rows[0], "employeeCode"));
        Assert.Equal("Harish Venkat", Cell(result.Rows[0], "displayName"));
        Assert.Equal("harish.v@in.fujitec.com", Cell(result.Rows[0], "email"));
        Assert.Equal("true", Cell(result.Rows[0], "isActive"));
        Assert.Equal("3", Cell(result.Rows[0], "seatCount"));
        Assert.Equal("false", Cell(result.Rows[1], "isActive"));
        Assert.Null(Cell(result.Rows[1], "email"));
    }

    [Fact]
    public void Export_QuotesRowNumbersExcelWouldShow()
    {
        using var stream = new ExcelWorkbookWriter()
            .CreateExport(Users, [Managers], [Row("INDE05512", "Harish Venkat")]);

        var result = new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default);

        /* Instruction band is row 1 and the header is row 2, so the first record is row 3. */
        Assert.Equal(3, result.Rows[0].SourceRowNumber);
    }

    [Fact]
    public void Reader_AcceptsBooleanAndDateWordsExcelDidNotType()
    {
        var rows = new[] { Row("INDE05512", "Harish Venkat", active: "Y", started: "2026-08-31") };
        using var stream = new ExcelWorkbookWriter().CreateExport(Users, [Managers], rows);

        var result = new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default);

        Assert.Equal("true", Cell(result.Rows[0], "isActive"));
        Assert.StartsWith("2026-08-31", Cell(result.Rows[0], "startedOn"));
    }

    [Fact]
    public void Reader_MarksAValueThatDoesNotFitTheColumnType()
    {
        var rows = new[] { Row("INDE05512", "Harish Venkat", seats: "not a number") };
        using var stream = new ExcelWorkbookWriter().CreateExport(Users, [Managers], rows);

        var result = new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default);

        var cell = result.Rows[0].Cells.Single(candidate => candidate.ColumnId == "seatCount");
        Assert.True(cell.Malformed);
        Assert.Null(cell.Value);
        Assert.Equal("not a number", cell.SourceText);
    }

    [Fact]
    public void Reader_SkipsBlankRowsLeftByEditing()
    {
        var rows = new[]
        {
            Row("INDE05512", "Harish Venkat"),
            new BulkExportRow(new Dictionary<string, string?>()),
            Row("INDE05513", "Meera Nair"),
        };

        using var stream = new ExcelWorkbookWriter().CreateExport(Users, [Managers], rows);
        var result = new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("INDE05513", Cell(result.Rows[1], "employeeCode"));
    }

    [Fact]
    public void Reader_RejectsAWorkbookForADifferentEntity()
    {
        var other = Users with { EntityKey = "organization-units", DisplayName = "Organization units" };
        using var stream = new ExcelWorkbookWriter().CreateTemplate(other, []);

        var exception = Assert.Throws<BulkDocumentException>(
            () => new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default));

        Assert.Equal(BulkDocumentError.EntityMismatch, exception.Error);
    }

    [Fact]
    public void Reader_RejectsASupersededTemplateVersion()
    {
        using var stream = new ExcelWorkbookWriter().CreateTemplate(Users, [Managers]);
        var current = Users with { TemplateVersion = 2 };

        var exception = Assert.Throws<BulkDocumentException>(
            () => new ExcelWorkbookReader().Read(stream, current, BulkDocumentLimits.Default));

        Assert.Equal(BulkDocumentError.TemplateVersionMismatch, exception.Error);
    }

    [Fact]
    public void Reader_RejectsAFileThatIsNotTheTemplate()
    {
        /* DevExpress parses delimited text as a workbook rather than failing, so a stray CSV is
           caught by the metadata check, not by the loader. Either way the upload is refused. */
        using var stream = new MemoryStream("employeeCode,displayName"u8.ToArray());

        var exception = Assert.Throws<BulkDocumentException>(
            () => new ExcelWorkbookReader().Read(stream, Users, BulkDocumentLimits.Default));

        Assert.Equal(BulkDocumentError.MetadataMissing, exception.Error);
        Assert.Contains("Download the template", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reader_RejectsAFileOverTheSizeLimit()
    {
        using var stream = new MemoryStream(new byte[2048]);
        var limits = BulkDocumentLimits.Default with { MaxBytes = 1024 };

        var exception = Assert.Throws<BulkDocumentException>(
            () => new ExcelWorkbookReader().Read(stream, Users, limits));

        Assert.Equal(BulkDocumentError.TooLarge, exception.Error);
    }

    [Fact]
    public void Reader_RejectsMoreRowsThanTheLimitAllows()
    {
        var rows = Enumerable.Range(1, 6)
            .Select(index => Row($"INDE0551{index}", $"Person {index}"))
            .ToArray();
        using var stream = new ExcelWorkbookWriter().CreateExport(Users, [Managers], rows);
        var limits = BulkDocumentLimits.Default with { MaxRows = 4 };

        var exception = Assert.Throws<BulkDocumentException>(
            () => new ExcelWorkbookReader().Read(stream, Users, limits));

        Assert.Equal(BulkDocumentError.TooManyRows, exception.Error);
    }

    [Fact]
    public void Annotator_ProducesAWorkbookThatStillImports()
    {
        var writer = new ExcelWorkbookWriter();
        using var original = writer.CreateExport(
            Users,
            [Managers],
            [Row("INDE05512", "Harish Venkat", "not-an-email")]);

        BulkCellError[] errors =
        [
            new(3, "email", "email.invalid", "Not a valid email address."),
        ];

        using var annotated = new ExcelWorkbookAnnotator().Annotate(original, Users, errors);
        var result = new ExcelWorkbookReader().Read(annotated, Users, BulkDocumentLimits.Default);

        /* Annotation must not corrupt the file: the administrator fixes the cells and re-uploads it. */
        Assert.Single(result.Rows);
        Assert.Equal("INDE05512", Cell(result.Rows[0], "employeeCode"));
    }
}
