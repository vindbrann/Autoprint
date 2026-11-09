// Autoprint : bascule du thème clair / sombre du client (référence pour l'intégration)
// À appeler depuis App.UpdateTheme(), qui suit déjà le thème de Windows (SystemEvents.UserPreferenceChanged).
// Non compilé dans cette branche de design : à vérifier lors de l'intégration (MaterialDesignThemes 5.4).
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Autoprint.Client
{
    public static class AutoprintTheme
    {
        private const string LightUri = "pack://application:,,,/Theme/Autoprint.Colors.Light.xaml";
        private const string DarkUri = "pack://application:,,,/Theme/Autoprint.Colors.Dark.xaml";

        // Couleurs principales de la charte (tokens.json > color.light / color.dark > primary, secondary)
        private static readonly Color PrimaryLight = (Color)ColorConverter.ConvertFromString("#1D5FCF");
        private static readonly Color PrimaryDark = (Color)ColorConverter.ConvertFromString("#5B9BF5");
        private static readonly Color SecondaryLight = (Color)ColorConverter.ConvertFromString("#3B4352");
        private static readonly Color SecondaryDark = (Color)ColorConverter.ConvertFromString("#C9CED8");

        /// <summary>Applique le thème Windows courant à MaterialDesign et aux couleurs Autoprint.</summary>
        public static void Apply()
        {
            bool dark = (Theme.GetSystemTheme() ?? BaseTheme.Light) == BaseTheme.Dark;

            // 1. MaterialDesign : fond, textes et palette primaire / secondaire
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
            theme.SetPrimaryColor(dark ? PrimaryDark : PrimaryLight);
            theme.SetSecondaryColor(dark ? SecondaryDark : SecondaryLight);
            paletteHelper.SetTheme(theme);

            // 2. Autoprint : remplace le dictionnaire de couleurs (les styles utilisent DynamicResource)
            var merged = Application.Current.Resources.MergedDictionaries;
            var current = merged.FirstOrDefault(d => d.Source != null &&
                d.Source.OriginalString.Contains("Autoprint.Colors.", StringComparison.OrdinalIgnoreCase));
            var next = new ResourceDictionary { Source = new Uri(dark ? DarkUri : LightUri, UriKind.Absolute) };
            if (current == null) merged.Insert(0, next);
            else if (!string.Equals(current.Source.OriginalString, next.Source.OriginalString, StringComparison.OrdinalIgnoreCase))
                merged[merged.IndexOf(current)] = next;
        }
    }
}
