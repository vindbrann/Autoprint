# Livrables attendus et intégration

## 1. Jetons de design : `tokens.json` (obligatoire)

C'est la **source de vérité**. Un seul fichier, valeurs pour le **clair** et le **sombre**, noms en anglais, en kebab-case.

Structure attendue (les valeurs ci-dessous ne sont que des exemples de format) :

```json
{
  "meta": { "name": "Autoprint", "version": "1.0", "base-radzen-theme": "material" },
  "color": {
    "light": {
      "primary": "#…", "on-primary": "#…", "primary-hover": "#…", "primary-subtle": "#…",
      "secondary": "#…", "on-secondary": "#…",
      "background": "#…", "surface": "#…", "surface-raised": "#…", "sidebar": "#…", "on-sidebar": "#…",
      "border": "#…", "divider": "#…",
      "text": "#…", "text-secondary": "#…", "text-disabled": "#…", "link": "#…",
      "success": "#…", "on-success": "#…", "success-subtle": "#…",
      "warning": "#…", "on-warning": "#…", "warning-subtle": "#…",
      "danger":  "#…", "on-danger":  "#…", "danger-subtle":  "#…",
      "info":    "#…", "on-info":    "#…", "info-subtle":    "#…",
      "neutral": "#…",
      "focus-ring": "#…",
      "chart": ["#…", "#…", "#…", "#…", "#…", "#…"]
    },
    "dark": { "…": "mêmes clés que light" }
  },
  "font": {
    "family": "…", "family-mono": "…",
    "size":   { "xs": "11px", "sm": "12px", "base": "14px", "lg": "16px", "h6": "…", "h5": "…", "h4": "…", "h3": "…", "h2": "…", "h1": "…" },
    "weight": { "regular": 400, "medium": 500, "semibold": 600, "bold": 700 },
    "line-height": { "tight": 1.2, "base": 1.5 }
  },
  "space":  { "1": "4px", "2": "8px", "3": "12px", "4": "16px", "5": "24px", "6": "32px", "7": "48px" },
  "radius": { "sm": "…", "md": "…", "lg": "…", "pill": "999px" },
  "shadow": { "sm": "…", "md": "…", "lg": "…" },
  "size":   { "control-height": "…", "control-height-sm": "…", "sidebar-width": "…", "grid-row-height": "…" },
  "motion": { "fast": "120ms", "base": "200ms", "easing": "…" }
}
```

Règles :
- chaque couleur de texte a sa couleur de fond associée (`on-*`) avec un contraste **AA** vérifié ;
- les valeurs `*-subtle` servent aux fonds de badges et d'encadrés (avertissement, erreur, information) ;
- la palette `chart` reste **distinguable par un daltonien** (pas uniquement rouge et vert).

## 2. Maquettes (obligatoire)

- **Format** : HTML statique (de préférence, consultable dans un navigateur, sans dépendance externe) ou images PNG 1920×1080 pour la console, à la taille réelle de la fenêtre pour le client et l'installeur.
- **Clair et sombre** pour la console et le client.
- **Écrans minimum** (★ dans les fiches) :
  - **console** : Connexion, Tableau de bord, Imprimantes (grille + barre d'outils), Fiche imprimante (modale), Paramètres (onglets + formulaire) ;
  - **client** : fenêtre principale sur l'onglet Disponibles (avec une imprimante installée et favorite, une en cours d'installation, une à installer), état vide, Options ;
  - **installeur** : Vérification du système (un prérequis OK, un manquant, un trop ancien), Base de données (SQL Server), Fin avec « Action requise ».
- **Planche de composants** : boutons (toutes variantes et états : normal, survol, focus, désactivé), champs (normal, focus, erreur, désactivé), listes déroulantes, cases et interrupteurs, onglets, badges de statut, alertes, notifications, grille (en-tête, ligne, ligne survolée et sélectionnée, pagination), modale, cartes d'indicateurs, menu latéral (normal, actif, survol, badge).

## 3. Fichiers de thème prêts à intégrer (souhaités)

| Interface | Fichier | Contenu |
|---|---|---|
| Console | `autoprint-theme.css` | surcharge des variables `--rz-*` du thème de base Radzen 12 choisi, pour le clair (`:root` ou classe du thème clair) et le sombre ; plus quelques classes utilitaires (`ap-page-header`, `ap-kpi-card`, `ap-status-badge--online`…) pour remplacer les styles écrits en dur |
| Client | `Autoprint.Theme.xaml` | `ResourceDictionary` : couleurs et pinceaux (clés `Autoprint.Color.*` / `Autoprint.Brush.*`) pour clair et sombre, palette MaterialDesign (primaire / secondaire), styles des onglets en pilule, des cartes d'imprimante et des pastilles |
| Installeur | `Autoprint.Installer.Theme.xaml` | `ResourceDictionary` autonome (sans MaterialDesign) : couleurs, styles de `Button` (principal, secondaire), `TextBox`, `PasswordBox`, `ComboBox`, `RadioButton`, `CheckBox`, `ProgressBar`, encadrés succès / avertissement / erreur, en-tête |

Si ces fichiers ne sont pas fournis, l'équipe de développement les produit à partir de `tokens.json` et des maquettes.

## 4. Ressources graphiques

| Ressource | Formats et tailles |
|---|---|
| Logo | **SVG** (version horizontale avec le nom, version symbole seul), variantes pour fond clair et fond sombre ; PNG 512 px de secours |
| Icône d'application (client, installeur, fenêtres) | `.ico` multi-tailles : **16, 20, 24, 32, 40, 48, 64, 256 px**. Les petites tailles doivent rester lisibles (l'icône vit dans la zone de notification) |
| Favicon de la console | PNG 32 et 192 px (ou SVG) |
| Images de l'installeur MSI | BMP 24 bits **493×58** (bandeau) et **493×312** (accueil / fin) : voir [03-installeur-serveur.md](03-installeur-serveur.md) |
| Police(s) | fichiers `.woff2` (console) et `.ttf` / `.otf` (WPF), avec leur **licence** |
| Icônes | nom du jeu retenu, et sa licence ; fichiers SVG si le jeu n'est pas Material Symbols / Material Design Icons |
| Illustrations d'état vide (facultatif) | SVG monochromes, adaptables au thème |

## 5. Ce qui ne change pas (hors périmètre)

- les **fonctionnalités** et les **enchaînements** ;
- les **rapports PDF / CSV** et les **e-mails** générés par le serveur (une déclinaison pourra être faite plus tard) ;
- les **textes** sont modifiables si c'est justifié, mais en restant en français et en informant le développement.

## 6. Intégration (côté développement, pour information)

1. **Console** :
   - ajouter `autoprint-theme.css`, les polices et les icônes dans `Autoprint.Web/wwwroot` ;
   - choisir le thème de base Radzen ;
   - remplacer progressivement les ~930 `style="…"` par les classes du template ;
   - retirer Bootstrap si inutile.
2. **Client** : fusionner `Autoprint.Theme.xaml` dans `App.xaml`, remplacer la palette Grey / Lime et les couleurs en dur, intégrer la police et l'icône.
3. **Installeur** : fusionner `Autoprint.Installer.Theme.xaml`, retirer les couleurs en dur de `MainWindow.xaml`, intégrer les BMP dans les deux projets WiX.
4. **Recette** :
   - contrôles à l'écran (clair et sombre) ;
   - vérification de la CSP, sans ressource externe ;
   - tests d'interface prévus par la procédure de recette.
