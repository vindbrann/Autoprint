# Console d'administration (web)

## Technologie et contraintes

| Élément | Aujourd'hui | Pour le template |
|---|---|---|
| Framework | Blazor WebAssembly (.NET 10), page unique servie par IIS | inchangé |
| Bibliothèque de composants | **Radzen.Blazor 8.3.8**, thème `material` / `material-dark` | **Radzen 12** (migration faite avant la refonte) |
| Personnalisation | thème Radzen + `wwwroot/css/app.css` (restes du modèle Blazor) + **~930 attributs `style="…"` écrits en dur** dans les pages | thème Radzen surchargé par **variables CSS** (`--rz-*`) + une feuille `autoprint.css` de classes utilitaires ; les styles en dur seront remplacés par ces classes |
| Icônes | police **Material Icons** fournie par Radzen (noms : `print`, `dashboard`, `place`, `settings`…) ; Bootstrap Icons chargé mais presque inutilisé | un **seul jeu d'icônes**, embarqué. Material Symbols reste le plus simple avec Radzen ; un autre jeu est possible s'il est fourni en police ou SVG |
| Graphiques | un graphique en anneau (« Répartition par modèle ») : Chart.js 4.6 (couleurs en dur dans `wwwroot/js/app.js`) et `RadzenChart` | une **palette de 6 à 8 couleurs de séries**, lisible en clair et en sombre |
| Bootstrap 5.3 | chargé, utilisé à la marge (6 classes) | à retirer si le template n'en a pas besoin |
| Thème clair / sombre | bouton en bas du menu, choix mémorisé dans le navigateur | conserver : les deux variantes sont obligatoires |
| Écran | poste de bureau (1366×768 à 1920×1080) ; tablette en secours | prévoir le menu repliable sous ~1024 px |

**Politique de sécurité du contenu (CSP) du serveur**, à respecter impérativement :

```
style-src 'self' 'unsafe-inline';  font-src 'self' data:;  img-src 'self' data: blob:;  script-src 'self' (pas de script en ligne)
```

Conséquences pour le template :
- **polices, icônes et images servies par le site lui-même** : aucune URL externe ;
- **aucun JavaScript** dans le template (ni en ligne, ni externe) : tout doit passer par le CSS ;
- **aucune ressource externe** : la recette vérifie qu'aucune requête ne sort du serveur (test PA-09).

**Base Radzen** : Radzen fournit plusieurs thèmes de base (Material, Standard, Fluent… selon la version), et chacun expose ses couleurs, rayons et ombres sous forme de **variables CSS** (`--rz-primary`, `--rz-base-background-color`, `--rz-text-color`, `--rz-border-radius`, etc.).

Le template doit :
- partir **d'un thème de base Radzen 12** (à nommer dans les livrables) ;
- livrer un fichier qui **surcharge ces variables** pour le clair et pour le sombre.

Les noms exacts des variables sont à prendre dans la documentation et le code de Radzen 12 (`_content/Radzen.Blazor/css/<thème>-base.css`).

## Structure de l'écran

```
┌──────────────┬───────────────────────────────────────────────────────┐
│ [logo] Autoprint │  Titre de la page                    [actions]      │
│              │                                                       │
│ Tableau de bord │  Contenu : cartes d'indicateurs, grilles de données, │
│ GESTION      │  formulaires, onglets                                 │
│  Imprimantes │                                                       │
│  Lieux       │                                                       │
│  Marques     │                                                       │
│  Modèles  (!)│                                                       │
│  Pilotes  (!)│                                                       │
│  Profils SNMP│                                                       │
│  Identifiants SNMP                                                   │
│  Rapports & Planification                                            │
│ SYSTÈME      │                                                       │
│  Paramètres  │                                                       │
│  Utilisateurs│                                                       │
│  Journal d'audit                                                     │
│  Rôles & droits                                                      │
│──────────────│                                                       │
│ (NC) Nom  ☾  │                                                       │
└──────────────┴───────────────────────────────────────────────────────┘
```

**Menu latéral** (280 px aujourd'hui) :
- logo et nom en haut ;
- rubriques **Gestion** et **Système**, affichées selon les droits de l'utilisateur ;
- **badge d'alerte rouge « ! »** sur Modèles et Pilotes quand il y a quelque chose à traiter ;
- en bas : avatar à initiales, nom de l'utilisateur, « Connecté » et bouton clair / sombre. Un clic sur le nom ouvre « Mon profil ».

**Notifications** : messages éphémères (succès, erreur, avertissement, information) en haut à droite, environ 190 appels dans l'application.

**Fenêtres modales** : environ 20 modales (création et modification d'objets, diagnostics, aperçus), de la petite confirmation au grand formulaire à onglets.

## Inventaire des écrans

Les **écrans clés à maquetter en priorité** sont marqués ★ ; le template doit couvrir tous les autres sans maquette dédiée.

| Écran | Adresse | Contenu | Composants dominants |
|---|---|---|---|
| ★ Connexion | `/login` | logo, identifiant, mot de passe, bouton ; messages d'erreur (identifiants faux, compte bloqué, trop de tentatives) | formulaire centré |
| Réinitialisation du mot de passe | `/reset-password` | nouveau mot de passe, confirmation, règles | formulaire centré |
| Changement de mot de passe imposé | (écran bloquant) | « Sécurité requise » : changer le mot de passe avant d'accéder à la console | modale plein écran |
| ★ Tableau de bord | `/` | 5 indicateurs (Parc actif, Pilotes, Lieux, Imprimantes hors ligne, Alertes actives / toner bas) ; anneau « Répartition par modèle » ; « État du service » (Spouleur Windows, service AutoprintSpooler : En ligne / Arrêté, version du serveur, erreurs de synchronisation) ; « Dernières activités » (liste horodatée) | cartes, badges de statut, graphique, liste |
| ★ Imprimantes | `/imprimantes` | grille de plusieurs centaines de lignes : nom, lieu, modèle, adresse IP, statut (en ligne / hors ligne / archivée), niveau de toner ; recherche, filtres, tri, pagination ; actions : ajouter, synchroniser, scanner le réseau, importer depuis le spouleur | **grille de données dense**, barre d'outils, badges |
| ★ Fiche imprimante | modale | formulaire : identité, lieu, modèle, réseau, SNMP (profil, identifiants), options ; boutons Enregistrer / Annuler / Diagnostic | formulaire en sections, listes déroulantes, interrupteurs |
| Diagnostic imprimante | modale | résultats de tests réseau et SNMP (OK / erreur, détails techniques) | liste de résultats avec statuts |
| Scanner le réseau | modale | plage d'adresses, progression, résultats à importer | formulaire + barre de progression + grille |
| Aperçu de synchronisation | modale | ajouts / modifications / suppressions prévus avant application | liste avec codes couleur |
| Lieux, Marques, Modèles, Pilotes | `/lieux`, `/marques`, `/modeles`, `/pilotes` | grilles de référentiel + modale de création / modification ; alertes (modèle sans pilote, pilote manquant sur le serveur) | grille + modale simple |
| Profils SNMP / Identifiants SNMP | `/snmp-profiles`, `/snmp-credentials` | grille ; éditeur de profil (liste d'OID, test sur une imprimante) ; identifiants v2c / v3 (secrets jamais réaffichés) | grille + grand formulaire technique |
| Rapports & planification | `/reports` | rapports PDF / CSV, planifications d'envoi par e-mail (grand formulaire de planification) | onglets, formulaire long |
| ★ Paramètres | `/parametres` | **8 onglets** : Maintenance, SMTP, Nommage, Active Directory, Supervision, Monitoring, Scanner réseau, Sécurité ; boutons « Tester », messages de résultat ; sauvegarde / restauration | **onglets + formulaires techniques longs**, alertes |
| Utilisateurs | `/users` | grille (identifiant, nom, rôle, dernière connexion, compte local / AD, actif) ; modale utilisateur | grille + modale |
| Rôles & droits | `/roles` | grille des rôles ; modale de permissions (liste de cases à cocher par domaine) ; liaison à des groupes AD | grille + modale à cases à cocher |
| Journal d'audit | `/audit` | grille horodatée filtrable (qui, quoi, quand, résultat) ; détail d'un événement | grille dense + modale de détail |
| Mon profil | `/profile` | informations du compte, changement de mot de passe, jetons d'API | cartes + formulaire |
| Page introuvable / erreur | — | message simple ; bandeau d'erreur Blazor (« Une erreur est survenue ») | — |

## États à prévoir dans le template

- **Chargement** : barre de progression, squelettes de lignes (`RadzenSkeleton`), et l'écran de démarrage de l'application (cercle de progression dans `index.html`, avant le chargement de Blazor).
- **Vide** : « Aucune imprimante », « Aucune activité récente ».
- **Erreur** : de champ (validation), de formulaire, de serveur (notification).
- **Désactivé** et **lecture seule** : utilisateur sans droit d'écriture.
- **Statuts** : en ligne, hors ligne, avertissement (toner bas), archivé, inconnu.

## Composants Radzen réellement utilisés

Composants à styler en priorité, par ordre de fréquence : Stack / Row / Column (mise en page), **Button**, **Text** (titres H1 à H6, sous-titres, légendes, surtitres), **Label**, **DataGrid** (19 grilles, 94 colonnes), **Icon**, **TextBox**, **Card**, **Badge**, **Alert**, **DropDown**, **Switch**, **ProgressBar**, **Fieldset**, **Password**, **CheckBox**, **Tabs**, **Numeric**, **Skeleton**, **SelectBar**, **TextArea**, **DatePicker**, **Chart** (anneau), **DataList**, **PanelMenu** (menu), **Layout** / **Sidebar** / **Body**, **Notification**, **Dialog**.

Variantes de bouton utilisées : principal, secondaire, texte, contour, danger, succès, icône seule.
