using Microsoft.Extensions.Configuration;

namespace MiddleCsharp.Presentation.Hosting;

internal static class HttpsRedirectionPolicy
{
    private const string EnabledKey = "HttpsRedirection:Enabled";

    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue(EnabledKey, defaultValue: true);
}
