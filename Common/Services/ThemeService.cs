using System;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using MaterialDesignColors;

namespace InazumaElevenVRSaveEditor.Common.Services
{
    public class ThemeService
    {
        private static ThemeService? _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        private readonly PaletteHelper _paletteHelper;
        private bool _isDarkTheme;

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                _isDarkTheme = value;
                ApplyTheme(value);
            }
        }

        public event EventHandler<bool>? ThemeChanged;

        private ThemeService()
        {
            _paletteHelper = new PaletteHelper();
            _isDarkTheme = true;
        }

        public void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ThemeChanged?.Invoke(this, IsDarkTheme);
        }

        private void ApplyTheme(bool isDark)
        {
            var theme = _paletteHelper.GetTheme();

            if (isDark)
            {
                theme.SetBaseTheme(BaseTheme.Light);

                theme.Background = Color.FromRgb(255, 235, 59);
                theme.Foreground = Color.FromRgb(0, 0, 0);

                theme.SetPrimaryColor(Color.FromRgb(13, 71, 161));
                theme.PrimaryLight = Color.FromRgb(25, 118, 210);
                theme.PrimaryMid = Color.FromRgb(13, 71, 161);
                theme.PrimaryDark = Color.FromRgb(1, 87, 155);

                theme.SetSecondaryColor(Color.FromRgb(255, 193, 7));
                theme.SecondaryLight = Color.FromRgb(255, 213, 79);
                theme.SecondaryMid = Color.FromRgb(255, 193, 7);
                theme.SecondaryDark = Color.FromRgb(255, 160, 0);

                theme.ValidationError = Color.FromRgb(211, 47, 47);
            }
            else
            {
                theme.SetBaseTheme(BaseTheme.Light);

                theme.Background = Color.FromRgb(250, 250, 250);
                theme.Foreground = Color.FromRgb(33, 33, 33);

                theme.SetPrimaryColor(Color.FromRgb(103, 58, 183));
                theme.PrimaryLight = Color.FromRgb(179, 157, 219);
                theme.PrimaryMid = Color.FromRgb(103, 58, 183);
                theme.PrimaryDark = Color.FromRgb(77, 40, 140);

                theme.SetSecondaryColor(Color.FromRgb(205, 220, 57));
                theme.SecondaryLight = Color.FromRgb(220, 231, 117);
                theme.SecondaryMid = Color.FromRgb(205, 220, 57);
                theme.SecondaryDark = Color.FromRgb(175, 180, 43);

                theme.ValidationError = Color.FromRgb(211, 47, 47);
            }

            _paletteHelper.SetTheme(theme);
        }

        public Color GetThemeColor()
        {
            return IsDarkTheme ? Color.FromRgb(18, 18, 18) : Color.FromRgb(250, 250, 250);
        }
    }
}
