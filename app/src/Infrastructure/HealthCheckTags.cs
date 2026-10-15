namespace Infrastructure;

public static class HealthCheckTags
{
    // Checks that must pass before the app can receive traffic (readiness probe).
    public const string Ready = "ready";
}
