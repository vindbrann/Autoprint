"""Réduit Liberation Sans aux caractères latins et la renomme « Autoprint Sans » (exigence OFL : nom réservé)."""
import sys, os
from fontTools import subset
from fontTools.ttLib import TTFont

src_dir, out_dir = sys.argv[1], sys.argv[2]
os.makedirs(out_dir, exist_ok=True)
UNICODES = ("U+0020-007E,U+00A0-017F,U+0192,U+02C6-02DC,U+2010-2027,U+2030-203A,U+20AC,U+2116,U+2122,"
            "U+2190-2193,U+2212,U+2264-2265,U+25CF")

for style, src in (("Regular", "LiberationSans-Regular.ttf"), ("Bold", "LiberationSans-Bold.ttf")):
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
        elif rec.nameID == 3: name.setName(f"Autoprint: {ps} 1.0", 3, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 4: name.setName(full, 4, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 6: name.setName(ps, 6, rec.platformID, rec.platEncID, rec.langID)
    # Le nom réservé ne doit plus apparaître nulle part dans les noms de police (champs qui composent le nom)
    for rec in name.names:
        if rec.nameID in (1, 2, 3, 4, 6, 16, 17, 18, 21, 22) and ("Liberation" in rec.toUnicode() or "Arimo" in rec.toUnicode()):
            raise SystemExit(f"Nom réservé encore présent : id {rec.nameID} = {rec.toUnicode()}")
    out = os.path.join(out_dir, f"{ps}.ttf")
    font.save(out)
    print(out, os.path.getsize(out), "octets,", len(font.getGlyphOrder()), "glyphes")
