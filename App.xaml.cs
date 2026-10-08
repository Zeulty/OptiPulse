using System;
using System.Windows;
using ChlorideTweaks.Models;
using ChlorideTweaks.Services;

namespace ChlorideTweaks
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply the saved UI accent color before any window is created.
            ThemeService.Initialize();

            // Surface unexpected errors instead of exiting silently.
            DispatcherUnhandledException += (_, args) =>
            {
                MessageBox.Show(args.Exception.Message, "OptiPulse - Unexpected error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // GhostTools desktop-menu commands: run silently and exit.
            if (e.Args.Length > 0)
            {
                if (e.Args[0].Equals("--reduce-memory", StringComparison.OrdinalIgnoreCase))
                {
                    GhostToolsService.ReduceMemory();
                    Shutdown();
                    return;
                }
                if (e.Args[0].Equals("--cleanup-temp", StringComparison.OrdinalIgnoreCase))
                {
                    GhostToolsService.CleanupTemp();
                    Shutdown();
                    return;
                }
            }

            // Restore the persisted language before the window is created,
            // so the main window comes up localized.
            LocalizationService.Set(
                LocalizationService.FromCode(AppStateService.State.Language), persist: false);

            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }
    }
}
