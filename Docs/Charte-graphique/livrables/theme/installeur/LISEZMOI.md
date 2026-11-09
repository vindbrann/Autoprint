# Thème de l'installeur du serveur

`Autoprint.Installer.Theme.xaml` est **autonome** : couleurs (clair uniquement), polices, styles, et **styles implicites** pour les contrôles standard (Button, TextBox, PasswordBox, ComboBox, CheckBox, RadioButton, ProgressBar, ScrollBar). Il ne dépend d'aucune bibliothèque tierce.
Fichier généré par `sources/build_theme.py` à partir de `tokens.json` : ne pas le modifier à la main.

## Intégration

1. Copier le fichier dans `Autoprint.Installer.Server.UI/Theme/` (action de génération : `Page`).
2. Copier `ressources/polices/ttf/IBMPlexSans-*.ttf` et `IBMPlexMono-*.ttf` dans `Autoprint.Installer.Server.UI/Fonts/` (action de génération : `Resource`).
3. Dans `App.xaml` :
   ```xml
   <Application.Resources>
       <ResourceDictionary>
           <ResourceDictionary.MergedDictionaries>
               <ResourceDictionary Source="Theme/Autoprint.Installer.Theme.xaml" />
           </ResourceDictionary.MergedDictionaries>
       </ResourceDictionary>
   </Application.Resources>
   ```
4. Dans `MainWindow.xaml` : `Style="{StaticResource Autoprint.Installer.Window}"`, puis retirer les couleurs en dur (`#2D2D30`, `#007ACC`, `#2E7D32`, `#FFF8E1`, `#FFB300`, `#5D4037`, gris…).
5. Structure proposée (voir la maquette `maquettes/installeur/installeur.html`) : rail des étapes à gauche (`Autoprint.Installer.Rail`, `Autoprint.Step*`, `Autoprint.StepNumber*`), page à droite (`Autoprint.Installer.Page`), pied de fenêtre (`Autoprint.Installer.Footer`). Le bouton « Suivant » prend `Autoprint.Button.Primary` ; les autres boutons sont secondaires par défaut.
6. Écrans : vérification (`Autoprint.CheckList`, `Autoprint.CheckRow`, `Autoprint.Text.StatusOk` / `StatusKo`), base de données (`Autoprint.RadioCard`), installation (`ProgressBar` + `Autoprint.LogBox`), fin (`Autoprint.Installer.Title.Success`, `Autoprint.Callout.Warning` pour « Action requise »).
7. MSI (client et serveur) : remplacer `WixUIBannerBmp` par `ressources/msi/banniere-493x58.bmp` et `WixUIDialogBmp` par `ressources/msi/accueil-493x312.bmp`.

## À vérifier à l'intégration

Fichier validé en XML, toutes les clés utilisées sont définies, mais il n'a pas pu être compilé sur la branche de design. Points à regarder en priorité : liste déroulante, barre de progression indéterminée, rendu des polices.
