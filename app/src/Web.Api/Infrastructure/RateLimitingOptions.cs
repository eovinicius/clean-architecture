using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Web.Api.Infrastructure;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [ValidateObjectMembers]
    public FixedWindowSettings Global { get; init; } = new() { PermitLimit = 100, WindowInSeconds = 60 };

    [ValidateObjectMembers]
    public FixedWindowSettings Authentication { get; init; } = new() { PermitLimit = 10, WindowInSeconds = 60 };
}

internal sealed class FixedWindowSettings
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int WindowInSeconds { get; init; }
}
