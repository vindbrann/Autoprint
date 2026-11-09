# Notes de conception : charte Autoprint v1

## Direction

| | |
|---|---|
| Produit | Déployer et suivre les imprimantes réseau d'un parc Windows |
| Caractère | Fiable, précis, posé |
| Univers | Plan d'étage, étiquettes d'inventaire, papier A4 |
| Moments | L'état du parc au tableau de bord ; la carte d'imprimante côté client |
| Signature | L'étiquette d'inventaire en chasse fixe (adresse IP, code inventaire, version) |

## Choix retenus

- **Couleur d'accent** : bleu dérivé de la punaise du logo (`brand` #4f8dee). Assombri en `primary` #1d5fcf en clair pour tenir 4,5:1 avec du texte blanc ; éclairci en #5b9bf5 en sombre, avec texte foncé. C'est la **seule** couleur de l'interface hors statuts.
- **Neutres** : gris légèrement froids, identiques pour les fonds, surfaces, filets et textes.
- **Statuts** : succès, avertissement, erreur, info, neutre. Toujours couleur + icône + texte.
- **Thème de base Radzen 12** : **Standard** (plus compact que Material, adapté aux grandes grilles). Noms exacts des variables `--rz-*` à relever dans `standard-base.css` de Radzen 12 au moment d'écrire `autoprint-theme.css`.
- **Police** : IBM Plex Sans (400, 500, 600) et IBM Plex Mono (400, 500), SIL Open Font License 1.1. Fichiers `.woff2` dans `ressources/polices/`. Les `.ttf` pour WPF seront produits à l'étape des fichiers de thème.
- **Icônes** : Material Symbols Outlined, graisse 300, licence Apache 2.0. Natif dans Radzen ; pour WPF, les mêmes glyphes existent dans Material Design Icons (PackIcon) ou en chemins SVG.
- **Logo** : on garde la punaise avec l'imprimante. `ressources/logo/autoprint-symbole.svg` en est une version vectorielle à plat (sans dégradé), lisible à 16 px. Versions horizontales et PNG à venir.
- **Diagnostic** : icône stéthoscope (`stethoscope`), car le diagnostic couvre l'état, les erreurs, le toner et le réseau. L'icône réseau (`lan`) reste réservée aux profils SNMP et aux tests de connexion.
- **Barres de défilement** : fines et discrètes partout (couleur du texte discret, sans flèches), jamais l'ascenseur Windows par défaut. Les barres d'onglets n'en affichent pas du tout. À reprendre dans `autoprint-theme.css` et dans les styles `ScrollViewer` des dictionnaires XAML.
- **Densité** : contrôles de 32 px, lignes de grille de 36 px (32 en compact), menu de 248 px (au lieu de 280).
- **Textes** : casse de phrase partout (fin des majuscules espacées « PARC ACTIF », « EXPORT CSV »), pas de point final sur les libellés courts.

## Écarts proposés par rapport à l'existant

- Cartes d'indicateurs sans bordure colorée : seul un chiffre qui demande une action prend la couleur de son statut.
- « Répartition par modèle » : barre empilée avec une légende chiffrée au lieu d'un anneau, plus lisible avec des dizaines de modèles. À valider.
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

- **Connexion** : écran partagé, avec à gauche le plan d'étage d'un lieu et ses imprimantes (l'univers du produit), à droite le formulaire.
- **Tableau de bord** : « État du service » devient un bandeau sous le titre ; la répartition par modèle est une barre empilée suivie d'une liste classée.
- **Imprimantes** : filtres en pastilles (Lieu, Statut, Modèle), actions groupées quand des lignes sont sélectionnées, lieu et détail sur deux lignes, colonnes ajustables. Le code inventaire passe dans le choix des colonnes. Pas de niveau de toner dans la grille principale : le toner bas apparaît seulement comme statut. Sur chaque ligne, les boutons Modifier et Diagnostic sont côte à côte.
- **Paramètres** : chaque réglage a son titre et son explication à gauche, ses champs à droite ; la restauration est isolée en zone sensible ; une barre d'enregistrement apparaît en bas dès qu'une modification est en attente.
- **Client** : le lieu détecté sert de titre ; une carte par imprimante, l'imprimante par défaut est mise en avant ; la zone technique est encadrée en pointillés.
- **Installeur** : fini l'en-tête noir ; les 7 étapes sont listées à gauche, l'étape en cours est mise en avant, la barre de boutons reste en bas.

Données de test fictives (noms, adresses, utilisateurs).

## Génération

Les pages sont produites par `sources/build_maquettes.py` à partir de `sources/autoprint.src.css` (jetons et composants `ap-*`) et des gabarits de `sources/maquettes/`. Les icônes sont copiées en ligne dans chaque page. Ne pas modifier directement les fichiers de `maquettes/` : modifier les sources, puis relancer le script.

## Points à arbitrer

- Valider la barre empilée à la place de l'anneau.
- Valider le plan d'étage sur l'écran de connexion.
- Valider le rail d'étapes de l'installeur à la place de l'en-tête sombre.
- Confirmer le thème de base Radzen Standard.

## Crédits

Planche réalisée avec la méthode **unslop** de Damir Sokolovsky (@heydamir), CC BY 4.0.
