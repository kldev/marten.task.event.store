using Halcyon.App.Auth;
using Halcyon.App.Features.Agents;
using Halcyon.App.Features.Reports;
using Halcyon.App.Features.Tickets;
using Halcyon.App.Infrastructure;
using Halcyon.App.Localization;
using Halcyon.App.Monitoring;
using Halcyon.App.Storage;
using Halcyon.Database;
using Halcyon.Database.Migrations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace Halcyon.App;

/// <summary>
/// Builds the one host (ADR-0001). Tests build it the same way, with their own settings (the <c>configure</c> of <see cref="Build"/>).
/// </summary>
public static class HalcyonApp
{
    public const string ReadyTag = "ready";

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        // Named explicitly: under a test runner the entry assembly is the tests', and the static assets manifest is
        // looked up by the application's name (Halcyon.App.staticwebassets.endpoints.json).
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { Args = args, ApplicationName = typeof(HalcyonApp).Assembly.GetName().Name }
        );
        configure?.Invoke(builder);

        builder.Services.AddSerilog(
            (services, logger) =>
                logger
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .WriteTo.Console()
                    // The log the system pages browse, kept for the retention (ADR-0011).
                    .WriteTo.Sink(services.GetRequiredService<AppLogSink>()),
            // The host's own logger, not the static Log.Logger: two hosts in one process (the tests) would otherwise write
            // into each other's log - and so into each other's database.
            preserveStaticLogger: true
        );
        builder.AddHalcyonMonitoring();

        builder.Services.AddHalcyonDatabase(
            DatabaseStartup.ConnectionString(builder.Configuration),
            DatabaseStartup.SchemaCreation(builder.Configuration, builder.Environment)
        );

        builder.AddHalcyonAuth();
        builder.AddHalcyonStorage(ReadyTag);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddHalcyonLanguages();

        builder.AddHalcyonSla();
        // Try: a test may have put its own in (spec 012, a number met twice).
        builder.Services.TryAddSingleton<TicketIds>();
        builder.Services.AddSingleton<TicketWriter>();
        builder.Services.AddSingleton<PublicReportLimiter>();
        builder.Services.AddSingleton<ReportTicket>();
        builder.Services.AddSingleton<TakeTicket>();
        builder.Services.AddSingleton<HandOverTicket>();
        builder.Services.AddSingleton<ReturnTicketToPool>();
        builder.Services.AddSingleton<ChangeTicketPriority>();
        builder.Services.AddSingleton<ResolveTicket>();
        builder.Services.AddSingleton<CommentOnTicket>();
        builder.Services.AddSingleton<UpdateTicketFields>();
        builder.Services.AddSingleton<AssignTicketHandler>();
        builder.Services.AddSingleton<CategoryReportQueries>();
        builder.Services.AddSingleton<AgentReportQueries>();
        builder.Services.AddSingleton<ChartQueries>();
        builder.Services.AddSingleton<AttachmentFiles>();
        builder.Services.AddSingleton<AddTicketAttachments>();
        builder.Services.AddSingleton<TicketQueries>();
        builder.Services.AddSingleton<AgentDirectory>();
        builder.Services.AddScoped<CurrentAgent>();
        builder.Services.AddScoped<TicketChanges>();

        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        var app = builder.Build();

        app.UseRequestRecording();
        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/blad", createScopeForErrors: true);

        app.UseSerilogRequestLogging(options =>
            options.GetLevel = (http, _, exception) =>
                exception is not null || http.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
                : http.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
                : Serilog.Events.LogEventLevel.Information
        );
        // Only for pages a browser asked for: re-executing a rejected POST (429 from the sign-in brake) on a Blazor page
        // would answer with an antiforgery error instead of the real reason.
        app.UseWhen(
            http => HttpMethods.IsGet(http.Request.Method),
            pages => pages.UseStatusCodePagesWithReExecute("/nie-znaleziono", createScopeForStatusCodePages: true)
        );
        // The culture of the request from the cookie (ADR-0012) - also of the circuit a page opens.
        app.UseRequestLocalization();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) })
            .AllowAnonymous();

        app.MapHalcyonAuth();
        app.MapLanguageSwitch();
        app.MapAttachments();
        app.MapReportExports();
        app.MapStaticAssets().AllowAnonymous();
        app.MapRazorComponents<Components.App>().AddInteractiveServerRenderMode();

        return app;
    }

    /// <summary>Checks (or applies, locally) the schema, then serves (ADR-0002).</summary>
    public static async Task RunAsync(WebApplication app)
    {
        app.Logger.LogInformation(
            "Halcyon {Version} (commit {Commit})",
            AppVersion.Version,
            AppVersion.Commit ?? "dev"
        );
        await app.Services.PrepareDatabaseAsync();
        await app.RunAsync();
    }
}
