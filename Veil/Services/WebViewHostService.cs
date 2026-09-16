using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Veil.Services;

public sealed class WebViewHostService
{
    public async Task InitializeAsync(WebView2 webView, Action<CoreWebView2> beforeNavigation)
    {
        var frontendUrl = Environment.GetEnvironmentVariable("VEIL_FRONTEND_URL");
        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            var webViewDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Veil",
                "WebView2");
            Directory.CreateDirectory(webViewDataDirectory);
            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: webViewDataDirectory);
            await webView.EnsureCoreWebView2Async(webViewEnvironment);
        }
        else
        {
            await webView.EnsureCoreWebView2Async();
        }

        beforeNavigation(webView.CoreWebView2);

        if (!string.IsNullOrWhiteSpace(frontendUrl))
        {
            webView.CoreWebView2.Navigate(frontendUrl);
            return;
        }

        var frontendDirectory = ExtractFrontend();
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "veil.local",
            frontendDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        webView.CoreWebView2.Navigate("https://veil.local/index.html");
    }

    private static string ExtractFrontend()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Veil.Frontend.zip")
            ?? throw new InvalidOperationException("The packaged frontend resource is missing.");
        var directory = Path.Combine(Path.GetTempPath(), "Veil", "frontend");
        Directory.CreateDirectory(directory);
        ZipFile.ExtractToDirectory(resource, directory, overwriteFiles: true);
        return directory;
    }
}
