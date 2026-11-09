# Dépôt du template graphique

Déposer ici les livrables décrits dans [../04-livrables-et-integration.md](../04-livrables-et-integration.md), au fur et à mesure. Rien dans ce dossier n'est utilisé par l'application tant que l'intégration n'est pas faite.

```
livrables/
├── tokens.json                      jetons de design (obligatoire)
├── maquettes/
│   ├── console/                     HTML ou PNG : connexion, tableau de bord, imprimantes, fiche, paramètres (clair + sombre)
│   ├── client/                      fenêtre principale, état vide, options (clair + sombre)
│   ├── installeur/                  vérification du système, base de données, fin
│   └── composants/                  planche de composants
├── theme/
│   ├── console/autoprint-theme.css               (souhaité)
│   ├── client/Autoprint.Theme.xaml               (souhaité)
│   └── installeur/Autoprint.Installer.Theme.xaml (souhaité)
└── ressources/
    ├── logo/                        SVG (horizontal, symbole ; fond clair et sombre) + PNG 512
    ├── icones/                      autoprint.ico (16 à 256 px), favicon 32 / 192
    ├── msi/                         banniere-493x58.bmp, accueil-493x312.bmp
    └── polices/                     .woff2 / .ttf + fichier de licence
```

Un fichier `NOTES.md` est bienvenu : thème de base Radzen choisi, polices et icônes retenues (avec licences), points à arbitrer.
