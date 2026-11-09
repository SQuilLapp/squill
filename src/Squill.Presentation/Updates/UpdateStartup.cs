namespace Squill.Presentation.Updates;

/// <summary>Start-up switches of the update flow shared by every head.</summary>
public static class UpdateStartup
{
    /// <summary>Gets the name of the variable that turns the start-up update check off (a debug aid for tests and captures).</summary>
    public const string DisableVariable = "SQUILL_NO_UPDATE_CHECK";

    /// <summary>Gets whether the start-up update flow (scheduled check and one-time question) must be skipped.</summary>
    /// <param name="read">Reads an environment variable; <see langword="null"/> uses the process environment.</param>
    /// <returns><see langword="true"/> when the variable is <c>1</c>.</returns>
    public static bool IsDisabled(Func<string, string?>? read = null) =>
        (read ?? Environment.GetEnvironmentVariable)(DisableVariable) == "1";
}
