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
- **Densité** : contrôles de 32 px, lignes de grille de 36 px (32 en compact), menu de 248 px (au lieu de 280).
- **Textes** : casse de phrase partout (fin des majuscules espacées « PARC ACTIF », « EXPORT CSV »), pas de point final sur les libellés courts.

## Écarts proposés par rapport à l'existant

- Cartes d'indicateurs sans bordure colorée : seul un chiffre qui demande une action prend la couleur de son statut.
- « Répartition par modèle » : barre empilée avec une légende chiffrée au lieu d'un anneau, plus lisible avec des dizaines de modèles. À valider.
- Boutons d'action de la grille : un bouton principal (« Ajouter ») ; les autres en secondaire ou en texte, au lieu de quatre couleurs différentes.
- Clés ajoutées à `tokens.json` par rapport au format de référence : `brand`, `text-muted`, `border-strong` (contour des champs à 3:1), tailles complémentaires.

## Points à arbitrer

- Valider la barre empilée à la place de l'anneau.
- Confirmer le thème de base Radzen Standard.

## Crédits

Planche réalisée avec la méthode **unslop** de Damir Sokolovsky (@heydamir), CC BY 4.0.
