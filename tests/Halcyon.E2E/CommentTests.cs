using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 005 in the browser.</summary>
public class CommentTests(E2EEnvironment env)
{
    [Fact]
    public async Task Comment_IsSeenByAnotherAgent_AndMarkupStaysText()
    {
        var (id, _) = await env.ReportAsync(
            E2EEnvironment.Unique("Projektor"),
            Priority.Medium,
            TimeSpan.FromMinutes(20)
        );
        var author = await env.SignInAsync("anna.nowak@company.io");
        await author.GotoAsync($"/zgloszenia/{id}");

        await author.GetByTestId("comment-input").FillAsync("Sprawdzę kabel <script>alert('x')</script>");
        await author.GetByTestId("comment-submit").ClickAsync();
        await Expect(author.GetByTestId("notice-success")).ToHaveTextAsync("Komentarz dodany.");

        var reader = await env.SignInAsync("piotr.kowalski@company.io");
        await reader.GotoAsync($"/zgloszenia/{id}");
        var comment = reader.GetByTestId("comment").Last;
        await Expect(comment.GetByTestId("comment-author")).ToHaveTextAsync("Anna Nowak");
        await Expect(comment.GetByTestId("comment-text")).ToHaveTextAsync("Sprawdzę kabel <script>alert('x')</script>");
        await Expect(reader.Locator("[data-testid=comments] script")).ToHaveCountAsync(0);
        await author.Context.CloseAsync();
        await reader.Context.CloseAsync();
    }

    [Fact]
    public async Task EmptyComment_IsRefused()
    {
        var (id, _) = await env.ReportAsync(E2EEnvironment.Unique("Pusty"), Priority.Low, TimeSpan.FromMinutes(1));
        var page = await env.SignInAsync();
        await page.GotoAsync($"/zgloszenia/{id}");

        await page.GetByTestId("comment-input").FillAsync("   ");
        await page.GetByTestId("comment-submit").ClickAsync();

        await Expect(page.GetByTestId("notice-error")).ToContainTextAsync("Komentarz");
        await page.Context.CloseAsync();
    }
}
