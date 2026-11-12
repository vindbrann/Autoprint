# Dépôt du template graphique

Déposer ici les livrables décrits dans [../04-livrables-et-integration.md](../04-livrables-et-integration.md), au fur et à mesure. Rien dans ce dossier n'est utilisé par l'application tant que l'intégration n'est pas faite.

```
livrables/
├── tokens.json                      jetons de design (source de vérité)
├── NOTES.md                         choix, intégration, points à arbitrer
├── maquettes/
│   ├── index.html                   accueil des maquettes
│   ├── console/                     connexion, tableau de bord, imprimantes, fiche, paramètres (clair + sombre)
│   ├── client/                      fenêtre principale : disponibles, état vide, options (clair + sombre)
│   ├── installeur/                  vérification du système, base de données, fin
│   ├── composants/                  planche de composants
│   ├── ressources/                  aperçu des ressources graphiques
│   └── commun/autoprint.css         feuille commune des maquettes
├── theme/
│   ├── console/autoprint-theme.css               + LISEZMOI.md
│   ├── client/Autoprint.Theme.xaml               + Autoprint.Colors.Light/Dark.xaml, AutoprintTheme.cs, LISEZMOI.md
│   └── installeur/Autoprint.Installer.Theme.xaml + LISEZMOI.md
├── ressources/
│   ├── logo/                        SVG (symbole, symbole simplifié, horizontal clair et sombre) + PNG 512
│   ├── icones/                      autoprint.ico (16 à 256 px), favicon 32 / 192 / SVG
│   ├── msi/                         banniere-493x58.bmp, accueil-493x312.bmp
│   └── polices/                     .woff2, ttf/*.ttf + licence OFL
└── sources/                         générateurs (build_theme.py, build_maquettes.py) et gabarits : à modifier à la place des fichiers générés
```

Un fichier `NOTES.md` est bienvenu : thème de base Radzen choisi, polices et icônes retenues (avec licences), points à arbitrer.
