using System.Globalization;
using Halcyon.Domain.Time;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Specs 002 and 006: an agent registers a ticket with the custom date picker.</summary>
public class ReportTicketTests(E2EEnvironment env)
{
    private static DateOnly Today => DateOnly.FromDateTime(LocalTime.ToLocal(DateTimeOffset.UtcNow).DateTime);

    [Fact]
    public async Task Agent_ReportsWithThePicker_CascadeAndDeadlinePreview_AndTheTicketIsInThePool()
    {
        var page = await env.SignInAsync();
        var title = E2EEnvironment.Unique("Monitor miga");
        await page.GotoAsync("/nowe");

        await page.GetByTestId("title").FillAsync(title);
        await page.GetByTestId("description").FillAsync("Monitor w sali 3 miga co kilka sekund.");

        // Cascade: no subcategory before a category; a new category clears the subcategory.
        await Expect(page.GetByTestId("subcategory-empty")).ToBeVisibleAsync();
        await page.GetByTestId("category-legal").ClickAsync();
        await page.GetByTestId("subcategory-legal.contracts").ClickAsync();
        await page.GetByTestId("category-it").ClickAsync();
        await Expect(page.GetByTestId("subcategories").Locator("input:checked")).ToHaveCountAsync(0);
        await Expect(page.GetByTestId("subcategories").Locator("label"))
            .ToHaveTextAsync(["Sprzęt", "Awaria", "Problem z aplikacją", "Feedback"]);
        await page.GetByTestId("subcategory-it.hardware").ClickAsync();

        await page.GetByTestId("priority-High").ClickAsync();

        // The picker: yesterday 09:30.
        var yesterday = Today.AddDays(-1);
        await page.GetByTestId("reported-at-field").ClickAsync();
        await Expect(page.GetByTestId("reported-at-popup")).ToBeVisibleAsync();
        await page.Locator($"[data-testid=reported-at-grid] [data-date='{yesterday:yyyy-MM-dd}']").ClickAsync();
        await page.GetByTestId("reported-at-hour").SelectOptionAsync("9");
        await page.GetByTestId("reported-at-minute").SelectOptionAsync("30");
        await page.GetByTestId("reported-at-done").ClickAsync();
        await Expect(page.GetByTestId("reported-at-value"))
            .ToHaveTextAsync($"{yesterday.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} 09:30");

        // Deadline preview: yesterday 09:30 + 8 h.
        await Expect(page.GetByTestId("deadline-preview-value"))
            .ToHaveTextAsync($"{yesterday.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} 17:30");

        await page.GetByTestId("requester-search").FillAsync("Mazur");
        await page.GetByTestId("requester-option").First.ClickAsync();
        await Expect(page.GetByTestId("requester-selected")).ToContainTextAsync("Adam Mazur");

        await page.GetByTestId("submit").ClickAsync();

        await Expect(page.GetByTestId("ticket-title")).ToHaveTextAsync(title);
        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("dodane do puli");
        await Expect(page.GetByTestId("ticket-requester")).ToHaveTextAsync("Adam Mazur");

        await page.GetByTestId("nav-pool").ClickAsync();
        var row = await E2EEnvironment.SearchAsync(page, title);
        await Expect(row).ToBeVisibleAsync();
        // Reported yesterday 09:30 with 8 h: already overdue.
        await Expect(row.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "overdue");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Picker_DisablesFutureDays_AndWorksWithTheKeyboard()
    {
        var page = await env.SignInAsync();
        await page.GotoAsync("/nowe");

        await page.GetByTestId("reported-at-field").ClickAsync();
        var grid = page.GetByTestId("reported-at-grid");
        await Expect(grid.Locator($"[data-date='{Today.AddDays(1):yyyy-MM-dd}']")).ToBeDisabledAsync();
        await Expect(grid.Locator($"[data-date='{Today:yyyy-MM-dd}']")).ToBeEnabledAsync();

        // Arrow left + Enter picks yesterday; Escape closes the calendar.
        await grid.FocusAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("Enter");
        await Expect(grid.Locator($"[data-date='{Today.AddDays(-1):yyyy-MM-dd}']"))
            .ToHaveAttributeAsync("aria-selected", "true");
        await page.Keyboard.PressAsync("Escape");
        await Expect(page.GetByTestId("reported-at-popup")).ToBeHiddenAsync();
        await Expect(page.GetByTestId("reported-at-value"))
            .ToContainTextAsync(Today.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task MissingFields_AreShown_AndNothingIsSent()
    {
        var page = await env.SignInAsync();
        await page.GotoAsync("/nowe");

        await page.GetByTestId("submit").ClickAsync();

        await Expect(page.Locator(".validation-message").First).ToBeVisibleAsync();
        await Expect(page.GetByText("Podaj tytuł.")).ToBeVisibleAsync();
        await Expect(page.GetByText("Wybierz priorytet.")).ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task NeitherPersonNorIncognito_IsRefusedByTheDomain()
    {
        var page = await env.SignInAsync();
        await page.GotoAsync("/nowe");
        await page.GetByTestId("title").FillAsync(E2EEnvironment.Unique("Bez zgłaszającego"));
        await page.GetByTestId("description").FillAsync("Opis");
        await page.GetByTestId("category-marketing").ClickAsync();
        await page.GetByTestId("subcategory-marketing.website").ClickAsync();
        await page.GetByTestId("priority-Low").ClickAsync();

        await page.GetByTestId("submit").ClickAsync();

        await Expect(page.GetByTestId("notice-error")).ToContainTextAsync("Wybierz zgłaszającego");
        await page.Context.CloseAsync();
    }
}
