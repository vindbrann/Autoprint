# Notes de conception : charte Autoprint v1

## Direction

| | |
|---|---|
| Produit | Déployer et suivre les imprimantes réseau d'un parc Windows |
| Caractère | Fiable, précis, posé |
| Univers | Étiquettes d'inventaire, salle serveur, papier A4 |
| Moments | L'état du parc au tableau de bord ; la carte d'imprimante côté client |
| Signature | L'étiquette d'inventaire en chasse fixe (adresse IP, code inventaire, version) |

## Choix retenus

- **Couleur d'accent** : bleu dérivé de la punaise du logo (`brand` #4f8dee). Assombri en `primary` #1d5fcf en clair pour tenir 4,5:1 avec du texte blanc ; éclairci en #5b9bf5 en sombre, avec texte foncé. C'est la **seule** couleur de l'interface hors statuts.
- **Neutres** : gris légèrement froids, identiques pour les fonds, surfaces, filets et textes.
- **Statuts** : succès, avertissement, erreur, info, neutre. Toujours couleur + icône + texte.
- **Thème de base Radzen 12** : **Standard** (plus compact que Material, adapté aux grandes grilles). Validé ; variables relevées sur Radzen 12.2.0.
- **Police** : IBM Plex Sans (400, 500, 600) et IBM Plex Mono (400, 500), SIL Open Font License 1.1. `.woff2` (console) dans `ressources/polices/`, `.ttf` (WPF) dans `ressources/polices/ttf/`. Les fichiers Medium et SemiBold portent le nom de famille typographique « IBM Plex Sans » : WPF les regroupe en une seule famille.
- **Icônes** : Material Symbols Outlined, graisse 300, licence Apache 2.0. Natif dans Radzen ; pour WPF, les mêmes glyphes existent dans Material Design Icons (PackIcon) ou en chemins SVG.
- **Logo** : on garde la punaise avec l'imprimante, redessinée à plat (sans dégradé). Symbole (`autoprint-symbole.svg`), symbole simplifié pour 16 à 24 px (`autoprint-symbole-petit.svg`), logo horizontal pour fond clair et fond sombre (nom vectorisé), PNG 512 px.
- **Diagnostic** : icône stéthoscope (`stethoscope`), car le diagnostic couvre l'état, les erreurs, le toner et le réseau. L'icône réseau (`lan`) reste réservée aux profils SNMP et aux tests de connexion.
- **Menus réductibles** : le menu principal et le menu des sections ont chacun un bouton pour se réduire en barre d'icônes, avec le nom au survol. Le choix est à mémoriser par utilisateur, comme le thème, et ne doit pas changer quand on passe d'une page à l'autre : dans la maquette, chaque écran est une page séparée et le menu se rouvre à chaque clic, ce qui ne sera pas le cas dans la console (application d'une seule page). En mode réduit, une bulle aux couleurs de la charte affiche le nom de la rubrique au survol et au focus clavier, sans délai (pas d'infobulle native du navigateur), avec le nombre d'éléments à traiter pour Modèles et Pilotes. Les pastilles d'alerte restent visibles en point rouge sur l'icône. Sous 1024 px, les deux menus se réduisent tout seuls ; sous 640 px, le menu des sections devient une liste déroulante. Utile sur un portable de 14 pouces (souvent 1280 à 1366 px de large utiles avec la mise à l'échelle de Windows).
- **Barres de défilement** : fines et discrètes partout (couleur du texte discret, sans flèches), jamais l'ascenseur Windows par défaut. Les barres d'onglets n'en affichent pas du tout. À reprendre dans `autoprint-theme.css` et dans les styles `ScrollViewer` des dictionnaires XAML.
- **Densité** : contrôles de 32 px, lignes de grille de 36 px (32 en compact), menu de 248 px (au lieu de 280).
- **Textes** : casse de phrase partout (fin des majuscules espacées « PARC ACTIF », « EXPORT CSV »), pas de point final sur les libellés courts.

## Écarts proposés par rapport à l'existant

- Cartes d'indicateurs sans bordure colorée : seul un chiffre qui demande une action prend la couleur de son statut.
- « Répartition par modèle » : barre empilée avec une légende chiffrée au lieu d'un anneau, plus lisible avec des dizaines de modèles. Validé, avec une légende simple.
- Boutons d'action de la grille : un bouton principal (« Ajouter ») ; les autres en secondaire ou en texte, au lieu de quatre couleurs différentes.
- Clés ajoutées à `tokens.json` par rapport au format de référence : `brand`, `text-muted`, `border-strong` (contour des champs à 3:1), tailles complémentaires.

## Maquettes (étape 3)

Ouvrir `maquettes/index.html` dans un navigateur. Chaque page a une barre de maquette (hors application) pour passer d'un écran à l'autre et forcer le thème clair ou sombre ; par défaut, le thème suit celui du système.

| Interface | Fichier | Écrans |
|---|---|---|
| Console | `maquettes/console/*.html` | Connexion, Tableau de bord, Imprimantes, Fiche imprimante (modale), Paramètres |
| Client | `maquettes/client/client.html` | Disponibles (installée et par défaut, en cours, à installer), État vide (hors ligne), Options (zone technique) |
| Installeur | `maquettes/installeur/installeur.html` | Vérification du système, Base de données (SQL Server), Fin avec « Action requise » |
| Référence | `maquettes/composants/planche-composants.html` | Planche de composants |

Propositions visibles dans les maquettes :

- **Connexion** : formulaire centré dans une carte, sur le fond de page. Pas d'illustration (le plan d'étage proposé en v1 a été retiré : aucune fonction « plan » dans l'application).
- **Tableau de bord** : « État du service » devient un bandeau sous le titre ; la répartition par modèle est une barre empilée suivie d'une légende simple (pastille, nom, nombre, part), sans barres de progression par ligne.
- **Imprimantes** : filtres en pastilles (Lieu, Statut, Modèle), actions groupées quand des lignes sont sélectionnées, lieu et détail sur deux lignes, colonnes ajustables. Le code inventaire passe dans le choix des colonnes. Pas de niveau de toner dans la grille principale : le toner bas apparaît seulement comme statut. Sur chaque ligne, les boutons Modifier et Diagnostic sont côte à côte.
- **Paramètres** : les 8 onglets sont remplacés par un menu des sections, à gauche dans la page (le menu principal ne change pas) ; un titre et une description rappellent la section ouverte ; chaque réglage a son titre et son explication à gauche, ses champs à droite ; la restauration est isolée en zone sensible ; une barre d'enregistrement apparaît en bas dès qu'une modification est en attente.
- **Client** : le lieu détecté sert de titre ; une carte par imprimante, l'imprimante par défaut est mise en avant ; la zone technique est encadrée en pointillés.
- **Installeur** : fini l'en-tête noir ; les 7 étapes sont listées à gauche, l'étape en cours est mise en avant, la barre de boutons reste en bas. Validé.

Données de test fictives (noms, adresses, utilisateurs).

## Fichiers de thème (étape 4)

| Interface | Fichiers | Guide |
|---|---|---|
| Console | `theme/console/autoprint-theme.css` | `theme/console/LISEZMOI.md` |
| Client | `theme/client/Autoprint.Theme.xaml`, `Autoprint.Colors.Light.xaml`, `Autoprint.Colors.Dark.xaml`, `AutoprintTheme.cs` | `theme/client/LISEZMOI.md` |
| Installeur | `theme/installeur/Autoprint.Installer.Theme.xaml` | `theme/installeur/LISEZMOI.md` |

Ressources : `ressources/icones/autoprint.ico` (16 à 256 px), `favicon-32.png`, `favicon-192.png`, `favicon.svg`, `ressources/msi/banniere-493x58.bmp`, `ressources/msi/accueil-493x312.bmp` (BMP 24 bits). Aperçu : `maquettes/ressources/ressources.html`.

Choix d'intégration :

- **Console** : le thème part de Radzen 12 « standard » (variables relevées sur la 12.2.0) et surcharge environ 300 variables `--rz-*` à partir des jetons `--ap-*`. Il corrige aussi ce que Radzen impose en dur : fond de page décoré, boutons de 36 px, contour du bouton secondaire. Vérifié dans un navigateur avec les vraies feuilles Radzen 12, en clair et en sombre.
- **Bascule sombre de la console** : Radzen change de feuille (`standard` ↔ `standard-dark`), ce que le CSS ne peut pas détecter. Il faut poser `data-theme="dark"` sur `<html>` en même temps : une ligne de JavaScript dans `app.js`, appelée par interop.
- **Client et installeur** : modèles de contrôles communs, sans dépendance (boutons, champs, liste déroulante, cases, barre de progression, barres de défilement fines). Le client garde MaterialDesign pour le reste, avec la palette bleue Autoprint ; ses couleurs Autoprint sont dans deux dictionnaires échangés selon le thème Windows (`AutoprintTheme.Apply()`, à appeler depuis `App.UpdateTheme()` qui existe déjà). L'installeur est autonome et applique la charte aux contrôles standard par styles implicites.
- **Limite** : les XAML n'ont pas pu être compilés sur cette branche (pas de WPF disponible). Ils sont validés en XML, toutes les clés utilisées sont définies et l'ordre des ressources statiques est vérifié, mais un passage dans Visual Studio est nécessaire à l'intégration.

## Génération

Les fichiers de thème sont produits par `sources/build_theme.py` à partir de `tokens.json` (et du gabarit `sources/theme/autoprint-theme.src.css`). Les pages sont produites par `sources/build_maquettes.py` à partir de `sources/autoprint.src.css` (jetons et composants `ap-*`) et des gabarits de `sources/maquettes/`. Les icônes sont copiées en ligne dans chaque page. Ne pas modifier directement les fichiers de `maquettes/` : modifier les sources, puis relancer le script.

## Statut

Charte v1 validée le 09/11/2026 : direction, jetons, maquettes, thème de base Radzen « Standard », fichiers de thème et ressources. Prochaines étapes (voir `05-mode-operatoire.md`) : passage de la console à Radzen 12 sur `chore/radzen-12`, fusion dans `master`, puis intégration sur cette branche.

## Crédits

Planche réalisée avec la méthode **unslop** de Damir Sokolovsky (@heydamir), CC BY 4.0.
