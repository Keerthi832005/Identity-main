using Identity.Application.BulkData;
using Identity.Domain.Enums;

namespace Identity.Application.Tests;

public sealed class BulkValidationPipelineTests
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
                MaxLength: 50),
            new BulkColumnDefinition("displayName", "Display name", BulkColumnType.Text, true),
            new BulkColumnDefinition("email", "Contact email", BulkColumnType.Text, false),
            new BulkColumnDefinition("seatCount", "Seats", BulkColumnType.Number, false),
            new BulkColumnDefinition(
                "effect",
                "Effect",
                BulkColumnType.Enumeration,
                Required: false,
                AllowedValues: ["Allow", "Deny"]),
            new BulkColumnDefinition(
                "isActive",
                "Active",
                BulkColumnType.Boolean,
                Required: false,
                AllowedValues: ["Yes", "No"]),
        ],
        "One user per row.",
        KeyColumnIds: ["employeeCode"]);

    private static BulkRow Row(int number, params (string Column, string? Value)[] cells) =>
        new(number, [.. cells.Select(cell => new BulkCell(cell.Column, cell.Value, cell.Value, false))]);

    private static BulkRow Valid(int number, string code) =>
        Row(number, ("employeeCode", code), ("displayName", "Someone"));

    private readonly BulkValidationPipeline pipeline = new();

    [Fact]
    public void ValidRowWithNoExistingMatchIsACreate()
    {
        var outcomes = pipeline.Validate(Users, [Valid(3, "INDE05512")], []);

        Assert.Equal(BulkImportRowState.Create, outcomes[0].State);
        Assert.Empty(outcomes[0].Errors);
    }

    [Fact]
    public void ValidRowMatchingAnExistingRecordIsAnUpdate()
    {
        BulkEntityRowVerdict[] verdicts = [new(3, true, [])];

        var outcomes = pipeline.Validate(Users, [Valid(3, "INDE04112")], verdicts);

        Assert.Equal(BulkImportRowState.Update, outcomes[0].State);
    }

    [Fact]
    public void MissingRequiredValueIsReportedAgainstItsOwnColumn()
    {
        var row = Row(3, ("employeeCode", "INDE05512"), ("displayName", "  "));

        var outcomes = pipeline.Validate(Users, [row], []);

        var error = Assert.Single(outcomes[0].Errors);
        Assert.Equal("displayName", error.ColumnId);
        Assert.Equal(BulkErrorCodes.Required, error.Code);
        Assert.Equal(BulkImportRowState.Invalid, outcomes[0].State);
    }

    [Fact]
    public void MalformedValueExplainsTheExpectedFormat()
    {
        BulkRow row = new(3, [
            new BulkCell("employeeCode", "INDE05512", "INDE05512", false),
            new BulkCell("displayName", "Someone", "Someone", false),
            new BulkCell("seatCount", null, "three", true),
        ]);

        var outcomes = pipeline.Validate(Users, [row], []);

        var error = Assert.Single(outcomes[0].Errors);
        Assert.Equal(BulkErrorCodes.Malformed, error.Code);
        Assert.Contains("not a number", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValueOutsideAnEnumerationIsRejectedAndListsWhatIsAllowed()
    {
        var row = Row(3, ("employeeCode", "INDE05512"), ("displayName", "Someone"), ("effect", "Maybe"));

        var outcomes = pipeline.Validate(Users, [row], []);

        var error = Assert.Single(outcomes[0].Errors);
        Assert.Equal(BulkErrorCodes.NotAllowed, error.Code);
        Assert.Contains("Allow, Deny", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BooleanColumnAcceptsTheNormalisedValueRatherThanItsDropdownLabel()
    {
        /* The reader turns Yes into true; checking against the Yes/No labels would fail every row. */
        var row = Row(3, ("employeeCode", "INDE05512"), ("displayName", "Someone"), ("isActive", "true"));

        var outcomes = pipeline.Validate(Users, [row], []);

        Assert.Empty(outcomes[0].Errors);
    }

    [Fact]
    public void ValueLongerThanTheColumnLimitIsRejectedBeforeItReachesSql()
    {
        var row = Row(3, ("employeeCode", new string('X', 51)), ("displayName", "Someone"));

        var outcomes = pipeline.Validate(Users, [row], []);

        var error = Assert.Single(outcomes[0].Errors);
        Assert.Equal(BulkErrorCodes.TooLong, error.Code);
        Assert.Contains("51", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateKeyInsideTheBatchNamesTheRowThatClaimedItFirst()
    {
        BulkRow[] rows = [Valid(3, "INDE05512"), Valid(7, "inde05512")];

        var outcomes = pipeline.Validate(Users, rows, []);

        Assert.Empty(outcomes[0].Errors);
        var error = Assert.Single(outcomes[1].Errors);
        Assert.Equal(BulkErrorCodes.DuplicateInBatch, error.Code);
        Assert.Contains("Row 3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IncompleteKeyIsReportedAsMissingRatherThanDuplicate()
    {
        BulkRow[] rows = [Row(3, ("displayName", "A")), Row(4, ("displayName", "B"))];

        var outcomes = pipeline.Validate(Users, rows, []);

        Assert.All(outcomes, outcome =>
            Assert.DoesNotContain(
                outcome.Errors,
                error => error.Code == BulkErrorCodes.DuplicateInBatch));
        Assert.All(outcomes, outcome =>
            Assert.Contains(outcome.Errors, error => error.Code == BulkErrorCodes.Required));
    }

    [Fact]
    public void EntityVerdictErrorsAreMergedAndForceTheRowInvalid()
    {
        BulkEntityRowVerdict[] verdicts =
        [
            new(3, false, [new BulkCellError(3, "managerEmployeeCode", "manager.unknown", "No such manager.")]),
        ];

        var outcomes = pipeline.Validate(Users, [Valid(3, "INDE05512")], verdicts);

        Assert.Equal(BulkImportRowState.Invalid, outcomes[0].State);
        Assert.Equal("manager.unknown", Assert.Single(outcomes[0].Errors).Code);
    }

    [Fact]
    public void AnExistingMatchStaysInvalidWhenTheRowAlsoFailsValidation()
    {
        BulkEntityRowVerdict[] verdicts = [new(3, true, [])];
        var row = Row(3, ("employeeCode", "INDE04112"), ("displayName", ""));

        var outcomes = pipeline.Validate(Users, [row], verdicts);

        Assert.Equal(BulkImportRowState.Invalid, outcomes[0].State);
    }
}
