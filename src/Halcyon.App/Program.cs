using Halcyon.App.Messaging;
using JasperFx;

namespace Halcyon.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = HalcyonApp.Build(args);
        // `codegen write` (the image build, ADR-0016): writes Wolverine's handler code and exits - no database, no start.
        if (WolverineSetup.IsCodegenCommand(args))
            return await app.RunJasperFxCommands(args);
        await HalcyonApp.RunAsync(app);
        return 0;
    }
}
