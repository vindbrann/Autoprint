# Police des rapports PDF — Autoprint Sans

| Fichier | Rôle |
|---|---|
| `AutoprintSans-Regular.ttf`, `AutoprintSans-Bold.ttf` | Polices embarquées dans `Autoprint.Server.dll` et utilisées par `PdfReportWriter` |
| `OFL-LICENSE.txt` | Licence d'origine (SIL Open Font License 1.1), copiée à côté du site publié (`Licenses\`) |

## Origine

Sous-ensemble de **Liberation Sans 2.1.5** (Red Hat, d'après Arimo de Google), sous licence **SIL Open Font License 1.1**.
Téléchargée le 04/11/2026 depuis la page de version officielle :
`https://github.com/liberationfonts/liberation-fonts/files/7261482/liberation-fonts-ttf-2.1.5.tar.gz`
(SHA-256 de l'archive : `7191c669bf38899f73a2094ed00f7b800553364f90e2637010a69c0e268f25d0`).

Pourquoi : SkiaSharp intègre toujours la police **entière** dans le PDF. Avec Arial (~1 Mo par style), un rapport pesait 1,1 Mo ;
avec ce sous-ensemble (caractères latins, ponctuation, €, flèches), il pèse quelques dizaines de Ko. Les largeurs de caractères
sont identiques à celles d'Arial : la mise en page est la même.

## Licence : pourquoi « Autoprint Sans »

« Liberation », « Arimo », « Tinos » et « Cousine » sont des *noms de police réservés* par la licence OFL : une version modifiée
(un sous-ensemble en est une) doit porter un autre nom. Les mentions de copyright, de marque et la licence d'origine sont conservées
dans les polices et dans `OFL-LICENSE.txt`. La police n'est jamais vendue seule.

## Régénérer

```bash
python -m venv venv-fonttools
venv-fonttools/Scripts/python -m pip install fonttools
venv-fonttools/Scripts/python tools/fonts/make_autoprint_sans.py <dossier liberation-fonts-ttf-x.y.z> Autoprint.Server/Resources/Fonts
```

Pour ajouter des caractères (autre alphabet), compléter `UNICODES` dans le script.
