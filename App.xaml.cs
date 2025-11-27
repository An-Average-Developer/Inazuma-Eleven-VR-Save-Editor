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

            var themeService = ThemeService.Instance;
            themeService.IsDarkTheme = true;

            // Initialize and patch EAC Launcher
            EACLauncherService = new EACLauncherService();
            EACLauncherService.PatchEACLauncher();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Restore original EAC Launcher on app exit
            EACLauncherService?.RestoreEACLauncher();

            base.OnExit(e);
        }
    }
}
