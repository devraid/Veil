using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Veil.Services;

namespace Veil
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _services;

        private static readonly string StartupLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Veil",
            "startup.log");

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
                WriteStartupLog("Starting application.");
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddDebug());
                services.AddSingleton<AppSettingsStore>();
                services.AddSingleton<SettingsService>();
                services.AddSingleton<OpenAiChatService>();
                services.AddSingleton<ImageDataUrlService>();
                services.AddSingleton<ImageStorageService>();
                services.AddSingleton<FrontendMessageSender>();
                services.AddSingleton<WebViewHostService>();
                services.AddSingleton<StartupService>();
                services.AddSingleton<ChatRequestService>();
                services.AddSingleton<ChatRequestCancellation>();
                services.AddTransient<MainWindow>();
                _services = services.BuildServiceProvider();
                _services.GetRequiredService<MainWindow>().Show();
                WriteStartupLog("Main window shown.");
            }
            catch (Exception exception)
            {
                WriteStartupLog("Startup failed.", exception);
                throw;
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            WriteStartupLog("Dispatcher exception.", e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteStartupLog("Unhandled exception.", e.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteStartupLog("Unobserved task exception.", e.Exception);
        }

        private static void WriteStartupLog(string message, Exception? exception = null)
        {
            try
            {
                var directory = Path.GetDirectoryName(StartupLogPath)!;
                Directory.CreateDirectory(directory);
                File.AppendAllText(StartupLogPath,
                    $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}{exception}{Environment.NewLine}");
            }
            catch
            {
                // Last-resort diagnostics must never prevent application shutdown.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            base.OnExit(e);
        }
    }

}
