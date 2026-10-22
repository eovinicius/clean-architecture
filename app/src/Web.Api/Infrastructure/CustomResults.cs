using SharedKernel;

namespace Web.Api.Infrastructure;

public static class CustomResults
{
    public static IResult Problem(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result can't be converted into a problem.");
        }

        return Results.Problem(
            title: GetTitle(result.Error),
            detail: GetDetail(result.Error),
            type: GetType(result.Error.Type),
            statusCode: GetStatusCode(result.Error.Type),
            extensions: GetErrors(result));

        // Only expected client errors expose their code and description; anything else fails closed.
        static bool IsClientError(ErrorType errorType) =>
            errorType is ErrorType.Validation
                or ErrorType.Problem
                or ErrorType.NotFound
                or ErrorType.Conflict
                or ErrorType.Forbidden
                or ErrorType.Unauthorized;

        static string GetTitle(Error error) =>
            IsClientError(error.Type) ? error.Code : "Server failure";

        static string GetDetail(Error error) =>
            IsClientError(error.Type) ? error.Description : "An unexpected error occurred";

        static string GetType(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                ErrorType.Problem => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                ErrorType.Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                ErrorType.NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                ErrorType.Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            };

        static int GetStatusCode(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation or ErrorType.Problem => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

        static Dictionary<string, object?>? GetErrors(Result result)
        {
            if (result.Error is not ValidationError validationError)
            {
                return null;
            }

            return new Dictionary<string, object?>
            {
                { "errors", validationError.Errors }
            };
        }
    }
}
