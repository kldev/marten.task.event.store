using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 008: recruitment orders a campaign from marketing, prefilled, and sees it on its ticket.</summary>
public class RecruitmentCampaignTests(E2EEnvironment env)
{
    [Fact]
    public async Task RecruitmentTicket_OrdersAPrefilledCampaign_WithSeveralChannels_AndListsIt()
    {
        var page = await env.SignInAsync();
        var position = E2EEnvironment.Unique("Monter rusztowań");
        await page.GotoAsync("/nowe");
        await page.GetByTestId("category-recruitment").ClickAsync();
        await page.GetByTestId("subcategory-recruitment.client").ClickAsync();
        await page.GetByTestId("field-client").FillAsync("Bau-Technik GmbH");
        await page.GetByTestId("field-position").FillAsync(position);
        await page.GetByTestId("field-headcount").FillAsync("8");
        await page.GetByTestId("title").FillAsync($"Rekrutacja: {position}");
        await page.GetByTestId("description").FillAsync("8 osób do Dortmundu.");
        await page.GetByTestId("priority-High").ClickAsync();
        await page.GetByTestId("requester-search").FillAsync("Kubiak");
        await page.GetByTestId("requester-option").First.ClickAsync();
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("ticket-field-headcount")).ToHaveTextAsync("8");
        var recruitmentNumber = await page.GetByTestId("ticket-number").InnerTextAsync();

        await page.GetByTestId("order-campaign").ClickAsync();
        await Expect(page.GetByTestId("subcategory-marketing.budget").Locator("input")).ToBeCheckedAsync();
        await Expect(page.GetByTestId("title")).ToHaveValueAsync($"Kampania rekrutacyjna: {position}");
        await Expect(page.GetByTestId("field-related-ticket")).ToHaveValueAsync(recruitmentNumber);
        await Expect(page.GetByTestId("field-campaign-goal")).ToHaveValueAsync("Rekrutacja");
        await Expect(page.GetByTestId("requester-selected")).ToContainTextAsync("Aleksandra Kubiak");

        await page.GetByTestId("field-channels-TikTok").ClickAsync();
        await page.GetByTestId("field-channels-Facebook").ClickAsync();
        await Expect(page.GetByTestId("field-channels-TikTok")).ToHaveAttributeAsync("aria-pressed", "true");
        await page.GetByTestId("field-amount").FillAsync("3 200");
        await page.GetByTestId("priority-Medium").ClickAsync();
        await page.GetByTestId("submit").ClickAsync();

        await Expect(page.GetByTestId("ticket-field-channels")).ToHaveTextAsync("Facebook, TikTok");
        await Expect(page.GetByTestId("ticket-field-amount")).ToContainTextAsync("3 200,00");
        await page.GetByTestId("related-ticket-link").ClickAsync();
        await Expect(page.GetByTestId("ticket-number")).ToHaveTextAsync(recruitmentNumber);
        await Expect(page.GetByTestId("campaign")).ToContainTextAsync($"Kampania rekrutacyjna: {position}");
        await page.Context.CloseAsync();
    }
}
