using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Validation;

internal sealed class RequestValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().SingleOrDefault();
        if (request is null)
        {
            return Results.BadRequest();
        }

        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty("request")
                .Select(member => new ValidationError(member, result.ErrorMessage ?? "Invalid value.")))
            .GroupBy(error => error.MemberName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal);
        return Results.ValidationProblem(errors);
    }

    private sealed record ValidationError(string MemberName, string Message);
}
