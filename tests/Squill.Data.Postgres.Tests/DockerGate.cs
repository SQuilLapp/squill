namespace Squill.Data.Postgres.Tests;

/// <summary>Gate for tests that need Docker: they run only when <c>SQUILL_DOCKER_TESTS=1</c>.</summary>
public static class DockerGate
{
    public const string SkipReason = "Set SQUILL_DOCKER_TESTS=1 to run the Docker tests.";

    public static bool Enabled => Environment.GetEnvironmentVariable("SQUILL_DOCKER_TESTS") == "1";
}
