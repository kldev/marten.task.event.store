namespace Halcyon.App.Monitoring;

/// <summary>The app's record of itself (ADR-0011): HTTP answers and the log into the database, with retention.</summary>
public static class MonitoringSetup
{
    public static WebApplicationBuilder AddHalcyonMonitoring(this WebApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<MonitoringOptions>()
            .Bind(builder.Configuration.GetSection(MonitoringOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<MonitoringQueues>();
        builder.Services.AddSingleton<BackgroundJobs>();
        builder.Services.AddSingleton<AppLogSink>();
        builder.Services.AddSingleton<MonitoringQueries>();
        builder.Services.AddSingleton<DatabaseActivity>();

        builder.Services.AddSingleton<MonitoringWriter>();
        builder.Services.AddHostedService(services => services.GetRequiredService<MonitoringWriter>());
        builder.Services.AddSingleton<MonitoringCleanup>();
        builder.Services.AddHostedService(services => services.GetRequiredService<MonitoringCleanup>());
        return builder;
    }

    /// <summary>First in the pipeline: it must see the status the client finally got.</summary>
    public static IApplicationBuilder UseRequestRecording(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestRecording>();
}
