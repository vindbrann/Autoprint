# Installeur du serveur

## Rôle

Assistant lancé une fois par serveur (Windows Server **français**) par un administrateur système. Il :
1. vérifie et installe les prérequis ;
2. configure la base de données, le site web et le pare-feu ;
3. installe ou met à jour Autoprint Server.

Il fait aussi les **mises à jour** : même assistant, titre « Mise à jour Autoprint Server ».

## Technologie et contraintes

| Élément | Aujourd'hui | Pour le template |
|---|---|---|
| Framework | WPF (.NET 10), exécutable unique autonome | inchangé |
| Bibliothèque de thème | **aucune** : contrôles WPF standard, couleurs en dur dans la fenêtre (`#2D2D30` en-tête sombre, `#007ACC` bouton principal, `#2E7D32` succès, `#FFF8E1` / `#FFB300` / `#5D4037` encadré d'avertissement, gris `#F5F5F5`, `#EFEFEF`, `#F9F9F9`, `#AAAAAA`) | **dictionnaire de ressources XAML** (couleurs, pinceaux, styles de boutons, champs, cases, listes, cartes), **sans dépendance** à une bibliothèque tierce de préférence |
| Fenêtre | 800×650 px fixe, en-tête sombre « AUTOPRINT SERVER / Assistant d'installation », zone de contenu, barre de boutons « Précédent / Suivant » | taille fixe à conserver (environ 800×650) |
| Thème | clair uniquement | **clair uniquement** suffit (outil d'administration ponctuel) ; le sombre est facultatif |
| Boîtes de message | `MessageBox` Windows standard (une dizaine : prérequis manquants, erreurs, script SQL copié…) | peuvent rester standard ; une boîte stylée est un plus |

## Étapes de l'assistant

| # | Étape | Contenu |
|---|---|---|
| 1 | **Bienvenue** | texte d'accueil, liste de ce qui va être configuré |
| 2 | **Contrat de licence** | texte RTF défilant, case d'acceptation |
| 3 | ★ **Vérification du système** | 3 lignes de prérequis : Rôle Serveur Web (IIS), Rôle Serveur d'impression, Runtime .NET 10 (Hosting Bundle). Chaque ligne affiche : un **état** (✔ « Installé » en vert, ✖ « Manquant » ou « Version 10.0.0 trop ancienne : 10.0.14 requise (correctifs de sécurité) » en rouge, « Analyse… » pendant la vérification) et un bouton **Installer** ou **Mettre à jour** à droite |
| 4 | **Base de données** | choix SQLite (fichier local) ou SQL Server ; pour SQL Server, un panneau : serveur (instance), port, nom de la base, mode d'authentification (Windows / SQL), puis identifiants |
| 5 | **Configuration web (IIS)** | port HTTP, **nom public de la console** (nom DNS), note sur le pare-feu |
| 6 | **Installation** | barre de progression, étape en cours (« Préparation… »), **journal** défilant des actions |
| 7 | ★ **Fin** | « Installation terminée » / « Mise à jour terminée » en vert. Si une action reste à faire, encadré **« Action requise »** en jaune : par exemple, faire exécuter un script SQL par l'administrateur SQL |

Captures de l'existant : `captures/installeur-*.png`.

## Installeurs MSI (assistant Windows Installer standard)

Le **MSI du client** et le **MSI du serveur** (embarqué dans l'assistant ci-dessus) utilisent les fenêtres standard de WiX (`WixUI_InstallDir`), en français. On n'y personnalise que des **images** :

| Image | Taille | Usage |
|---|---|---|
| Bandeau (`WixUIBannerBmp`) | **493 × 58 px**, BMP 24 bits | haut des pages intérieures ; le titre de la page s'écrit **en noir sur la gauche** : garder la zone de gauche claire et unie, avec le logo à droite |
| Fond d'accueil / fin (`WixUIDialogBmp`) | **493 × 312 px**, BMP 24 bits | pages Bienvenue et Fin ; le texte s'écrit sur la **partie droite** (à partir d'environ 164 px) : image ou couleur sur la bande de gauche, zone de droite claire et unie |

Le client a aussi une page « Paramètres de connexion » (adresse du serveur, clé d'agent), qui reprend le même bandeau.
