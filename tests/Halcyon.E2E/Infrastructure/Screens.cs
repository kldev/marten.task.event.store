namespace Halcyon.E2E.Infrastructure;

/// <summary>
/// Writes screenshots to <c>screens/&lt;folder&gt;</c> next to the test output and, with <c>HALCYON_SCREENS_TO_DOCS=1</c>
/// (<c>scripts/screens.sh</c>), also to <c>docs/screens/&lt;folder&gt;</c> - the ones the README and
/// <c>docs/Portal.md</c> show.
/// </summary>
public sealed class Screens
{
    private readonly string dir;
    private readonly string? docsDir;

    public Screens(string folder = "")
    {
        dir = Path.Combine(AppContext.BaseDirectory, "screens", folder);
        docsDir =
            Environment.GetEnvironmentVariable("HALCYON_SCREENS_TO_DOCS") == "1"
                ? Path.Combine(E2EEnvironment.RepoRoot(), "docs", "screens", folder)
                : null;
        Directory.CreateDirectory(dir);
        if (docsDir is not null)
            Directory.CreateDirectory(docsDir);
    }

    public async Task ShotAsync(IPage page, string name, bool fullPage = true)
    {
        // Let fonts and the circuit settle so a shot never catches a half-rendered page.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var viewport = page.ViewportSize!;
        if (fullPage)
        {
            // A window as tall as the page instead of Playwright's full-page stitching: the sticky sidebar and top bar
            // then stay where they belong and the sidebar runs the whole height.
            await page.EvaluateAsync("window.scrollTo(0, 0)");
            var height = await page.EvaluateAsync<int>("document.documentElement.scrollHeight");
            await page.SetViewportSizeAsync(viewport.Width, Math.Max(viewport.Height, height));
            await page.WaitForTimeoutAsync(150);
        }
        var bytes = await page.ScreenshotAsync(new() { Type = ScreenshotType.Jpeg, Quality = 85 });
        if (fullPage)
            await page.SetViewportSizeAsync(viewport.Width, viewport.Height);
        await File.WriteAllBytesAsync(Path.Combine(dir, $"{name}.jpg"), bytes);
        if (docsDir is not null)
            await File.WriteAllBytesAsync(Path.Combine(docsDir, $"{name}.jpg"), bytes);
    }
}
