# Client de bureau (poste utilisateur)

## Rôle

Petite application résidente sur chaque poste Windows 10 / 11, déployée par Intune (installation machine).

- **Au démarrage de session** : elle détecte le **lieu** de l'utilisateur (site, étage…) et propose les imprimantes de ce lieu.
- **Imprimantes** : l'utilisateur les installe en un clic, choisit ses **favoris** (★) et son imprimante par défaut.
- **Nettoyage** : il peut retirer les anciennes imprimantes réseau.
- **Zone de notification** : l'application vit près de l'horloge (icône, menu contextuel, bulles de notification).

Public : **tous les utilisateurs**, non techniciens. L'interface doit être simple, rassurante et rapide.

## Technologie et contraintes

| Élément | Aujourd'hui | Pour le template |
|---|---|---|
| Framework | WPF (.NET 10) | inchangé |
| Bibliothèque | **MaterialDesignThemes / MaterialDesignColors 5.4** (MaterialDesignInXAML), styles Material Design 3 | conservée : le template définit la palette (primaire, secondaire) et redéfinit les styles utiles |
| Thème actuel | `BundledTheme` : primaire **Grey**, secondaire **Lime**, base **héritée de Windows** (clair / sombre automatique) | nouvelle palette ; **le suivi du thème Windows reste obligatoire** |
| Couleurs en dur | pastilles de compteur `#1E88E5` (bleu) et `#43A047` (vert), badge « Mode hors ligne » | à remplacer par des ressources nommées |
| Icônes | `materialDesign:PackIcon` (jeu Material Design Icons : Printer, Broom, Cog, MapMarker, Star, StarOutline, DeleteOutline, PrinterOff, PrinterAlert, Refresh, Lock, Security, MicrosoftWindows…) | même jeu que la console de préférence ; sinon, chemins SVG / XAML |
| Police | police MaterialDesign par défaut (Roboto) | police de la charte, **embarquée** dans l'application |
| Fenêtre | **sans bordure Windows**, coins arrondis (12 px), 540×620 px, redimensionnable ; déplaçable par l'en-tête ; la croix réduit dans la zone de notification | à conserver (ou proposer mieux) ; la fenêtre reste petite |
| Icône | `Autoprint.ico` (fenêtre, barre des tâches, zone de notification) | nouvelle icône : voir les livrables |

## Écrans

### Fenêtre principale

```
┌──────────────────────────────────────────┐
│ [ico] AUTOPRINT CLIENT [MODE HORS LIGNE] ✕│  ← en-tête (déplaçable)
│ 📍 Siège - Bâtiment A - 2e étage          │  ← lieu détecté (titre)
├──────────────────────────────────────────┤
│  ( Disponibles 12 | Mes installations 4 | Options )  ← onglets « pilule »
├──────────────────────────────────────────┤
│                                          │
│   contenu de l'onglet                    │
│                                          │
├──────────────────────────────────────────┤
│ ⓘ 26.11.9.1   Paramètres Windows ↗  ⟳ ACTUALISER │ ← pied de page
└──────────────────────────────────────────┘
```

### Onglet « Disponibles » (★ écran clé)

- **Contenu** : liste des imprimantes du lieu, sous forme de cartes ou de lignes : nom, emplacement, modèle.
- **Actions** :
  - bouton **Installer** (puis « Installation… » avec indicateur circulaire) ;
  - une fois installée : étoile **favori / imprimante par défaut** et bouton **Retirer**.
- **État vide** : icône d'imprimante barrée, « Aucune imprimante disponible pour ce lieu. Vérifiez votre connexion réseau ou contactez le support. »

### Onglet « Mes installations »

- **En-tête** : « Imprimantes détectées », avec la mention « Les favoris (★) et les imprimantes locales sont protégés du nettoyage. »
- **Liste** : les imprimantes installées sur le poste.
- **Option** : case « Afficher les imprimantes locales ».
- **Action** : bouton **NETTOYER**, qui supprime les imprimantes réseau non favorites.

### Onglet « Options »

- **Préférences** (interrupteurs) :
  - « M'avertir lors du changement de lieu de travail » ;
  - « Changer l'imprimante par défaut automatiquement selon mes favoris » ;
  - « Lancer Autoprint à l'ouverture de session Windows ».
- **Zone technique (admin)** : cachée par défaut, elle s'affiche après 5 clics sur le numéro de version. Elle contient l'adresse du serveur, la clé d'agent (4 derniers caractères) et le bouton « TESTER LA CONNEXION SERVEUR ». Le style doit la distinguer comme zone réservée.
- **Mention en bas** : « Les modifications sont enregistrées automatiquement. »

### Fenêtre « Imprimante manquante »

Petite boîte de dialogue :
- icône d'alerte ;
- titre « Favori introuvable » ;
- message expliquant qu'une imprimante favorite a disparu du poste ;
- boutons « Oublier ce favori » (secondaire) et « Réinstaller » (principal).

### Zone de notification de Windows

- **Icône** : visible en permanence, infobulle « Autoprint Agent ».
- **Menu contextuel** : Mes imprimantes, Mes installations, Options, Gérer imprimantes Windows, Quitter (avec icônes).
- **Bulles de notification** : changement de lieu, installation terminée, erreur.

## États à prévoir

En ligne / **hors ligne** (badge dans l'en-tête), installation en cours, succès, erreur, liste vide, thème clair et thème sombre.
