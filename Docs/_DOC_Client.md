# Documentation Technique : Agent Client (Autoprint.Client)

## 1. Vue d'ensemble
L'agent client est une application Windows légère s'exécutant entièrement dans l'espace utilisateur sans nécessiter de service d'arrière-plan à privilèges élevés, afin de respecter les meilleures pratiques de sécurité système (absence de risque d'élévation locale de privilèges - LPE).

* **Cible :** Postes de travail Windows 10 / 11 (Domaine, Hors-Domaine/Intune).
* **Technologie :** .NET 10 (WPF).
* **Architecture :** Processus unique en mode utilisateur standard.

---

## 2. Architecture Technique

### 2.1 Contexte d'Exécution
* **Processus unique :** `Autoprint.Client.exe`.
* **Contexte :** `User Session` (Privilèges utilisateur standard).
* **Responsabilités :** Interface graphique (TrayIcon), détection réseau, requête vers l'API du serveur d'impression, et mappage de l'imprimante dans la session de l'utilisateur connecté.
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

## 4. Workflow d'Installation (Moteur d'Impression)

Le processus d'ajout d'imprimante s'exécute entièrement dans le contexte utilisateur :

* **Mappage de la Queue (Client / User) :**
  L'application exécute la commande de mappage réseau via `printui.dll` :
  `rundll32 printui.dll /in /n "\\serveur-impression\NomPartageImprimante"`
  
* **Pré-requis Pilotes (Driver Store) :**
  N'ayant plus de service à hauts privilèges (`SYSTEM`) pour injecter des pilotes non signés ou inconnus du système, **le pilote requis doit être présent dans le Driver Store de la machine**. 
  * *Déploiement moderne :* Dans un parc managé (Intune, SCCM, GPO), les pilotes d'impression requis doivent être pré-déployés sur les postes.
  * *Avantage sécurité :* Protection complète contre les vulnérabilités de type PrintNightmare et le chargement de pilotes arbitraires malveillants.
  * *Avantage réseau :* L'exécution dans la session utilisateur permet à Windows d'afficher nativement les pop-ups d'authentification réseau (Kerberos/NTLM) si le spouleur de destination l'exige (cas des postes hors-domaine).

---

## 5. Configuration et Déploiement

### 5.1 Gestion de la Configuration
L'application stocke sa configuration métier de manière portable :
* **Source de Vérité Unique :** Le fichier `user-settings.json`, situé dans le profil utilisateur (`%UserProfile%\Documents\Autoprint`), contient l'URL du serveur, la clé API de l'Agent et les préférences utilisateur (mode sombre, etc.).
* **Mécanisme de Bootstrap :** Au démarrage, la configuration est lue selon les priorités suivantes :
    1. **Arguments CLI :** Les paramètres passés à l'exécutable (`--api-key`, `--print-server`) écrasent temporairement/définissent le fichier JSON.
    2. **Fichier JSON existant :** Chargé par défaut.
    3. **Mode Technicien :** Si aucun paramètre n'est fourni et que le fichier JSON est absent, l'application attend une configuration manuelle via l'interface graphique de configuration.

### 5.2 Packaging (MSI)
Le déploiement de l'agent est assuré par un package MSI généré via WiX Toolset.
* **Active Setup :** Le MSI inscrit la commande de démarrage d'Autoprint dans la clé `HKLM\Software\Microsoft\Active Setup\Installed Components`. Cela permet d'assurer que pour chaque nouvel utilisateur se connectant sur le poste de travail, le client WPF démarre automatiquement à l'ouverture de sa session et initialise son profil utilisateur.
* **Paramètres d'installation (MSI) :**
  Le package supporte des variables d'installation silencieuse à passer en ligne de commande :
  `msiexec /i AutoprintClient.msi PRINTSERVER="https://mon-serveur-print" APIKEY="ma-super-cle-api-agent" /qn`