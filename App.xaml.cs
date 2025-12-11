using System.Windows;
using InazumaElevenVRSaveEditor.Common.Services;

namespace InazumaElevenVRSaveEditor
{
    public partial class App : Application
    {
        public static EACLauncherService? EACLauncherService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize theme
            var themeService = ThemeService.Instance;
            themeService.IsDarkTheme = true;

            // Initialize EAC service
            EACLauncherService = new EACLauncherService();
            EACLauncherService.PatchEACLauncher();

            // Create and show main window (loading screen is handled inside MainWindow)
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            EACLauncherService?.RestoreEACLauncher();

            base.OnExit(e);
        }
    }
}
