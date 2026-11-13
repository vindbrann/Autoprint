# Thème du client de bureau

Fichiers générés par `sources/build_theme.py` à partir de `tokens.json` : ne pas les modifier à la main.

| Fichier | Rôle |
|---|---|
| `Autoprint.Colors.Light.xaml` / `Autoprint.Colors.Dark.xaml` | couleurs (`Autoprint.Color.*`) et pinceaux (`Autoprint.Brush.*`), un fichier par thème |
| `Autoprint.Theme.xaml` | polices, tailles, styles (boutons, champs, onglets en pilule, cartes d'imprimante, interrupteur, zone technique, barres de défilement) |
| `AutoprintTheme.cs` | exemple de bascule clair / sombre, à appeler depuis `App.UpdateTheme()` |

## Intégration

1. Créer `Autoprint.Client/Theme/` et y copier les trois `.xaml` (action de génération : `Page`).
2. Créer `Autoprint.Client/Fonts/` et y copier `ressources/polices/ttf/*.ttf` (action de génération : `Resource`).
3. Dans `App.xaml`, remplacer `BundledTheme` (Grey / Lime) et fusionner dans cet ordre :
   ```xml
   <materialDesign:BundledTheme BaseTheme="Inherit" PrimaryColor="Blue" SecondaryColor="Blue" />
   <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml" />
   <ResourceDictionary Source="Theme/Autoprint.Colors.Light.xaml" />
   <ResourceDictionary Source="Theme/Autoprint.Theme.xaml" />
   ```
   La palette réelle (bleu Autoprint) est appliquée au démarrage par `AutoprintTheme.Apply()`.
4. Dans `App.UpdateTheme()`, remplacer le contenu par `AutoprintTheme.Apply();` : la palette MaterialDesign et le dictionnaire de couleurs suivent alors le thème de Windows.
5. Remplacer les couleurs écrites en dur (`#1E88E5`, `#43A047`, badge « Mode hors ligne ») par les ressources : `{DynamicResource Autoprint.Brush.Primary}`, style `Autoprint.Badge.Offline`, etc.
6. Remplacer `Autoprint.ico` par `ressources/icones/autoprint.ico`.

## Styles principaux

| Élément | Clé |
|---|---|
| Fenêtre (coins 12 px) | `Autoprint.Client.Window` (Border), `Autoprint.Client.Footer` |
| Lieu détecté (titre) | `Autoprint.Client.Place` |
| Onglets en pilule | `Autoprint.PillTabs` (Border) + `Autoprint.PillTab` (RadioButton) + `Autoprint.PillTab.Count` |
| Carte d'imprimante | `Autoprint.PrinterCard`, `Autoprint.PrinterCard.Default`, `Autoprint.PrinterGlyph`, `Autoprint.InstalledDot` |
| Favori | `Autoprint.FavoriteToggle` (ToggleButton) |
| Boutons | `Autoprint.Button.Primary`, `.Secondary`, `.Outline`, `.Text`, `.Danger`, `.Icon` |
| Interrupteur | `Autoprint.Switch` (ToggleButton) |
| Zone technique | `Autoprint.AdminZone` (ContentControl, cadre en pointillés) |
| Badges | `Autoprint.Badge.Online`, `.Offline`, `.Warning`, `.Info`, `.Archived`, `.Unknown`, `Autoprint.LabelTag` |
| Encadrés | `Autoprint.Callout.Success`, `.Warning`, `.Danger`, `.Info` |
| Textes | `Autoprint.Text`, `.Title`, `.Secondary`, `.Caption`, `.Mono`, `.Label`, `.Error` |

Les icônes restent celles de `materialDesign:PackIcon` (jeu Material Design Icons), équivalent des Material Symbols de la console.

## À vérifier à l'intégration

Ces dictionnaires n'ont pas pu être compilés sur la branche de design (pas de WPF disponible). Ils sont validés en XML et toutes les clés utilisées sont définies, mais un passage dans Visual Studio est nécessaire. Points à regarder en priorité : liste déroulante (fenêtre surgissante), interrupteur, rendu des polices.
