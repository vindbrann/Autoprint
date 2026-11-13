# Thème de la console : `autoprint-theme.css`

Thème de base : **Radzen.Blazor 12, thème « standard »** (variables vérifiées sur la version 12.2.0).
Fichier généré par `sources/build_theme.py` à partir de `tokens.json` : ne pas le modifier à la main.

## Intégration

1. Copier `autoprint-theme.css` dans `Autoprint.Web/wwwroot/css/`.
2. Copier les polices `ressources/polices/*.woff2` dans `Autoprint.Web/wwwroot/fonts/` (le CSS les cherche dans `../fonts/`).
3. Dans `wwwroot/index.html`, ajouter `<link rel="stylesheet" href="css/autoprint-theme.css" />`. L'ordre par rapport au thème Radzen n'a pas d'importance : les sélecteurs `html:root` l'emportent sur le `:root` de Radzen.
4. Remplacer `<RadzenTheme Theme="material" />` par `<RadzenTheme Theme="standard" />` dans `MainLayout.razor` et `EmptyLayout.razor`.
5. Bascule clair / sombre (`NavMenu.razor`, `MainLayout.razor`, `EmptyLayout.razor`) :
   - thèmes `standard` et `standard-dark` à la place de `material` et `material-dark` ;
   - **en même temps**, poser `data-theme="dark"` sur `<html>` en sombre et le retirer en clair (une ligne de JS dans `wwwroot/js/app.js`, appelée par interop). Sans cet attribut, les couleurs Autoprint restent en clair.
6. Retirer Bootstrap et Bootstrap Icons s'ils ne servent plus. Les icônes Material Symbols sont fournies par Radzen 12 (graisse réglée à 300 par le thème).
7. Remplacer progressivement les `style="…"` par les classes `ap-*` (voir ci-dessous et les maquettes).

## Correspondance des composants

| Maquette | Radzen |
|---|---|
| Bouton principal | `RadzenButton ButtonStyle="ButtonStyle.Primary"` |
| Bouton secondaire (fond blanc, contour) | `ButtonStyle="ButtonStyle.Base" Variant="Variant.Outlined"` |
| Bouton texte (Exporter en CSV, Diagnostic) | `ButtonStyle="ButtonStyle.Primary" Variant="Variant.Text"` |
| Bouton danger | `ButtonStyle="ButtonStyle.Danger"` |
| Bouton icône seule | `ButtonStyle="ButtonStyle.Base" Variant="Variant.Text" Icon="edit"` |
| Badge de statut | `<span class="ap-status-badge ap-status-badge--online"><RadzenIcon Icon="check_circle" />En ligne</span>` (ou `RadzenBadge Shade="Shade.Lighter"`) |
| Encadré | `RadzenAlert Shade="Shade.Lighter"` |
| Étiquette d'inventaire | `<span class="ap-label-tag">10.12.4.21</span>` |
| Carte d'indicateur | `ap-kpi-card` (+ `--danger` / `--warning` si action requise) |
| Grille | `RadzenDataGrid` sans `GridLines`, densité par défaut : lignes d'environ 36 px |
| Menu des sections (Paramètres) | `ap-subnav-layout` + `ap-subnav` ; ajouter `is-collapsed` pour la réduire |
| Diagnostic | icône `stethoscope` |

## Menus réductibles

- Menu principal : `RadzenSidebar` + `RadzenPanelMenu` ; en mode réduit, afficher seulement les icônes (`DisplayStyle="MenuItemDisplayStyle.Icon"` ou équivalent) et ajouter dans chaque lien un `<span class="ap-rail-tip">Nom</span>` pour la bulle au survol.
- Mémoriser l'état réduit / ouvert par utilisateur, comme le thème (stockage du navigateur).

## Points de vigilance

- Radzen Standard impose un fond de page décoré et des boutons de 36 px : le thème les corrige (fond uni, 28 / 32 / 40 px).
- Le thème ne contient aucun JavaScript ni ressource externe (CSP respectée).
- Vérifier visuellement après intégration : fenêtres de dialogue, notifications, sélecteur de date.
