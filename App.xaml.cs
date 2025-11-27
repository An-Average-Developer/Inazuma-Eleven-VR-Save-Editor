using System.Windows;
using InazumaElevenVRSaveEditor.Common.Services;

namespace InazumaElevenVRSaveEditor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var themeService = ThemeService.Instance;
            themeService.IsDarkTheme = true;
        }
    }
}
