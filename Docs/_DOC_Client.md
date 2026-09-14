# Documentation Technique : Agent Client (Autoprint.Client)

## 1. Vue d'ensemble
L'agent client est une application Windows légère s'exécutant entièrement dans l'espace utilisateur sans nécessiter de service d'arrière-plan à privilèges élevés, afin de respecter les meilleures pratiques de sécurité système (absence de risque d'élévation locale de privilèges - LPE).

* **Cible :** Postes de travail Windows 10 / 11 (Domaine, Hors-Domaine/Intune).
* **Technologie :** .NET 10 (WPF).
* **Architecture :** Processus unique en mode utilisateur standard.

---

## 2. Architecture Technique

### 2.1 Contexte d'Exécution & Interface Fenêtre Unique
* **Processus unique :** `Autoprint.Client.exe` (compilé en exécutable autonome .NET 10 *Single-File*).
* **Contexte :** `User Session` (Privilèges utilisateur standard, sans service permanent).
* **Interface Fenêtre Unique :** Remplacement de l'ancien modèle multi-fenêtres par une fenêtre unifiée réactive organisée en onglets segmentés (*Segmented Pill Tabs*) :
  1. **Disponibles :** Liste temps réel des imprimantes du lieu détecté avec installation et favoris (★).
  2. **Mes installations :** Gestion unifiée du nettoyage (imprimantes réseau et locales).
  3. **Options :** Préférences utilisateur (notifications, bascule automatique, démarrage) et zone technique admin sécurisée.
  4. **Pied de page :** Bouton de raccourci direct vers la gestion Windows (`ms-settings:printers`) et actualisation manuelle.
* **Intégration Tray :** Routage contextuel direct depuis les menus de la barre des tâches vers l'onglet approprié.
* **Cycle de vie :** Lancé automatiquement à l'ouverture de session via l'Active Setup de Windows.

---

## 3. Logique Métier et Réseau

### 3.1 Découverte et Localisation (Location Awareness)
L'agent détermine son environnement réseau en temps réel :
* **Détection IP :** Analyse des interfaces réseaux actives du poste de travail.
* **Algorithme CIDR :** Comparaison de l'IP locale avec les plages déclarées en base (cache SQLite local) pour identifier le "Lieu" logique actuel.
* **Roaming :** Un écouteur d'événements réseau (`NetworkChange`) déclenche une réévaluation automatique lors d'un changement d'IP (ex: passage du Wi-Fi au filaire, ou connexion à un VPN).

### 3.2 Stratégie de Connexion
* **Connectivité API :** REST via HTTPS pour le téléchargement initial des données. Authentification par Header HTTP `X-Agent-Secret` contenant la clé API de l'agent.
* **Temps Réel :** Connexion persistante **SignalR** (WebSockets) pour recevoir instantanément les ordres de rafraîchissement émis par le serveur ("Push-to-Pull").
* **Résilience (Offline-First) :**
    * Toutes les données (Lieux, Imprimantes) sont mises en cache dans une base **SQLite** locale dans le répertoire `%LocalAppData%`.
    * En cas de perte de connectivité avec le serveur, le client bascule instantanément sur le cache local.
    * Un watchdog tente de rétablir la connexion périodiquement toutes les 30 secondes.

### 3.3 Diagnostic Préventif (SMB)
Avant d'autoriser toute installation, l'interface graphique teste l'accessibilité du partage administratif du spouleur de destination (`\\Serveur\print$`). Si ce test échoue (problème DNS, VPN coupé, ou blocage pare-feu), les fonctions d'installation sont désactivées pour éviter les timeouts systèmes bloquants.

---

## 4. Workflow d'Installation et Moteur de Nettoyage Hybride

### 4.1 Ajout d'Imprimante Réseau
Le processus d'ajout d'imprimante s'exécute entièrement dans le contexte utilisateur :
* **Mappage de la Queue :** Commande de connexion spouleur utilisateur :
  `rundll32 printui.dll,PrintUIEntry /in /n "\\serveur-impression\NomPartageImprimante"`
* **Pré-requis Pilotes (Driver Store) :** Le pilote requis doit être présent dans le Driver Store de la machine (pré-déployé via Intune, SCCM ou GPO) pour une sécurité maximale contre les failles de type PrintNightmare.

### 4.2 Moteur de Nettoyage Hybride (Réseau & Local)
L'onglet "Mes installations" implémente un nettoyage intelligent et sécurisé :
* **Suppression Réseau :** Exécution directe dans la session utilisateur via `printui.dll /dn` (aucun privilège requis).
* **Suppression Locale (UAC Just-in-Time) :** 
  - Détection automatique des droits d'administration sur la file (`PRINTER_ACCESS_ADMINISTER`).
  - Si l'imprimante a été déployée pour l'ordinateur entier (*Machine-Wide*), un bouclier UAC est affiché et une invite d'élévation ponctuelle Windows (`runas`) est sollicitée via le chemin absolu `System32\rundll32.exe`.
* **Protection Système Absolue :**
  - Les imprimantes virtuelles critiques (`Microsoft Print to PDF`, `XPS`, `OneNote`, `Fax`) sont protégées contre toute suppression accidentelle.
  - Le bouton groupé **"NETTOYER"** cible exclusivement les imprimantes réseau non-favorites. Les imprimantes locales matérielles et les favoris sont systématiquement protégés.

---

## 5. Configuration et Déploiement

### 5.1 Gestion de la Configuration
L'application articule sa configuration sur deux niveaux :
* **Configuration Machine (Serveur & Clé API) :** Stockée dans le Registre Windows `HKLM\SOFTWARE\Autoprint` (valeurs `PRINTSERVER` et `APIKEY`). Déposée par l'installateur MSI ou surchargeable via arguments en ligne de commande (`--print-server`, `--api-key`).
* **Préférences Utilisateur :** Stockées dans `%UserProfile%\Documents\Autoprint\user-settings.json` (imprimantes favorites par lieu, activation des notifications, bascule automatique, lancement au démarrage).
* **Zone Technique Admin :** Accessible dans l'onglet Options après 5 clics sur la version, elle affiche les paramètres machine en lecture seule avec test de connectivité API, et se referme automatiquement à la fermeture de la fenêtre.

### 5.2 Packaging MSI Autonome (Single-File)
Le déploiement est assuré par un package MSI Windows Installer x64 généré via WiX Toolset 6 :
* **Architecture Single-File :** L'exécutable client est compilé avec `PublishSingleFile=true`, `SelfContained=true` et `IncludeNativeLibrariesForSelfExtract=true`. Le package MSI ne déploie qu'un seul fichier binaire (`Autoprint.Client.exe`).
* **Déploiement en Entreprise :** 
  - **En mode silencieux (`/qn` ou `/qr`) :** L'installation ou réparation s'exécute en **~7 secondes**, sans aucune invite bloquante.
  - **En mode interactif graphique :** Si la machine dispose de la stratégie de groupe `DisableAutomaticApplicationShutdown=1`, Windows Installer désactive le Restart Manager et procède à un scan hérité des fenêtres actives lors de l'action `InstallValidate`.
* **Active Setup :** Enregistre la commande dans `HKLM\Software\Microsoft\Active Setup\Installed Components` pour lancer automatiquement l'agent à l'ouverture de session de chaque utilisateur.
* **Paramètres d'installation silencieuse :**
  ```cmd
  msiexec /i Autoprint.Client.Setup.msi PRINTSERVER="https://mon-serveur-print" APIKEY="ma-super-cle-api-agent" /qn
  ```