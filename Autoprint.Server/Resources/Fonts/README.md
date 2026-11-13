# Police des rapports PDF — Autoprint Sans

| Fichier | Rôle |
|---|---|
| `AutoprintSans-Regular.ttf`, `AutoprintSans-Bold.ttf` | Polices embarquées dans `Autoprint.Server.dll` et utilisées par `PdfReportWriter` |
| `OFL-LICENSE.txt` | Licence d'origine (SIL Open Font License 1.1), copiée à côté du site publié (`Licenses\`) |

## Origine

Sous-ensemble d'**IBM Plex Sans 3.005** (IBM Corp.), la police de la charte graphique, sous licence **SIL Open Font License 1.1**.
« Regular » vient d'`IBMPlexSans-Regular.ttf`, « Bold » d'`IBMPlexSans-SemiBold.ttf` (graisse 600, la graisse forte de la console).
Fichiers source : `Docs/projet/Charte-graphique/livrables/ressources/polices/ttf` (les mêmes que la console et les installeurs).

Pourquoi un sous-ensemble : SkiaSharp intègre toujours la police **entière** dans le PDF. Avec Arial (~1 Mo par style), un rapport
pesait 1,1 Mo ; avec ce sous-ensemble (caractères latins, ponctuation, €, flèches), il pèse quelques dizaines de Ko.

Jusqu'au 12/11/2026, Autoprint Sans était un sous-ensemble de Liberation Sans (largeurs d'Arial) ; remplacé pour que les rapports
aient la même police que la console.

## Licence : pourquoi « Autoprint Sans »

« Plex » est un *nom de police réservé* par la licence OFL : une version modifiée (un sous-ensemble en est une) doit porter un autre nom.
Le copyright et la mention de marque d'IBM sont conservés dans les polices, la licence d'origine dans `OFL-LICENSE.txt`.
La police n'est jamais vendue seule.

## Régénérer

```bash
python -m venv venv-fonttools
venv-fonttools/Scripts/python -m pip install fonttools
venv-fonttools/Scripts/python tools/fonts/make_autoprint_sans.py Docs/projet/Charte-graphique/livrables/ressources/polices/ttf Autoprint.Server/Resources/Fonts
```

Pour ajouter des caractères (autre alphabet), compléter `UNICODES` dans le script.
