using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>
/// Screenshots of the main screens (desktop and phone) into <c>screens/</c> next to the test output, for a human look
/// at the layout. With <c>HALCYON_SCREENS_TO_DOCS=1</c> (<c>scripts/screens.sh</c>) they also go to
/// <c>docs/screens/</c>, the ones the README shows. Asserts only that the pages rendered.
/// </summary>
public class ScreenshotTests(E2EEnvironment env)
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "screens");

    private static readonly string? DocsDir =
        Environment.GetEnvironmentVariable("HALCYON_SCREENS_TO_DOCS") == "1"
            ? Path.Combine(E2EEnvironment.RepoRoot(), "docs", "screens")
            : null;

    private static async Task ShotAsync(IPage page, string name, bool fullPage = true)
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
        await File.WriteAllBytesAsync(Path.Combine(Dir, $"{name}.jpg"), bytes);
        if (DocsDir is not null)
            await File.WriteAllBytesAsync(Path.Combine(DocsDir, $"{name}.jpg"), bytes);
    }

    [Fact]
    public async Task MainScreens()
    {
        Directory.CreateDirectory(Dir);
        if (DocsDir is not null)
            Directory.CreateDirectory(DocsDir);

        await env.ReportAsync("Serwer pocztowy odrzuca załączniki", Priority.Critical, TimeSpan.FromMinutes(150));
        var (id, _) = await env.ReportAsync(
            "Błąd przy zapisie faktury w CRM",
            Priority.High,
            TimeSpan.FromHours(2),
            "it.application",
            "Przy zapisie faktury z rabatem CRM pokazuje błąd 500 i faktura znika. Dotyczy całego działu sprzedaży, "
                + "od rana nie wystawiamy faktur z rabatem."
        );
        await env.TakeAsync(id, "anna.nowak@company.io");

        // ---- the public form: the requester, no sign-in
        var anonymous = await env.AnonymousPageAsync();
        await anonymous.SetViewportSizeAsync(1440, 900);
        var (errorShot, scan, brief) = await AttachmentFilesAsync(anonymous);
        await anonymous.SetViewportSizeAsync(1440, 900);

        await anonymous.GotoAsync("/zglos");
        await Expect(anonymous.GetByTestId("report-form")).ToBeVisibleAsync();
        await anonymous.GetByTestId("category-hr-abroad").ClickAsync();
        await anonymous.GetByTestId("subcategory-hr-abroad.be.posting").ClickAsync();
        await anonymous.GetByTestId("title").FillAsync("Delegowanie do Gandawy od 1 grudnia");
        await anonymous
            .GetByTestId("description")
            .FillAsync("Jadę na 3 miesiące do Gandawy. Proszę o zgłoszenie Limosa i formularz A1.");
        await anonymous.GetByTestId("priority-High").ClickAsync();
        await anonymous.GetByTestId("attachments-input").SetInputFilesAsync(Png("skan-umowy-oddelegowania.png", scan));
        await Expect(anonymous.GetByTestId("attachment-pending")).ToHaveCountAsync(1);
        await anonymous.GetByTestId("requester-search").FillAsync("Michalski");
        await anonymous.GetByTestId("requester-option").First.ClickAsync();
        await ShotAsync(anonymous, "01-public-report");

        await anonymous.GetByTestId("submit").ClickAsync();
        await Expect(anonymous.GetByTestId("public-sent")).ToBeVisibleAsync();
        await ShotAsync(anonymous, "10-public-report-sent", fullPage: false);

        await anonymous.GotoAsync("/zglos");
        await anonymous.GetByTestId("category-marketing").ClickAsync();
        await anonymous.GetByTestId("subcategory-marketing.graphics").ClickAsync();
        await ShotAsync(anonymous, "07-public-report-marketing");

        await anonymous.GotoAsync("/logowanie");
        await Expect(anonymous.GetByTestId("signin-default")).ToBeVisibleAsync();
        await ShotAsync(anonymous, "02-sign-in-demo", fullPage: false);

        // ---- the agent
        var page = await env.SignInAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await Expect(page.GetByTestId("ticket-table")).ToBeVisibleAsync();
        await ShotAsync(page, "03-pool");

        await page.GotoAsync($"/zgloszenia/{id}");
        await Expect(page.GetByTestId("timeline")).ToBeVisibleAsync();
        await page.GetByTestId("attachments-input")
            .SetInputFilesAsync([
                Png("blad-zapisu-faktury.png", errorShot),
                new FilePayload
                {
                    Name = "kroki-do-odtworzenia.pdf",
                    MimeType = "application/pdf",
                    Buffer = brief,
                },
            ]);
        await page.GetByTestId("attachments-submit").ClickAsync();
        await Expect(page.GetByTestId("attachment")).ToHaveCountAsync(2);
        await page.GetByTestId("comment-input").FillAsync("Odtworzone na testowym CRM - błąd przy fakturze z rabatem.");
        await page.GetByTestId("comment-submit").ClickAsync();
        await Expect(page.GetByTestId("comment")).ToHaveCountAsync(1);
        await ShotAsync(page, "04-ticket-attachments");

        await page.GotoAsync("/nowe");
        await page.GetByTestId("category-it").ClickAsync();
        await page.GetByTestId("priority-Critical").ClickAsync();
        await page.GetByTestId("reported-at-field").ClickAsync();
        await Expect(page.GetByTestId("reported-at-popup")).ToBeVisibleAsync();
        await ShotAsync(page, "05-new-ticket-picker");

        await page.GotoAsync("/wszystkie");
        await Expect(page.GetByTestId("ticket-table")).ToBeVisibleAsync();
        await page.GetByTestId("filter-category").SelectOptionAsync("hr-abroad");
        await Expect(page.GetByTestId("ticket-row").First).ToContainTextAsync("Kadry Zagranica");
        await ShotAsync(page, "06-all-tickets-hr");

        await page.GotoAsync("/agenci");
        await Expect(page.GetByTestId("agent-card").First).ToBeVisibleAsync();
        await ShotAsync(page, "08-agents");

        // ---- spec 008: report fields and the reports (the seed has HR tickets with data)
        await page.GotoAsync("/nowe");
        await page.GetByTestId("category-hr-poland").ClickAsync();
        await page.GetByTestId("subcategory-hr-poland.pl.onboarding").ClickAsync();
        await page.GetByTestId("field-first-name").FillAsync("Olena");
        await page.GetByTestId("field-last-name").FillAsync("Kovalenko");
        await page.GetByTestId("field-department").SelectOptionAsync("Logistyka");
        await page.GetByTestId("field-country").SelectOptionAsync("Ukraina");
        await page.GetByTestId("field-identity-document").SelectOptionAsync("PESEL");
        await ShotAsync(page, "11-new-employee-fields");

        await page.GotoAsync("/raporty/kadry-zagranica");
        await Expect(page.GetByTestId("report-table")).ToBeVisibleAsync();
        await ShotAsync(page, "12-report-hr-abroad");

        await page.GotoAsync("/raporty/agenci");
        await Expect(page.GetByTestId("report-table")).ToBeVisibleAsync();
        await ShotAsync(page, "13-report-agents");

        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync("/");
        await Expect(page.GetByTestId("ticket-table")).ToBeVisibleAsync();
        await ShotAsync(page, "09-pool-phone");

        await page.Context.CloseAsync();
        await anonymous.Context.CloseAsync();
    }

    private static FilePayload Png(string name, byte[] bytes) =>
        new()
        {
            Name = name,
            MimeType = "image/png",
            Buffer = bytes,
        };

    /// <summary>Believable files to attach, drawn by the browser itself: an error dialog, a document scan, a PDF.</summary>
    private static async Task<(byte[] ErrorShot, byte[] Scan, byte[] Pdf)> AttachmentFilesAsync(IPage page)
    {
        await page.SetViewportSizeAsync(960, 600);
        await page.SetContentAsync(
            """
            <body style="margin:0;font-family:system-ui;background:#e8edf3">
              <div style="height:44px;background:#1f3a5f;color:#fff;display:flex;align-items:center;padding:0 18px;font-weight:600">CRM · Faktury</div>
              <div style="padding:28px;display:flex;gap:18px">
                <div style="flex:1;background:#fff;border-radius:10px;padding:18px;color:#334">
                  <div style="height:12px;width:60%;background:#cfd8e3;border-radius:6px;margin:10px 0"></div>
                  <div style="height:12px;width:80%;background:#cfd8e3;border-radius:6px;margin:10px 0"></div>
                  <div style="height:12px;width:45%;background:#cfd8e3;border-radius:6px;margin:10px 0"></div>
                </div>
              </div>
              <div style="position:absolute;top:150px;left:210px;width:540px;background:#fff;border-radius:12px;box-shadow:0 20px 50px #0004;overflow:hidden">
                <div style="background:#c62828;color:#fff;padding:14px 18px;font-weight:700">Błąd zapisu (500)</div>
                <div style="padding:18px;color:#333;line-height:1.5">Nie udało się zapisać faktury FV/2026/11/0412.<br>
                  <code style="font-size:13px;color:#a11">NullReferenceException at InvoiceService.ApplyDiscount</code></div>
                <div style="padding:0 18px 18px;text-align:right"><span style="background:#1f3a5f;color:#fff;padding:8px 18px;border-radius:8px">OK</span></div>
              </div>
            </body>
            """
        );
        var errorShot = await page.ScreenshotAsync();

        await page.SetContentAsync(
            """
            <body style="margin:0;font-family:Georgia,serif;background:#f4f1ea;display:flex;justify-content:center;padding:30px">
              <div style="width:560px;background:#fff;padding:36px;box-shadow:0 4px 14px #0002;color:#222">
                <div style="text-align:center;font-weight:700;letter-spacing:1px">POROZUMIENIE O ODDELEGOWANIU</div>
                <div style="text-align:center;margin:6px 0 24px;color:#666">Belgia · Gandawa · 01.12.2026 - 28.02.2027</div>
                <div style="height:10px;background:#ddd;margin:12px 0"></div><div style="height:10px;background:#ddd;margin:12px 0;width:90%"></div>
                <div style="height:10px;background:#ddd;margin:12px 0;width:75%"></div><div style="height:10px;background:#ddd;margin:12px 0"></div>
                <div style="margin-top:40px;display:flex;justify-content:space-between;color:#555"><span>Pracodawca</span><span>Pracownik</span></div>
              </div>
            </body>
            """
        );
        var scan = await page.ScreenshotAsync();

        await page.SetContentAsync(
            "<h1 style='font-family:system-ui'>Kroki do odtworzenia</h1><ol><li>Faktura z rabatem 10%</li><li>Zapisz</li></ol>"
        );
        var pdf = await page.PdfAsync();
        return (errorShot, scan, pdf);
    }
}
