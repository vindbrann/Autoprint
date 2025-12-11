#  Autoprint

**Autoprint** est une solution hybride de gestion d'impression pour environnements Windows, conçue pour résoudre les problématiques de mobilité et de sécurité (Point & Print) sous Windows 11.

Basée sur une architecture **.NET 10**, elle combine une administration centralisée avec une intelligence locale sur les postes clients ("Location Awareness").

---

##  Architecture Hybride

La solution repose sur une architecture distribuée conçue pour la résilience (Offline-First) :

###  Pôle SERVEUR (Back-Office)
* **API REST (.NET 10)** : Cœur du système, gère le référentiel et la sécurité via JWT.
* **Admin UI (Blazor WASM + Radzen)** : Interface riche pour les techniciens (Matrice de droits RBAC, Dashboard, Logs).
* **Moteur WMI** : Synchronisation avec le Spouleur Windows Server (Source de vérité) et nettoyage intelligent des pilotes.

###  Pôle CLIENT (Poste Utilisateur)
Architecture scindée pour contourner les limitations de sécurité (UAC/Intune):
1.  **Service Windows (LocalSystem)** : Le "Worker". Il injecte les pilotes dans le Driver Store avec les privilèges élevés (Admin).
2.  **App WPF (Session Utilisateur)** : L'interface. Elle détecte le réseau (CIDR), gère les préférences et mappe les imprimantes dans la session de l'utilisateur.
3.  **Cache Local (SQLite)** : Permet au client de fonctionner même si le serveur est injoignable (Mode Hors-Ligne).

---

##  Fonctionnalités Clés

###  Sécurité & Conformité
* **Authentification Hybride** : Supporte Active Directory et les comptes locaux.
* **RBAC Granulaire** : Gestion fine des droits (Lecture, Écriture, Sync, Scan).
* **Protection M2M** : Les clients s'authentifient via une clé d'API unique (`X-Agent-Secret`).

###  Intelligence Client
* **Location Awareness** : Détection automatique du "Lieu" en fonction de l'IP et du sous-réseau (CIDR).
* **Smart Sync** : Installation "Zéro-Touche" des pilotes sans droits admin pour l'utilisateur.
* **Mode Filiale (Direct IP)** : Capacité de configurer des ports RAW directs pour contourner le serveur d'impression en cas de panne WAN.

---

##  Déploiement & Installation

### Pré-requis Serveur
L'installation se fait via un **Launcher Intelligent (WPF)** qui vérifie et installe automatiquement les pré-requis (Rôles IIS, .NET 10, Print Services) via WMI.

### Déploiement Client (Mass Deployment)
Le client est packagé sous forme de **MSI unique** compatible GPO/Intune/SCCM.
Il supporte l'injection des paramètres de connexion à l'installation :

```cmd
msiexec /i Autoprint.Client.msi /qn PRINTSERVER="srv-print.corp.local" APIKEY="votre-guid-unique"
````

  * **PRINTSERVER** : Nom DNS ou IP du serveur Autoprint.
  * **APIKEY** : Clé d'authentification Agent générée lors de l'installation serveur.

> **Note** : La configuration est ensuite stockée de manière persistante dans le fichier `user-settings.json` du profil utilisateur.

-----

##  Stack Technique

  * **Framework** : .NET 10 (Dernière version).
  * **Serveur** : ASP.NET Core Web API + Entity Framework Core (SQL Server).
  * **Web UI** : Blazor WebAssembly + Composants **Radzen**[.
  * **Client** : WPF (Desktop) + Windows Worker Service.
  * **BDD Locale** : SQLite (Cache client).
  * **Protocole** : SignalR (Notifications Temps Réel "Push-to-Pull") + Named Pipes (IPC Local).

-----

*Autoprint - Solution de Gestion Dynamique d'Impression.*