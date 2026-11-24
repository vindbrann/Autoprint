"""Génère les données des imprimantes simulées du labo (snmpsim), triées par OID comme l'exige le format .snmprec.

Chaque fichier sert une communauté du même nom (v1 et v2c), sur les deux adresses du simulateur (192.168.99.10 et .11) :
- lexmark.snmprec : monochrome type Lexmark (toner « -3 » + lowToner, libellé CP850, kit photoconducteur, alerte, bac en code média)
- couleur.snmprec : couleur (4 toners nommés par la table des colorants, dont un vide, récupérateur « -3 », deux bacs, alerte critique)

Types snmprec : 2 Integer, 4 OctetString, 4x OctetString en hexadécimal, 6 OID, 65 Counter32, 66 Gauge32, 67 TimeTicks.
Usage : python make_data.py  (écrit les fichiers dans data/)
"""
import os

PRT = "1.3.6.1.2.1.43"
SUP = PRT + ".11.1.1"   # prtMarkerSuppliesEntry : .3 colorant, .4 classe, .5 type, .6 description, .7 unité, .8 capacité, .9 niveau
COL = PRT + ".12.1.1"   # prtMarkerColorantEntry : .4 nom de la couleur
INP = PRT + ".8.2.1"    # prtInputEntry : .2 type, .8 unité, .9 capacité, .10 niveau, .12 support, .13 nom
ALR = PRT + ".18.1.1"   # prtAlertEntry : .2 gravité, .4 groupe, .7 code, .8 description

# Énumérations Printer MIB (RFC 3805)
CONSUMED, RECEPTACLE = 3, 4
TONER, WASTE_TONER, OPC, FUSER = 3, 4, 9, 15
PERCENT, IMPRESSIONS, SHEETS = 19, 7, 8
CRITICAL, WARNING = 3, 4


def system(descr, sysname, serial, model, uptime):
    return [
        ("1.3.6.1.2.1.1.1.0", 4, descr),
        ("1.3.6.1.2.1.1.2.0", 6, "1.3.6.1.4.1.641.1"),
        ("1.3.6.1.2.1.1.3.0", 67, uptime),
        ("1.3.6.1.2.1.1.5.0", 4, sysname),
        ("1.3.6.1.2.1.25.3.2.1.3.1", 4, model),
        ("1.3.6.1.2.1.25.3.5.1.1.1", 2, 3),          # hrPrinterStatus : idle
        (PRT + ".5.1.1.17.1", 4, serial),             # prtGeneralSerialNumber
    ]


def supply(i, colorant, cls, typ, descr, unit, maxc, level, hexdescr=False):
    return [
        (f"{SUP}.3.1.{i}", 2, colorant), (f"{SUP}.4.1.{i}", 2, cls), (f"{SUP}.5.1.{i}", 2, typ),
        (f"{SUP}.6.1.{i}", "4x" if hexdescr else 4, descr), (f"{SUP}.7.1.{i}", 2, unit),
        (f"{SUP}.8.1.{i}", 2, maxc), (f"{SUP}.9.1.{i}", 2, level),
    ]


def tray(i, name, media, maxc, level):
    return [
        (f"{INP}.2.1.{i}", 2, 4), (f"{INP}.8.1.{i}", 2, SHEETS), (f"{INP}.9.1.{i}", 2, maxc),
        (f"{INP}.10.1.{i}", 2, level), (f"{INP}.12.1.{i}", 4, media), (f"{INP}.13.1.{i}", 4, name),
    ]


def alert(i, severity, group, code, text):
    return [(f"{ALR}.2.1.{i}", 2, severity), (f"{ALR}.4.1.{i}", 2, group), (f"{ALR}.7.1.{i}", 2, code), (f"{ALR}.8.1.{i}", 4, text)]


lexmark = (
    system("Lexmark MS621 (simulee, labo Autoprint)", "SIM-LEXMARK", "SIMLX-0003", "Lexmark MS621", 4523000)
    + [("1.3.6.1.2.1.25.3.5.1.2.1", "4x", "20")]                                    # lowToner
    + [(f"{PRT}.10.2.1.4.1.1", 65, 152340)]                                         # compteur de pages
    + supply(1, 1, CONSUMED, TONER, "Black Cartridge", PERCENT, 100, -3)           # état seulement
    + supply(2, 0, CONSUMED, FUSER, "556e69748220646520667573696f6e", PERCENT, 100, 64, hexdescr=True)  # « Unit\x82 de fusion » (CP850)
    + supply(3, 0, CONSUMED, OPC, "Kit PC", PERCENT, 100, 85)
    + [(f"{COL}.4.1.1", 4, "black")]
    + tray(1, "Tray 1", "iso-a4-white", 550, 330)
    + tray(2, "MP Feeder", "iso-designated-long-envelope", 50, 0)
    + alert(1, WARNING, 11, 1101, "Toner noir bas")
)

couleur = (
    system("Imprimante couleur (simulee, labo Autoprint)", "SIM-COULEUR", "SIMCL-0004", "Labo Color MFP", 873000)
    + [("1.3.6.1.2.1.25.3.5.1.2.1", "4x", "10")]                                    # noToner
    + [(f"{PRT}.10.2.1.4.1.1", 65, 48213)]
    + [("1.3.6.1.4.1.641.2.1.5.3.0", 65, 20117)]                                     # compteur couleur propre au constructeur (profil « OID constructeur »)
    + supply(1, 1, CONSUMED, TONER, "Toner Cartridge", PERCENT, 100, 62)
    + supply(2, 2, CONSUMED, TONER, "Toner Cartridge", PERCENT, 100, 18)
    + supply(3, 3, CONSUMED, TONER, "Toner Cartridge", PERCENT, 100, 9)
    + supply(4, 4, CONSUMED, TONER, "Toner Cartridge", PERCENT, 100, 0)
    + supply(5, 0, RECEPTACLE, WASTE_TONER, "Waste Toner Box", PERCENT, 100, -3)
    + supply(6, 0, CONSUMED, FUSER, "Fuser Unit", IMPRESSIONS, 150000, 101787)
    + [(f"{COL}.4.1.1", 4, "black"), (f"{COL}.4.1.2", 4, "cyan"), (f"{COL}.4.1.3", 4, "magenta"), (f"{COL}.4.1.4", 4, "yellow")]
    + tray(1, "Tray 1", "iso-a4-white", 500, 410)
    + tray(2, "Tray 2", "na_letter_8.5x11in", 500, 60)
    + alert(1, CRITICAL, 11, 1104, "Toner jaune vide")
    + alert(2, WARNING, 8, 801, "Bac 2 presque vide")
)


def write(name, rows):
    key = lambda r: tuple(int(x) for x in r[0].split("."))
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data", name)
    with open(path, "w", encoding="ascii", newline="\n") as f:
        for oid, typ, val in sorted(rows, key=key):
            f.write(f"{oid}|{typ}|{val}\n")
    print(path, len(rows), "valeurs")


write("lexmark.snmprec", lexmark)
write("couleur.snmprec", couleur)
