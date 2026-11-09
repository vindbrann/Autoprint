# Autoprint : dossier pour la conception du template graphique

Ce dossier contient ce qu'il faut savoir pour concevoir la nouvelle charte graphique d'Autoprint et le **template** qui sera ensuite intégré dans la solution (backlog **B77**).

L'interface est conçue à part. Les livrables reviennent ensuite à l'équipe de développement, qui les intègre.

## Qu'est-ce qu'Autoprint ?

Autoprint est une solution de **gestion et de déploiement d'imprimantes réseau** pour un parc Windows (entreprise, collectivité), en français uniquement. Elle comprend trois interfaces :

| Interface | Utilisateurs | Technologie | Fiche |
|---|---|---|---|
| **Console d'administration** (web) | Administrateurs et support informatique | Blazor WebAssembly + **Radzen Blazor** | [01-console-web.md](01-console-web.md) |
| **Client de bureau** (poste utilisateur) | Tous les utilisateurs : choisir et installer les imprimantes de son lieu | WPF + **MaterialDesignInXAML** | [02-client-bureau.md](02-client-bureau.md) |
| **Installeur du serveur** (assistant) | Administrateur système, une fois par serveur | WPF (sans bibliothèque de thème) + MSI WiX | [03-installeur-serveur.md](03-installeur-serveur.md) |

**Objectif de la refonte** : une interface plus **professionnelle**, **cohérente** entre les trois produits (même identité, mêmes couleurs, mêmes icônes, même ton) et **sobre**. C'est un outil d'administration utilisé tous les jours, pas un site vitrine.

## Ce qui est attendu

Le détail des formats est dans [04-livrables-et-integration.md](04-livrables-et-integration.md). En résumé :

1. **Charte** : couleurs (thème clair **et** sombre), typographie, espacements, arrondis, ombres, icônes, logo, ton des textes.
2. **Jetons de design** (*design tokens*) dans un fichier unique (JSON) : la source de vérité que l'on déclinera dans chaque technologie.
3. **Maquettes** des écrans clés des trois interfaces (HTML statique ou images), en clair et en sombre.
4. **Si possible, les fichiers de thème prêts à intégrer** : feuille CSS de variables Radzen pour la console, dictionnaires de ressources XAML pour le client et l'installeur.
5. **Ressources graphiques** : logo, icône d'application, favicon, images de l'installeur MSI.

## Contraintes communes (à respecter dans les trois interfaces)

- **Français**, avec les accents. Les textes de l'interface restent ceux de l'application (ils peuvent être améliorés, mais la maquette doit utiliser les vrais libellés).
- **Thèmes clair et sombre obligatoires** :
  - la console a déjà un bouton clair / sombre, mémorisé par utilisateur ;
  - le client suit le thème de Windows.
- **Accessibilité** :
  - contraste **WCAG 2.1 AA** (4,5:1 pour le texte, 3:1 pour les composants et grands textes) dans les deux thèmes ;
  - focus clavier visible ;
  - une information ne doit jamais passer **uniquement** par la couleur (statut « Hors ligne » = couleur + icône + texte).
- **Aucune ressource externe** :
  - pas de Google Fonts, pas de CDN : polices et icônes doivent être **embarquées** dans l'application (serveurs souvent sans accès Internet, politique de sécurité du contenu stricte : voir la fiche console) ;
  - polices sous **licence libre** (OFL, Apache…) autorisant l'intégration dans un logiciel.
- **Couleurs de statut** cohérentes partout : succès / en ligne, avertissement / toner bas, erreur / hors ligne, information, neutre / archivé.
- **Densité** : la console affiche de longues listes (plusieurs centaines d'imprimantes) et des formulaires techniques. Il faut prévoir une densité « confortable mais compacte », pas des cartes géantes.

## Ordre de travail prévu

1. Passage de la console à **Radzen 12**, sans changement visuel voulu (prérequis technique, fait par le développement).
2. Conception de la charte et du template, **à partir de ce dossier**.
3. Intégration :
   - **console** : thème Radzen et nettoyage des styles écrits en dur ;
   - **client** : dictionnaire XAML ;
   - **installeur** : dictionnaire XAML et images MSI.
4. Recette en labo.

## Captures de l'existant

Le dossier [captures/](captures/) contient des captures de l'interface actuelle, à titre de référence (contenu, densité, enchaînements), pas comme modèle visuel. Elles ont été prises en 26.11.9.1, dans le labo, avec des données de test.

| Fichier | Écran |
|---|---|
| `console-connexion.jpg` | Console : connexion |
| `console-tableau-de-bord.jpg` / `-sombre.jpg` | Console : tableau de bord, thème clair et thème sombre |
| `console-imprimantes.jpg` | Console : grille des imprimantes (2 imprimantes de test ; en production, plusieurs centaines) |
| `console-fiche-imprimante.jpg` | Console : modale de modification d'une imprimante |
| `console-parametres.jpg` | Console : paramètres, onglet Maintenance |
| `console-journal-audit.jpg` | Console : journal d'audit (grille dense paginée) |
| `installeur-prerequis-a-installer.png` / `-ok.png` | Installeur : vérification du système (prérequis manquants, puis tous installés) |
| `installeur-fin.png` | Installeur : fin de mise à jour |

Pas encore de capture du **client de bureau** : sa description détaillée est dans [02-client-bureau.md](02-client-bureau.md). Des captures peuvent être ajoutées sur demande.
