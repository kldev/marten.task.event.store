using System.Globalization;
using System.Runtime.CompilerServices;
using Halcyon.App.Localization;

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>
/// The tests call features outside a request, where no culture cookie picks the language, so they read the process
/// culture. Polish is the application's default (ADR-0012); pin it so the run does not depend on the machine's LANG.
/// </summary>
internal static class PolishCulture
{
    [ModuleInitializer]
    internal static void Pin() =>
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture =
            CultureInfo.GetCultureInfo(Language.Polish);
}
