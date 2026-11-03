namespace Halcyon.App;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var app = HalcyonApp.Build(args);
        await HalcyonApp.RunAsync(app);
    }
}
