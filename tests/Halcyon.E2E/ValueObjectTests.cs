using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>ADR-0015 in the browser: what the value objects refuse is said on the page, what they accept is stored in their form.</summary>
public class ValueObjectTests(E2EEnvironment env)
{
    [Fact]
    public async Task AgentsPhone_IsShownAsWritten_AndDialledAsDigits()
    {
        var page = await env.SignInAsync();
        await page.GetByTestId("nav-agents").ClickAsync();

        var phone = page.Locator("[data-testid=agent-card][data-email='anna.nowak@company.io']")
            .GetByTestId("agent-phone");
        await Expect(phone).ToHaveTextAsync("+48 600 100 101");
        await Expect(phone).ToHaveAttributeAsync("href", "tel:+48600100101");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Payment_AWrongIbanIsRefused_ThenTheRightOneIsStoredCompact_AndTheAmountInItsForm()
    {
        var page = await env.SignInAsync();
        var title = E2EEnvironment.Unique("Przelew dla dostawcy");
        await page.GotoAsync("/nowe");

        await page.GetByTestId("category-finance").ClickAsync();
        await page.GetByTestId("subcategory-finance.payments").ClickAsync();
        await page.GetByTestId("field-contractor").FillAsync("Biuro Rachunkowe");
        await page.GetByTestId("field-amount").FillAsync("1 234,5");
        await page.GetByTestId("field-currency").SelectOptionAsync("PLN");
        await page.GetByTestId("field-bank-account").FillAsync("pl62 1090 1014 0000 0712 1981 2874"); // check digits
        await page.GetByTestId("title").FillAsync(title);
        await page.GetByTestId("description").FillAsync("Faktura za listopad.");
        await page.GetByTestId("priority-Medium").ClickAsync();
        await page.GetByTestId("requester-search").FillAsync("Mazur");
        await page.GetByTestId("requester-option").First.ClickAsync();

        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("notice-error"))
            .ToContainTextAsync("Pole „Numer rachunku (IBAN)” nie jest poprawnym numerem rachunku (IBAN).");

        await page.GetByTestId("field-bank-account").FillAsync("pl61 1090 1014 0000 0712 1981 2874");
        await page.GetByTestId("submit").ClickAsync();

        await Expect(page.GetByTestId("ticket-title")).ToHaveTextAsync(title);
        await Expect(page.GetByTestId("ticket-field-bank-account")).ToHaveTextAsync("PL61109010140000071219812874");
        await Expect(page.GetByTestId("ticket-field-amount")).ToContainTextAsync("234,50");
        await page.Context.CloseAsync();
    }
}
