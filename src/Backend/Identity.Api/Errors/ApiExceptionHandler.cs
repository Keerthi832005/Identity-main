using Identity.Api.Hosting;
using Identity.Application.Administration;
using Identity.Application.BulkData;
using Identity.Contracts.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace Identity.Api.Errors;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = exception switch
        {
            UnauthorizedAccessException => Create(403, "Forbidden", "forbidden", httpContext),
            KeyNotFoundException => Create(404, "Resource was not found", "resource_not_found", httpContext),
            OrganizationConcurrencyException => Create(409, "This unit changed. Reload the latest details before retrying.", "organization_concurrency_conflict", httpContext),
            OrganizationCodeConflictException => Create(409, "This code is already used in this organization, including other unit types.", "organization_code_conflict", httpContext),
            BulkDocumentException document => Create(
                document.Error is BulkDocumentError.TooLarge or BulkDocumentError.TooManyRows
                    or BulkDocumentError.TooManySheets ? 413 : 400,
                document.Message,
                "bulk_document_rejected",
                httpContext),
            AdministrationException administration => Create(
                409, administration.Message, "administration_conflict", httpContext),
            ArgumentException => Create(400, "The request is invalid", "invalid_request", httpContext),
            BadHttpRequestException => Create(400, "The request is invalid", "invalid_request", httpContext),
            _ => Create(500, "An unexpected server error occurred", "server_error", httpContext),
        };
        if (error.Status == 500)
        {
            logger.LogError(
                exception,
                "Unhandled API error type {ExceptionType} for correlation {CorrelationId}.",
                exception.GetType().FullName,
                error.CorrelationId);
        }
        else
        {
            logger.LogWarning(
                "API request rejected with {Code} for correlation {CorrelationId}.",
                error.Code,
                error.CorrelationId);
        }

        httpContext.Response.StatusCode = error.Status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }

    private static ApiErrorResponse Create(
        int status,
        string title,
        string code,
        HttpContext context) => new(
            $"https://identity.local/errors/{code}",
            title,
            status,
            code,
            RequestContextFactory.CorrelationId(context).ToString("D"));
}
