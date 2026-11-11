using System.Reflection;

namespace Halcyon.App.Infrastructure;

/// <summary>
/// What is running: the version from <c>Directory.Build.props</c> and the git commit it was built from. The SDK puts the
/// commit into the informational version ("1.1.0+&lt;sha&gt;") from the local .git; the image has no .git, so the
/// Dockerfile takes it from the <c>GIT_SHA</c> build argument (up.sh sets it). Unknown commit = "dev".
/// </summary>
public static class AppVersion
{
    private static readonly string Informational =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    public static string Version { get; } = Informational.Split('+')[0];

    /// <summary>The full commit, or null when the build did not know it.</summary>
    public static string? Commit { get; } =
        Informational.Split('+') is [_, { Length: > 0 } sha, ..] && sha != "unknown" ? sha : null;

    /// <summary>7 characters, keeping "-dirty" when the build had uncommitted changes.</summary>
    private static string ShortCommit =>
        Commit switch
        {
            null => "dev",
            { Length: > 7 } c => c[..7] + (c.EndsWith("-dirty", StringComparison.Ordinal) ? "-dirty" : ""),
            var c => c,
        };

    public static string Text => $"v{Version} · {ShortCommit}";
}
