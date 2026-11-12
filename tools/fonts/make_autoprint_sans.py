"""Réduit IBM Plex Sans aux caractères latins et la renomme « Autoprint Sans » (exigence OFL : « Plex » est un nom réservé).

Usage : python make_autoprint_sans.py <dossier des TTF IBM Plex Sans> <dossier de sortie>
Les TTF sont dans le dépôt (Docs/Charte-graphique/livrables/ressources/polices/ttf).
"""
import sys, os
from fontTools import subset
from fontTools.ttLib import TTFont

src_dir, out_dir = sys.argv[1], sys.argv[2]
os.makedirs(out_dir, exist_ok=True)
UNICODES = ("U+0020-007E,U+00A0-017F,U+0192,U+02C6-02DC,U+2010-2027,U+2030-203A,U+20AC,U+2116,U+2122,"
            "U+2190-2193,U+2212,U+2264-2265,U+25CF")
RESERVED = ("Plex",)

# « Bold » des rapports = SemiBold (600), la graisse forte utilisée par la console
for style, src in (("Regular", "IBMPlexSans-Regular.ttf"), ("Bold", "IBMPlexSans-SemiBold.ttf")):
    opts = subset.Options()
    opts.layout_features = ["kern", "liga"]
    opts.name_IDs = ["*"]
    opts.name_languages = ["*"]
    opts.notdef_outline = True
    opts.hinting = False
    font = TTFont(os.path.join(src_dir, src))
    sub = subset.Subsetter(opts)
    sub.populate(unicodes=subset.parse_unicodes(UNICODES))
    sub.subset(font)

    family, full, ps = "Autoprint Sans", f"Autoprint Sans {style}", f"AutoprintSans-{style}"
    name = font["name"]
    for rec in list(name.names):
        if rec.nameID in (1, 16): name.setName(family, rec.nameID, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID in (2, 17): name.setName(style, rec.nameID, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 3: name.setName(f"Autoprint: {ps} 2.0", 3, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 4: name.setName(full, 4, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 6: name.setName(ps, 6, rec.platformID, rec.platEncID, rec.langID)
    # Graisse annoncée cohérente avec le style (SemiBold source -> « Bold » pour Skia)
    if style == "Bold":
        font["OS/2"].usWeightClass = 700
        font["OS/2"].fsSelection = (font["OS/2"].fsSelection & ~0x40) | 0x20
        font["head"].macStyle |= 0x1
    # Le nom réservé ne doit plus apparaître dans les champs qui composent le nom de la police
    # (copyright id 0 et marque id 7 sont conservés, comme l'exige la licence)
    for rec in name.names:
        if rec.nameID in (1, 2, 3, 4, 6, 16, 17, 18, 21, 22, 25) and any(r in rec.toUnicode() for r in RESERVED):
            name.removeNames(nameID=rec.nameID) if rec.nameID in (21, 22, 25) else None
    for rec in name.names:
        if rec.nameID in (1, 2, 3, 4, 6, 16, 17, 18, 21, 22, 25) and any(r in rec.toUnicode() for r in RESERVED):
            raise SystemExit(f"Nom réservé encore présent : id {rec.nameID} = {rec.toUnicode()}")
    out = os.path.join(out_dir, f"{ps}.ttf")
    font.save(out)
    print(out, os.path.getsize(out), "octets,", len(font.getGlyphOrder()), "glyphes")
