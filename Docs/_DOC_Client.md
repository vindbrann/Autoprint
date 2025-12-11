# Documentation Technique : Agent Client (Autoprint.Client)

## 1. Vue d'ensemble
L'agent client est une solution hybride composée de deux exécutables distincts fonctionnant de concert pour contourner les limitations de sécurité de Windows (UAC, PrintNightmare) tout en préservant l'expérience utilisateur.

* **Cible :** Postes de travail Windows 10 / 11 (Domaine, Hors-Domaine/Intune).
* **Technologie :** .NET 10 (WPF pour l'UI, Worker Service pour le système).
* **Architecture :** Modèle de "Séparation des Privilèges" (Privilege Separation).

---

## 2. Architecture Technique

### 2.1 Composants et Contextes d'Exécution
L'application est scindée en deux processus communiquant via IPC (Inter-Process Communication).

1.  **Autoprint.Service.exe (Le Worker)**
    * **Contexte :** `LocalSystem` (Privilèges administratifs élevés).
    * **Responsabilité Unique :** Manipulation du *Driver Store* Windows. Il est le seul autorisé à injecter des fichiers pilotes dans le système `System32`.
    * **Cycle de vie :** Service Windows à démarrage automatique.

2.  **Autoprint.Client.exe (L'Interface)**
    * **Contexte :** `User Session` (Privilèges utilisateur standard).
    * **Responsabilité :** Interface graphique (TrayIcon), détection réseau, et mappage de l'imprimante dans la session utilisateur.
    * **Cycle de vie :** Lancé à l'ouverture de session via la clé de registre `Run`.

### 2.2 Communication Inter-Processus (IPC)
* **Protocole :** Named Pipes (Tuyaux Nommés) asynchrones.
* **Sécurité :** Le Pipe est sécurisé par des ACLs (Access Control Lists) restreignant l'accès aux utilisateurs authentifiés.
* **Format :** Échange de messages JSON stricts (Contrat : `INSTALL_DRIVER` + `DriverModelName`).

---

## 3. Logique Métier et Réseau

### 3.1 Découverte et Localisation (Location Awareness)
L'agent ne dépend pas d'une affectation statique. Il détermine son environnement en temps réel :
* **Détection IP :** Analyse des interfaces réseaux actives.
* **Algorithme CIDR :** Comparaison de l'IP locale avec les plages déclarées en base (Cache SQLite) pour identifier le "Lieu" logique.
* **Roaming :** Un écouteur d'événements réseau (`NetworkChange`) déclenche une réévaluation automatique lors d'un changement d'IP (ex: changement de WiFi ou connexion VPN).

### 3.2 Stratégie de Connexion Hybride
* **Connectivité API :** REST via HTTPS pour le téléchargement de données. Authentification par Header `X-Agent-Secret` (Clé API M2M).
* **Temps Réel :** Connexion persistante **SignalR** (WebSockets) pour recevoir les ordres de rafraîchissement ("Push-to-Pull").
* **Résilience (Offline-First) :**
    * Toutes les données (Lieux, Imprimantes) sont mises en cache dans une base **SQLite** locale (`%LocalAppData%`).
    * En cas de coupure serveur, le client bascule instantanément sur le cache local et active un *Watchdog* pour tenter une reconnexion périodique.

### 3.3 Diagnostic Préventif (SMB)
Avant d'autoriser toute installation, l'UI teste l'accessibilité du partage administratif `\\Serveur\print$`. Si ce test échoue (problème DNS, VPN coupé), les fonctions d'installation sont verrouillées pour éviter les timeouts systèmes bloquants.

---

## 4. Workflow d'Installation (Moteur d'Impression)

Le processus d'ajout d'imprimante utilise une stratégie séquentielle pour garantir la compatibilité avec les postes hors-domaine (BYOD/Intune) et l'authentification Kerberos/NTLM.

1.  **Phase 1 : Injection du Pilote (Service / Admin)**
    * L'UI envoie l'ordre au Service via IPC.
    * Le Service exécute `rundll32 printui.dll /ia` (Install Admin) pour pré-installer le pilote dans le magasin de pilotes de la machine.
    * *Note :* Cette étape nécessite les droits Admin, d'où l'utilisation du Service.

2.  **Phase 2 : Mappage de la Queue (Client / User)**
    * Une fois le pilote confirmé présent, l'UI reprend la main.
    * L'UI exécute `rundll32 printui.dll /in` (Install Network) dans le contexte de l'utilisateur.
    * **Avantage Critique :** En s'exécutant dans la session utilisateur, Windows peut afficher nativement la pop-up d'authentification réseau si le serveur d'impression le demande (cas des postes hors domaine), ce qui serait impossible depuis le contexte `LocalSystem`.

---

## 5. Configuration et Déploiement

### 5.1 Gestion de la Configuration
L'application privilégie la portabilité et l'indépendance vis-à-vis du registre système pour la configuration métier.

* **Source de Vérité Unique :** Le fichier `user-settings.json`, situé dans le profil utilisateur (`%UserProfile%\Documents\Autoprint`), contient l'URL du serveur, la Clé API Agent et les préférences utilisateur.
* **Mécanisme de Bootstrap :** Au démarrage, l'application détermine sa configuration selon l'ordre de priorité suivant :
    1.  **Arguments CLI :** Les paramètres passés à l'exécutable (`--api-key`, `--print-server`) sont prioritaires et viennent écraser/initialiser le fichier JSON.
    2.  **Fichier JSON existant :** Si aucun argument n'est fourni, l'application charge la configuration persistante.
    3.  **Mode Technicien :** Si aucune configuration n'est trouvée (fichier absent et pas d'arguments), l'application attend une configuration manuelle via l'interface sécurisée.

### 5.2 Packaging (MSI)
Le déploiement est assuré par un package MSI unique généré via WiX Toolset.

* **Architecture de Fichiers :** Installation physique séparée des binaires Client (UI) et Service (Worker) pour éviter les conflits de dépendances .NET.
* **Actions Système :**
    * Installation et démarrage du Service Windows (`Autoprint.Service.exe`) sous le compte `LocalSystem`.
    * Création des règles de Pare-feu Windows pour autoriser les communications SignalR et IPC.
    * Inscription de l'application UI (`Autoprint.Client.exe`) dans la clé de registre `Run` (`HKLM\...\Run`) **uniquement** pour assurer le lancement automatique à l'ouverture de session.

### 5.3 Paramètres d'Installation (Déploiement de Masse)
L'installateur MSI supporte l'injection de paramètres pour l'initialisation silencieuse (via script de déploiement ou raccourci) :

* **Commande CLI :** Les administrateurs peuvent déployer un raccourci ou un script de lancement passant les arguments requis lors du premier démarrage de l'exécutable client :
    `Autoprint.Client.exe --print-server "https://srv-print" --api-key "abc-123"`.