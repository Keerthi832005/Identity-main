using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Identity.Contracts.BulkData;
using Identity.Contracts.Errors;

namespace Identity.Api.Tests;

public sealed class BulkDataApiTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private const string WorkbookContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private HttpClient Administrator()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(true));
        return client;
    }

    private static (HttpMethod Method, string Path)[] Routes =>
    [
        (HttpMethod.Get, "/api/v1/admin/bulk/users/template"),
        (HttpMethod.Post, "/api/v1/admin/bulk/users/export"),
        (HttpMethod.Post, "/api/v1/admin/bulk/users/staging"),
        (HttpMethod.Get, $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}"),
        (HttpMethod.Put, $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}/rows/3"),
        (HttpMethod.Get, $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}/errors"),
        (HttpMethod.Post, $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}/commit"),
        (HttpMethod.Delete, $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}"),
    ];

    [Fact]
    public async Task EveryRouteRequiresAnAuthenticatedAdministrator()
    {
        foreach (var (method, path) in Routes)
        {
            using var client = factory.CreateClient();
            using var anonymous = new HttpRequestMessage(method, path)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            using var unauthenticated = await client.SendAsync(anonymous, Token);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(false));
            using var denied = new HttpRequestMessage(method, path)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            using var forbidden = await client.SendAsync(denied, Token);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
    }

    [Fact]
    public async Task TemplateDownloadIsAWorkbookNamedForItsEntity()
    {
        using var client = Administrator();

        using var response = await client.GetAsync("/api/v1/admin/bulk/users/template", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkbookContentType, response.Content.Headers.ContentType?.MediaType);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(fileName);
        Assert.Contains("users-template-", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", fileName.Trim('"'), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEntityWithoutBulkSupportIsNotFound()
    {
        using var client = Administrator();

        using var response = await client.GetAsync("/api/v1/admin/bulk/devices/template", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Token);
        Assert.Equal("resource_not_found", error?.Code);
    }

    [Fact]
    public async Task PastedRowsStageAndReturnTheBatchLocation()
    {
        using var client = Administrator();
        var request = new BulkPasteRequest(
        [
            new BulkPasteRowRequest(3, new Dictionary<string, string?>
            {
                ["employeeCode"] = "INDE05512",
                ["displayName"] = "Harish Venkat",
            }),
        ]);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/admin/bulk/users/staging", request, Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(
            "/api/v1/admin/bulk/staging/",
            response.Headers.Location?.ToString(),
            StringComparison.Ordinal);
        var batch = await response.Content.ReadFromJsonAsync<BulkBatchResponse>(Token);
        Assert.Equal("users", batch?.EntityKey);
        Assert.Equal("Staged", batch?.State);
    }

    [Fact]
    public async Task ExportReturnsAWorkbookForTheRowsTheCallerIsLookingAt()
    {
        using var client = Administrator();
        var request = new BulkExportRequest(
        [
            new BulkPasteRowRequest(3, new Dictionary<string, string?>
            {
                ["employeeCode"] = "INDE05512",
                ["displayName"] = "Harish Venkat",
            }),
        ]);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/admin/bulk/users/export", request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkbookContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync(Token)).Length > 0);
    }

    [Fact]
    public async Task AnUploadThatIsNotAnXlsxIsRejectedBeforeItIsParsed()
    {
        using var client = Administrator();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("employeeCode,displayName"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "workbook", "people.csv");

        using var response = await client.PostAsync(
            "/api/v1/admin/bulk/users/staging", content, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Token);
        Assert.Equal("bulk_document_rejected", error?.Code);
        Assert.Contains(".xlsx", error?.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyUploadIsRejectedWithoutOpeningIt()
    {
        using var client = Administrator();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([]);
        file.Headers.ContentType = new MediaTypeHeaderValue(WorkbookContentType);
        content.Add(file, "workbook", "people.xlsx");

        using var response = await client.PostAsync(
            "/api/v1/admin/bulk/users/staging", content, Token);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task PreviewPagesStagedRowsWithTheirCellErrors()
    {
        using var client = Administrator();

        using var response = await client.GetAsync(
            $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}?filter=needs-attention",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedBulkRowsResponse>(Token);
        Assert.NotNull(page);
        var row = Assert.Single(page.Items);
        Assert.Equal("Invalid", row.State);
        Assert.Equal("email.invalid", Assert.Single(row.Errors).Code);
    }

    [Fact]
    public async Task AnotherAdministratorsBatchIsNotFoundRatherThanForbidden()
    {
        using var client = Administrator();

        using var response = await client.GetAsync(
            $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.MissingBatchKey:D}", Token);

        /* Not forbidden: a 403 would confirm the batch exists and make keys enumerable. */
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CorrectingARowReturnsItsRevalidatedState()
    {
        using var client = Administrator();
        var request = new BulkCorrectRowRequest(new Dictionary<string, string?>
        {
            ["employeeCode"] = "INDE05513",
            ["email"] = "meera.n@in.fujitec.com",
        });

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}/rows/3", request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await response.Content.ReadFromJsonAsync<BulkStagedRowResponse>(Token);
        Assert.Equal(3, row?.SourceRowNumber);
    }

    [Fact]
    public async Task CommitReportsWhatWasWrittenAndWhatWasLeftBehind()
    {
        using var client = Administrator();

        using var response = await client.PostAsync(
            $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}/commit", null, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkCommitResponse>(Token);
        Assert.Equal(2, result?.CreatedRowCount);
        Assert.Equal(1, result?.UpdatedRowCount);
        Assert.Equal(1, result?.RemainingInvalidRowCount);
        Assert.False(result?.AlreadyCommitted);
    }

    [Fact]
    public async Task DiscardRemovesTheBatchAndReportsTheRowCount()
    {
        using var client = Administrator();

        using var response = await client.DeleteAsync(
            $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkDiscardResponse>(Token);
        Assert.Equal(3, result?.DiscardedRowCount);
    }

    [Fact]
    public async Task AnExportOnlyEntityOffersATemplateButRefusesAnImport()
    {
        using var client = Administrator();

        using var template = await client.GetAsync(
            "/api/v1/admin/bulk/audit-events/template", Token);
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);

        var request = new BulkPasteRequest(
        [
            new BulkPasteRowRequest(3, new Dictionary<string, string?>
            {
                ["eventType"] = "LoginSucceeded",
            }),
        ]);
        using var staging = await client.PostAsJsonAsync(
            "/api/v1/admin/bulk/audit-events/staging", request, Token);

        /* The audit trail is append-only. Refusing here, not at the committer, means no screen can
           make it writable by wiring up a toolbar. */
        Assert.Equal(HttpStatusCode.Conflict, staging.StatusCode);
        var error = await staging.Content.ReadFromJsonAsync<ApiErrorResponse>(Token);
        Assert.Equal("administration_conflict", error?.Code);
    }

    [Fact]
    public async Task AnnotatedErrorWorkbookIsRebuiltFromTheStagedRows()
    {
        using var client = Administrator();

        using var response = await client.GetAsync(
            $"/api/v1/admin/bulk/staging/{FakeRequestDispatcher.BatchKey:D}/errors", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkbookContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync(Token)).Length > 0);
    }
}
