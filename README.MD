#  Autoprint

[cite_start]**Autoprint** est une solution hybride de gestion d'impression pour environnements Windows, conçue pour résoudre les problématiques de mobilité et de sécurité (Point & Print) sous Windows 10/11 [cite: 981-984].

Basée sur une architecture **.NET 10**, elle combine une administration centralisée avec une intelligence locale sur les postes clients ("Location Awareness").

---

##  Architecture Hybride

La solution repose sur une architecture distribuée conçue pour la résilience (Offline-First) :

###  Pôle SERVEUR (Back-Office)
* [cite_start]**API REST (.NET 10)** : Cœur du système, gère le référentiel et la sécurité via JWT[cite: 1173].
* [cite_start]**Admin UI (Blazor WASM + Radzen)** : Interface riche pour les techniciens (Matrice de droits RBAC, Dashboard, Logs)[cite: 1183].
* [cite_start]**Moteur WMI** : Synchronisation avec le Spouleur Windows Server (Source de vérité) et nettoyage intelligent des pilotes [cite: 994-999].

###  Pôle CLIENT (Poste Utilisateur)
[cite_start]Architecture scindée pour contourner les limitations de sécurité (UAC/Intune)[cite: 1214]:
1.  **Service Windows (LocalSystem)** : Le "Worker". [cite_start]Il injecte les pilotes dans le Driver Store avec les privilèges élevés (Admin)[cite: 1218].
2.  **App WPF (Session Utilisateur)** : L'interface. [cite_start]Elle détecte le réseau (CIDR), gère les préférences et mappe les imprimantes dans la session de l'utilisateur [cite: 1224-1231].
3.  [cite_start]**Cache Local (SQLite)** : Permet au client de fonctionner même si le serveur est injoignable (Mode Hors-Ligne)[cite: 1233].

---

##  Fonctionnalités Clés

###  Sécurité & Conformité
* [cite_start]**Authentification Hybride** : Supporte Active Directory et les comptes locaux[cite: 1021].
* [cite_start]**RBAC Granulaire** : Gestion fine des droits (Lecture, Écriture, Sync, Scan)[cite: 1067].
* [cite_start]**Protection M2M** : Les clients s'authentifient via une clé d'API unique (`X-Agent-Secret`)[cite: 1045].

###  Intelligence Client
* [cite_start]**Location Awareness** : Détection automatique du "Lieu" en fonction de l'IP et du sous-réseau (CIDR)[cite: 1154].
* [cite_start]**Smart Sync** : Installation "Zéro-Touche" des pilotes sans droits admin pour l'utilisateur[cite: 1164].
* [cite_start]**Mode Filiale (Direct IP)** : Capacité de configurer des ports RAW directs pour contourner le serveur d'impression en cas de panne WAN[cite: 1142].

---

##  Déploiement & Installation

### Pré-requis Serveur
[cite_start]L'installation se fait via un **Launcher Intelligent (WPF)** qui vérifie et installe automatiquement les pré-requis (Rôles IIS, .NET 10, Print Services) via WMI [cite: 1290-1297].

### Déploiement Client (Mass Deployment)
[cite_start]Le client est packagé sous forme de **MSI unique** compatible GPO/Intune/SCCM[cite: 1329].
Il supporte l'injection des paramètres de connexion à l'installation :

```cmd
msiexec /i Autoprint.Client.msi /qn PRINTSERVER="srv-print.corp.local" APIKEY="votre-guid-unique"
````

  * [cite_start]**PRINTSERVER** : Nom DNS ou IP du serveur Autoprint[cite: 1334].
  * [cite_start]**APIKEY** : Clé d'authentification Agent générée lors de l'installation serveur[cite: 1335].

> [cite_start]**Note** : La configuration est ensuite stockée de manière persistante dans le fichier `user-settings.json` du profil utilisateur[cite: 1340].

-----

##  Stack Technique

  * [cite_start]**Framework** : .NET 10 (Dernière version)[cite: 1173].
  * **Serveur** : ASP.NET Core Web API + Entity Framework Core (SQL Server).
  * [cite_start]**Web UI** : Blazor WebAssembly + Composants **Radzen**[cite: 1183].
  * **Client** : WPF (Desktop) + Windows Worker Service.
  * [cite_start]**BDD Locale** : SQLite (Cache client)[cite: 1233].
  * [cite_start]**Protocole** : SignalR (Notifications Temps Réel "Push-to-Pull") + Named Pipes (IPC Local)[cite: 1253, 1272].

-----

*Autoprint - Solution de Gestion Dynamique d'Impression.*