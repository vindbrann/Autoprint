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
* **Panneau unique (B81) :** un panneau compact s'ouvre au clic sur l'icône près de l'horloge et se range dès qu'on clique ailleurs (ou Échap). Tout tient sur une page, sans popup pour les actions (maquette : `Docs/_MAQUETTE_CLIENT.html`) :
  1. **En-tête :** site actuel et état du serveur (point vert ou orange), réglages, fermer.
  2. **Vos imprimantes :** celles du site installées sur le poste. Le bouton radio choisit l'imprimante par défaut du site (tag « Par défaut », bref surlignage). La corbeille affiche « Retirée · Annuler » ; le retrait réel a lieu à la fermeture du panneau.
  3. **Ajouter sur ce site :** tri alphabétique uniquement. Au-delà de 6 imprimantes, une ligne repliée « Ajouter une imprimante · N » ; dépliée, un champ filtre par nom, emplacement ou code inventaire.
  4. **Vos imprimantes sur d'autres sites :** section repliée (toutes les imprimantes réseau du poste hors site actuel), corbeille et « Tout retirer ».
  5. **Imprimer sur un autre site :** lien en bas, qui ouvre un volet : choix du site, puis de l'imprimante de ce site. Les recherches ignorent accents et majuscules.
  6. **Réglages :** volet avec trois interrupteurs (imprimante selon le site, annonce du changement de site, démarrage avec Windows), lien vers les imprimantes de Windows, zone informatique après 5 clics sur la version.
* **Hors ligne ou télétravail :** une note discrète dans le panneau, jamais de popup. La notification Windows n'annonce qu'un changement de site, panneau fermé (pas au démarrage si le site est le même qu'à la session précédente).
* **Menu de l'icône (clic droit) :** ouvrir Autoprint, imprimantes et scanners de Windows, quitter.
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

### 4.2 Retrait d'imprimantes
* **Imprimantes réseau seulement :** le panneau ne liste et ne retire que les connexions réseau de l'utilisateur (`\\serveur\imprimante`), via `printui.dll /dn` dans la session utilisateur (aucun privilège requis, `System32\rundll32.exe` en chemin absolu, nom passé en argument séparé et filtré).
* **Retrait différé et annulable :** la corbeille marque la ligne « Retirée · Annuler » ; les retraits sont exécutés à la fermeture du panneau (ou de l'application). Une imprimante retirée cesse d'être l'imprimante par défaut de son site.
* **Imprimantes locales et virtuelles** (PDF, XPS, OneNote, imprimantes installées pour l'ordinateur) : jamais affichées ni touchées par Autoprint ; elles se gèrent depuis « Imprimantes et scanners » de Windows (lien dans les réglages et le menu de l'icône).

---

## 5. Configuration et Déploiement

### 5.1 Gestion de la Configuration
L'application articule sa configuration sur deux niveaux :
* **Configuration Machine :** stockée dans le Registre Windows `HKLM\SOFTWARE\Autoprint`, déposée par le MSI ou surchargeable en ligne de commande :
  - `PRINTSERVER` (`--print-server`) : nom du serveur des partages d'impression (`\\PRINTSERVER\imprimante`) ;
  - `SERVERURL` (`--server-url`), facultative (B82) : adresse de la console et de l'API (`https://autoprint.mondomaine.fr`), quand elle diffère du nom des partages. À défaut : `https://PRINTSERVER` ;
  - `APIKEY` (`--api-key`) : clé agent.
  - À la mise à jour du MSI, une valeur passée en ligne de commande l'emporte sur celle déjà présente dans le registre ; une valeur absente de la ligne de commande est conservée.
* **Préférences Utilisateur :** Stockées dans `%UserProfile%\Documents\Autoprint\user-settings.json` (imprimantes favorites par lieu, activation des notifications, bascule automatique, lancement au démarrage).
* **Zone informatique :** accessible dans les réglages après 5 clics sur la version, elle affiche les paramètres machine en lecture seule (clé masquée, 4 derniers caractères) avec test de connectivité API, et se referme à la fermeture du panneau.

### 5.2 Packaging MSI Autonome (Single-File)
Le déploiement est assuré par un package MSI Windows Installer x64 généré via WiX Toolset 7 :
* **Architecture Single-File :** L'exécutable client est compilé avec `PublishSingleFile=true`, `SelfContained=true` et `IncludeNativeLibrariesForSelfExtract=true`. Le package MSI ne déploie qu'un seul fichier binaire (`Autoprint.Client.exe`).
* **Déploiement en Entreprise :** 
  - **En mode silencieux (`/qn` ou `/qr`) :** L'installation ou réparation s'exécute en **~7 secondes**, sans aucune invite bloquante.
  - **En mode interactif graphique :** Si la machine dispose de la stratégie de groupe `DisableAutomaticApplicationShutdown=1`, Windows Installer désactive le Restart Manager et procède à un scan hérité des fenêtres actives lors de l'action `InstallValidate`.
* **Active Setup :** Enregistre la commande dans `HKLM\Software\Microsoft\Active Setup\Installed Components` pour lancer automatiquement l'agent à l'ouverture de session de chaque utilisateur.
* **Paramètres d'installation silencieuse :**
  ```cmd
  msiexec /i Autoprint.Client.Setup.msi PRINTSERVER="mon-serveur-print" APIKEY="ma-super-cle-api-agent" /qn
  rem Console sous un autre nom que les partages (changement de nom web, B82) :
  msiexec /i Autoprint.Client.Setup.msi PRINTSERVER="mon-serveur-print" SERVERURL="https://autoprint.mondomaine.fr" APIKEY="ma-super-cle-api-agent" /qn
  ```
