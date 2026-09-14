# 🗺️ Feuille de Route (Roadmap) : Autoprint Client — Fenêtre Unique & Gestion Hybride

Ce document détaille les spécifications et le plan d'implémentation pour faire évoluer **Autoprint.Client** vers une interface moderne, légère, affordante et consolidée en une **fenêtre unique**, tout en enrichissant le module de nettoyage avec la prise en charge des imprimantes locales et l'élévation de privilèges à la demande (UAC).

---

## 1. Objectifs & Philosophie

* **Expérience unifiée :** Remplacer le système multi-fenêtres (Fenêtre principale + Fenêtre de nettoyage + Fenêtre d'options) par une **fenêtre unique réactive** basée sur une navigation par onglets segmentés (*Segmented Pill Tabs*).
* **Routage direct depuis la barre des tâches :** Chaque menu contextuel du Tray Icon ouvre la fenêtre principale directement calée sur l'onglet correspondant.
* **Haute affordance :** Chaque action et chaque type d'imprimante dispose d'indicateurs visuels clairs (icônes explicites, bouclier UAC si droits nécessaires, badges de compteurs).
* **Sécurité & Non-Admin par défaut :** L'application continue de s'exécuter sous un profil utilisateur standard (non-admin). L'élévation UAC n'est sollicitée que de manière ciblée (*Just-in-Time*) pour la suppression d'imprimantes locales système.
* **Légèreté & Performance :** Utilisation exclusive de WPF natif (.NET 10) et de la bibliothèque `MaterialDesignThemes (5.3.0)` déjà intégrée au projet.

---

## 2. Spécifications Fonctionnelles & Ergonomie

### 2.1. Structure de la Fenêtre Unique (550 × 620 px)

```text
┌────────────────────────────────────────────────────────────────────────┐
│ 🖨️ AUTOPRINT CLIENT  [- MODE HORS LIGNE]                           ✕  │
│ 📍 Siège Social - Bâtiment A                                           │
├────────────────────────────────────────────────────────────────────────┤
│   [ 🖨️ Disponibles (4) ]   [ 🧹 Mes installations (2) ]   [ ⚙️ Options ] │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│   ZONE DE CONTENU DYNAMIQUE (Selon l'onglet sélectionné)               │
│                                                                        │
│   • Onglet "Disponibles"      : Liste du lieu actuel, statut install,  │
│                                 gestion des favoris (★).               │
│                                                                        │
│   • Onglet "Mes installations": Imprimantes réseau + case à cocher     │
│                                 des imprimantes locales, bouton UAC,   │
│                                 bouton de nettoyage réseau.            │
│                                                                        │
│   • Onglet "Options"          : Préférences utilisateur (notifs,       │
│                                 auto-switch, démarrage Windows)        │
│                                 + Zone technique admin masquée.        │
│                                                                        │
├────────────────────────────────────────────────────────────────────────┤
│ ℹ v1.2.0              [Paramètres Windows ↗]             [🔄 ACTUALISER]│
└────────────────────────────────────────────────────────────────────────┘
```

### 2.2. Navigation & Intégration Tray Icon (Barre des tâches)

| Élément Tray Icon | Action associée dans l'application |
| :--- | :--- |
| **Clic gauche / Double-clic** | Affiche la fenêtre principale sur le dernier onglet actif (ou *Disponibles* par défaut). |
| **Bulle de notification (clic)** | Affiche la fenêtre principale sur l'onglet *Disponibles*. |
| **"Mes Imprimantes"** | Affiche la fenêtre principale calée sur l'onglet **Disponibles**. |
| **"Nettoyer les installations"** | Affiche la fenêtre principale calée sur l'onglet **Mes installations**. |
| **"Options"** | Affiche la fenêtre principale calée sur l'onglet **Options**. |
| **"Gérer imprimantes Windows"** | Lance directement `ms-settings:printers` (conservé dans le menu et ajouté en raccourci visuel dans l'application). |
| **"Quitter"** | Ferme complètement l'agent (`Application.Current.Shutdown()`). |

### 2.3. Gestion & Nettoyage (Onglet "Mes installations")

1. **Case à cocher "Afficher aussi les imprimantes locales" :**
   - Décochée par défaut pour ne pas surcharger l'utilisateur standard.
   - Lorsque cochée : actualise la liste en incluant les imprimantes locales matérielles/directes (USB, ports TCP/IP directs).
   - **Protection système :** Filtrage systématique des imprimantes virtuelles critiques (`Microsoft Print to PDF`, `OneNote`, `XPS`, `Fax`) pour éviter toute suppression involontaire de composants système.

2. **Règle absolue du Nettoyage Global :**
   - Le bouton **"NETTOYER"** (ou "NETTOYER LE RÉSEAU") cible **strictement les imprimantes réseau non-favorites**.
   - Les imprimantes locales ne font **JAMAIS** l'objet d'un nettoyage groupé automatique.
   - Si seules des imprimantes locales sont présentes, le bouton reste inactif (grisé).

3. **Suppression unitaire intelligente (Approche A - Détection dynamique) :**
   - **Imprimante Réseau (Mappée) :** Bouton corbeille standard `[ 🗑️ ]` ➔ Suppression per-user via `printui /dn` sans droits admin.
   - **Imprimante Locale créée par l'utilisateur (Propriétaire / USB personnel) :** Bouton corbeille standard `[ 🗑️ ]` ➔ Suppression directe via `printui /dl` sans UAC.
   - **Imprimante Locale déployée par l'IT / Machine-Wide :** Bouton corbeille avec bouclier UAC `[ 🛡️ 🗑️ ]` ➔ Avertissement explicite + élévation de privilèges Windows (`ProcessStartInfo.Verb = "runas"`). En cas de refus de l'UAC par l'utilisateur, l'annulation est gérée silencieusement.

---

## 3. Architecture Technique

### 3.1. Structure des Vues (MVVM)
* **`Views/PrintersView.xaml`** *(Nouveau UserControl)* : Contient le `ScrollViewer` et la liste des imprimantes disponibles pour le lieu actuel.
* **`Views/ManagePrintersView.xaml`** *(Nouveau UserControl)* : Contient la gestion des installations (réseau + locales), la barre de progression, le statut et le bouton de nettoyage.
* **`Views/OptionsView.xaml`** *(Nouveau UserControl)* : Contient les réglages utilisateur et le panneau secret administrateur.
* **`MainWindow.xaml`** *(Refonte)* : Devient le conteneur hôte avec le header commun, le sélecteur d'onglets Material Design, un `ContentControl` / vue dynamique, et le footer commun.
* **Suppression** des fichiers devenus obsolètes : `ManagePrintersWindow.xaml(.cs)` et `OptionsWindow.xaml(.cs)`.

### 3.2. ViewModels & Orchestration
* **`MainWindowViewModel.cs`** :
  - Propriété `ClientTab SelectedTab { get; set; }` avec notification de changement.
  - Héberge et instancie `ManagePrintersViewModel` et `OptionsViewModel`.
  - Commande d'ouverture des paramètres Windows (`ms-settings:printers`).
  - Synchronisation instantanée : lors d'une désinstallation dans l'onglet de nettoyage, appel direct à `RafraichirEtatInstallation()` pour actualiser en direct la vue *Disponibles*.
* **`ManagePrintersViewModel.cs`** :
  - Ajout de `bool ShowLocalPrinters { get; set; }`.
  - Enrichissement de `InstalledPrinterUiItem` :
    - `bool IsNetwork`
    - `bool IsLocal`
    - `bool RequiresElevation` (calculé via vérification des droits `PRINTER_ACCESS_ADMINISTER` sur la file d'attente).
  - Adaptation de la méthode de suppression pour gérer l'exécution standard ou avec élévation UAC (`Verb = "runas"`).
  - Verrouillage de `CleanNonFavoritesAsync` aux seules imprimantes `IsNetwork`.

### 3.3. Cycle de vie dans `App.xaml.cs`
* Signature enrichie de `AfficherFenetre(ClientTab targetTab = ClientTab.Printers)`.
* Les handlers d'événements du Tray Menu appellent `AfficherFenetre` avec l'onglet spécifique.

---

## 4. Exigences de Sécurité & Modélisation des Menaces (Environnement Entreprise)

Pour garantir qu'aucune vulnérabilité ne soit introduite (notamment lors de l'élévation UAC), l'implémentation doit respecter strictement les 4 règles de sécurité suivantes :

### 4.1. Immunité contre l'Injection d'Arguments (Anti-LPE)
* **Risque :** Un nom d'imprimante forgé avec des caractères malveillants (`" & calc.exe & "`) transmis à une commande exécutée sous élévation UAC (`Verb = "runas"`) pourrait provoquer une escalade locale de privilèges.
* **Règle absolue :**
  - **Zéro concaténation de texte brut** pour former les arguments d'exécution.
  - Utilisation obligatoire de **`ProcessStartInfo.ArgumentList`** (.NET 10) qui isole et échappe chaque paramètre de façon hermétique au niveau de l'API système `CreateProcess`.
  - Validation préalable du nom d'imprimante contre la liste blanche des imprimantes renvoyées par le spouleur Windows (rejet des caractères suspects de contrôle de flux shell : `&`, `|`, `;`, sauts de ligne).

### 4.2. Protection contre le Détournement de Binaire (Binary / DLL Hijacking)
* **Risque :** L'appel d'un binaire simple sans chemin qualifié (ex: `rundll32.exe`) laisse Windows chercher dans le répertoire courant ou dans les chemins du `PATH`.
* **Règle absolue :**
  - Ciblage impératif du chemin absolu système immuable :
    `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rundll32.exe")` (pointant strictement vers `C:\Windows\System32\rundll32.exe`).

### 4.3. Protection contre le Déni de Service Local (Intégrité de l'OS)
* **Risque :** La suppression accidentelle ou malveillante d'imprimantes virtuelles critiques pour Windows ou les logiciels de gestion (ERP, comptabilité, bureautique).
* **Règle absolue :**
  - **Liste noire infranchissable :** Les imprimantes `Microsoft Print to PDF`, `Microsoft XPS Document Writer`, `OneNote`, `Fax` et pilotes virtuels racines sont systématiquement protégées (bouton de suppression désactivé ou masqué).

### 4.4. Maintien du Principe de Moindre Privilège (Least Privilege)
* L'application continue de s'exécuter à 100% dans la session utilisateur standard, sans nécessiter de service d'arrière-plan permanent à hauts privilèges (respect strict de la directive de sécurité du projet).
* L'UAC n'est qu'un pont temporaire et ponctuel, sous le contrôle total et explicite de l'utilisateur ou du technicien.

---

## 5. Plan d'Implémentation & Checklist de Réalisation

### 📋 Phase 1 : Refactorisation MVVM & Création des UserControls
- [x] Créer le dossier `Views/` dans `Autoprint.Client`.
- [x] Extraire le contenu de la liste d'imprimantes de `MainWindow.xaml` vers `Views/PrintersView.xaml`.
- [x] Transformer `ManagePrintersWindow.xaml` en `Views/ManagePrintersView.xaml`.
- [x] Transformer `OptionsWindow.xaml` en `Views/OptionsView.xaml`.

### 📋 Phase 2 : Refonte de `MainWindow.xaml` (Fenêtre Unique)
- [x] Intégrer la barre d'onglets segmentée (Material Design Pill/Segmented Buttons) sous l'en-tête de lieu.
- [x] Ajouter les badges visuels (compteur d'imprimantes disponibles, compteur d'installations).
- [x] Intégrer le bouton de raccourci *"Paramètres Windows ↗"* dans le pied de page de la fenêtre.
- [x] Relier les onglets au `MainWindowViewModel.SelectedTab`.

### 📋 Phase 3 : Enrichissement de la Gestion des Imprimantes Locales & UAC
- [x] Ajouter la case à cocher `[ ] Afficher les imprimantes locales` dans `ManagePrintersView.xaml` (repositionnée en bas à gauche).
- [x] Implémenter la détection des droits de suppression (`PRINTER_ACCESS_ADMINISTER`) via API Spouleur / Win32 dans `ManagePrintersViewModel`.
- [x] Afficher dynamiquement l'icône de bouclier UAC sur le bouton de suppression lorsque les droits manquent.
- [x] Implémenter la suppression avec élévation UAC (`runas`) et interception de l'annulation (`ERROR_CANCELLED`).
- [x] Verrouiller le bouton de nettoyage groupé pour exclure formellement toute imprimante locale et protéger les favoris.
- [x] **Sécurité :** Appliquer les garde-fous (chemin absolu `System32`, `ArgumentList` anti-injection, validation des noms et protection de la liste noire système).

### 📋 Phase 4 : Routage depuis le Tray Icon & Synchronisation
- [x] Adapter `AfficherFenetre(ClientTab targetTab)` dans `App.xaml.cs`.
- [x] Câbler les événements de menu du Tray Icon (`MenuOpen_Click`, `MenuManage_Click`, `MenuOptions_Click`) pour basculer directement sur le bon onglet.
- [x] Assurer la mise à jour bidirectionnelle immédiate : la suppression d'une imprimante met à jour instantanément la vue "Disponibles".

### 📋 Phase 5 : Nettoyage, Validation & Tests
- [x] Supprimer les anciennes fenêtres `ManagePrintersWindow` et `OptionsWindow`.
- [x] Valider le basculement Thème Clair / Thème Sombre Windows (correction du contraste des polices et icônes Material Design).
- [x] Valider le comportement sur poste standard (utilisateur sans droits admin) :
  - Suppression d'imprimante réseau (sans UAC).
  - Suppression d'imprimante locale USB personnelle (sans UAC).
  - Suppression d'imprimante locale déployée (avec invite UAC).
- [x] Contrôle de sécurité : vérification de l'absence de régression LPE et conformité avec les directives d'entreprise.
- [x] Valider la résilience en mode hors-ligne.
- [x] Mettre à jour `Docs/_Manuel_Client.md` pour refléter la nouvelle interface à fenêtre unique.
- [x] Packager l'installeur MSI en mode autonome *Single-File* avec gestion propre des raccourcis Bureau Public.

---

> **Statut du projet :** ✅ **Complété & Validé en Version 26.08.24.3**
