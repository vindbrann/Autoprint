# 🖨️ Autoprint

**Solution centralisée de déploiement dynamique, d'automatisation du spouleur d'impression Windows et de supervision télémétrique SNMP pour parcs d'entreprise multi-sites.**

[![Version](https://img.shields.io/badge/version-26.8.24.3-blue.svg?style=for-the-badge&logo=github)](Directory.Build.props)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/Backend-ASP.NET%20Core%20Web%20API-512BD4.svg?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/apps/aspnet)
[![Blazor WASM](https://img.shields.io/badge/WebAdmin-Blazor%20WebAssembly-7852FF.svg?style=for-the-badge&logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![WPF Client](https://img.shields.io/badge/Client-WPF%20Agent%20Desktop-0078D4.svg?style=for-the-badge&logo=windows)](https://github.com/dotnet/wpf)
[![Windows Service](https://img.shields.io/badge/Service-Windows%20Service%20SYSTEM-0078D4.svg?style=for-the-badge&logo=windows-terminal)](https://learn.microsoft.com/windows/win32/services/services)
[![Database](https://img.shields.io/badge/Database-SQL%20Server%20%7C%20SQLite-CC292B.svg?style=for-the-badge&logo=microsoft-sql-server)](https://www.microsoft.com/sql-server)
[![Security](https://img.shields.io/badge/Security-RBAC%2029%20Perms%20%7C%20PBKDF2-success.svg?style=for-the-badge&logo=shield)](PROJECT_CONTEXT.md#4-modèle-de-sécurité--conformité)

---

## 📑 Sommaire

1. [Présentation](#1-présentation)
   - [Problématique Métier Résolue](#11-problématique-métier-résolue)
   - [Proposition de Valeur Technique](#12-proposition-de-valeur-technique)
2. [Fonctionnalités Clés](#2-fonctionnalités-clés)
   - [Détection Dynamique de Lieu & Mobilité Utilisateur](#21-détection-dynamique-de-lieu--mobilité-utilisateur)
   - [Mode Filiale (Direct IP) & Automatisation du Spouleur](#22-mode-filiale-direct-ip--automatisation-du-spouleur)
   - [Télémétrie SNMP Universelle & Algorithme Prédictif](#23-télémétrie-snmp-universelle--algorithme-prédictif)
   - [Rapports Planifiés Vectoriels & API d'Intégration](#24-rapports-planifiés-vectoriels--api-dintégration)
   - [Console Web d'Administration Réactive](#25-console-web-dadministration-réactive)
   - [Sécurité Granulaire (RBAC) & Annuaire Active Directory](#26-sécurité-granulaire-rbac--annuaire-active-directory)
3. [Architecture & Technologies](#3-architecture--technologies)
4. [Arborescence du Projet](#4-arborescence-du-projet)
5. [Principes de Sécurité & Gouvernance](#5-principes-de-sécurité--gouvernance)
   - [Modèle Zéro-Privilège Utilisateur & Isolation IPC](#51-modèle-zéro-privilège-utilisateur--isolation-ipc)
   - [Cryptographie, Gestion des Sessions & Authentification](#52-cryptographie-gestion-des-sessions--authentification)
   - [Neutralisation des Injections & Désinfection des Entrées](#53-neutralisation-des-injections--désinfection-des-entrées)
   - [Masquage des Secrets & Traçabilité Complète](#54-masquage-des-secrets--traçabilité-complète)
6. [Installation, Configuration & Démarrage](#6-installation-configuration--démarrage)
   - [Prérequis Système & Logiciels](#61-prérequis-système--logiciels)
   - [Installation & Déploiement](#62-installation--déploiement)
   - [Configuration Serveur (appsettings.json & Variables d'Environnement)](#63-configuration-serveur-appsettingsjson--variables-denvironnement)
   - [Paramètres en Base de Données (ServerSettings)](#64-paramètres-en-base-de-données-serversettings)
   - [Commandes de Lancement & Maintenance](#65-commandes-de-lancement--maintenance)
7. [Assurance Qualité & Tests](#7-assurance-qualité--tests)
8. [Documentation Complémentaire](#8-documentation-complémentaire)

---

## 1. Présentation

### 1.1. Problématique Métier Résolue

Dans les parcs informatiques d'entreprise modernes (multi-sites, réseaux hybrides, collaborateurs nomades), la gestion de l'impression fait face à des contraintes structurelles majeures :
- **Perte de temps utilisateur lors des déplacements :** Un collaborateur changeant d'établissement ou d'étage doit rechercher manuellement les imprimantes locales, installer les bons pilotes ou solliciter le support technique.
- **Saturation des liaisons WAN :** L'impression centralisée traditionnelle achemine l'intégralité du flux spoulé (fichiers EMF/RAW volumineux) depuis le poste distant vers le serveur central, puis du serveur central vers l'imprimante locale, provoquant une congestion réseau sévère sur les liaisons inter-sites à faible bande passante.
- **Ruptures inopinées de consommables :** L'absence de visibilité centralisée sur les niveaux réels de toners et l'état des bacs entraîne des interruptions de service imprévues et des commandes de consommables en urgence.
- **Failles de sécurité liées aux privilèges :** L'installation de pilotes d'impression requiert historiquement des privilèges élevés (`Administrateur local`), rendant complexe la sécurisation des postes en environnement zéro-privilège.

### 1.2. Proposition de Valeur Technique

**Autoprint** répond à ces enjeux par une solution logicielle unifiée en architecture .NET 10 :
- **Géolocalisation réseau sans intervention :** L'agent de bureau analyse en temps réel l'adresse IPv4 active du poste de travail et ses correspondances CIDR pour présenter instantanément les imprimantes du site physique visité.
- **Découplage contrôle / données (Branch Office Direct Printing) :** Le serveur d'impression central orchestre les configurations, les autorisations et les politiques, tandis que les flux de données d'impression transitent directement du poste utilisateur vers le port TCP/IP de l'imprimante locale.
- **Maintenance proactive par télémétrie SNMP :** Surveillance continue de l'ensemble du parc via les MIB standard (RFC 3805) et propriétaires, couplée à un moteur de régression linéaire prédisant la date exacte de rupture de chaque cartouche.
- **Architecture à privilèges scindés :** L'agent client s'exécute exclusivement en droits utilisateur standard, déléguant les opérations système machine (installation silencieuse de pilotes système via `printui.dll`) à un service Windows dédié via un tube nommé IPC sécurisé.

---

## 2. Fonctionnalités Clés

### 2.1. Détection Dynamique de Lieu & Mobilité Utilisateur
- **Appariement CIDR Binaire (`IpHelper`) :** Évaluation instantanée de l'adresse IPv4 locale du poste par rapport aux masques sous-réseau configurés pour chaque site. Prise en charge des réseaux multi-sites avec priorité aux plages déclarées primaires (`IsPrimary`).
- **Fonctionnement Déconnecté (*Offline-First*) :** Cache local transactionnel SQLite (`client_cache.db`) stocké dans `%LOCALAPPDATA%\Autoprint\`, garantissant l'accès aux imprimantes même en cas de rupture de lien avec le serveur central.
- **Bascule Automatique de l'Imprimante par Défaut :** Détection du lieu et application transparente de l'imprimante favorite définie par l'utilisateur pour le site courant (`user-settings.json`).
- **Fenêtre Unique Épurée & Systray :** Interface WPF Material Design réactive à onglets (*Imprimantes*, *Gérer*, *Options*), minimisable dans la zone de notification Windows avec gestionnaire de réveil IPC anti-doublon.

### 2.2. Mode Filiale (Direct IP) & Automatisation du Spouleur
- **Branch Office Direct Printing :** Activation du mode d'impression directe filiale par file d'impression ou globalement, évitant le transit des flux volumineux sur le WAN.
- **Moteur de Staging & Réconciliation (`SyncSpoolerService`) :** Les modifications opérées dans la console Web sont enregistrées dans un état transitoire (`PendingCreation`, `PendingUpdate`, `PendingDelete`). L'administrateur prévisualise le différentiel avant d'appliquer les changements par lot sur le spouleur d'impression Windows hôte.
- **Contrôle Système Bas Niveau :** Création et configuration automatisées des ports TCP/IP (`Win32_TCPIPPrinterPort`), des files d'impression (`Win32_Printer`) et des pilotes via P/Invoke `winspool.drv` et WMI.
- **Nommage Standardisé par Gabarit (`NamingService`) :** Génération dynamique des noms de files et de partages à partir de variables contextuelles (`{LIEU}`, `{MODELE}`, `{IP}`, `{ADRESSEIP}`).

### 2.3. Télémétrie SNMP Universelle & Algorithme Prédictif
- **Profils SNMP Extensibles :** Suivi granulaire des toners (Noir, Cyan, Magenta, Jaune), bacs d'alimentation (*Trays*), bacs de récupération de toner résiduel, tambours photosensibles et compteurs de pages via SNMP v1/v2c.
- **Auto-Découverte MIB en Direct :** Assistant d'interrogation live d'une imprimante de test balayant l'arborescence OID standard RFC 3805 (Printer-MIB) pour générer automatiquement un profil matériel sans saisie manuelle.
- **Régression Linéaire Prédictive (`PredictiveService`) :** Analyse chronologique de la consommation réelle issue de l'historique quotidien (`TonerHistories`) calculée par la méthode des moindres carrés pour estimer le nombre de jours restants avant épuisement.
- **Balayage Périodique en Tâche de Fond :** Worker d'arrière-plan (`PrinterMonitoringWorker`) interrogeant le parc pendant les plages horaires configurées, avec archivage automatique des périphériques hors-ligne depuis plus de 30 jours.

### 2.4. Rapports Planifiés Vectoriels & API d'Intégration
- **Génération Vectorielle PDF (`ReportGeneratorService`) :** Création de rapports d'état complets incluant métriques de santé, jauges de consommables colorées, estimations prédictives et alertes matérielles, compilés via `PdfSharpCore`.
- **Expédition Automatique SMTP :** Planificateur de rapports périodiques (`ReportSchedule`) par e-mail avec support des protocoles chiffrés SSL/TLS.
- **Exports Données Assainis :** Téléchargement direct d'inventaires au format CSV avec neutralisation proactive des injections de formules (CWE-1236).
- **API Monitoring Tiers (`/api/v1/Monitoring/export`) :** Point d'accès dédié aux systèmes de supervision externes (PRTG, Zabbix, GLPI) authentifié par jeton d'intégration cryptographique (`X-Api-Token`).

### 2.5. Console Web d'Administration Réactive
- **Frontend Blazor WebAssembly (.NET 10) :** Interface monopage (SPA) fluide intégrant la suite de composants Radzen UI (v8.3.8) et Bootstrap 5.3.
- **Tableau de Bord Synthétique :** Indicateurs clés de performance en temps réel, répartition visuelle du parc par marque/modèle via Chart.js, état du spouleur d'impression Windows hôte et alertes d'intégrité système.
- **Notification Événementielle en Temps Réel :** Connexion permanente SignalR (`/hubs/events`) propageant instantanément les événements de rafraîchissement d'inventaire vers les consoles Web et les clients de bureau connectés.
- **Sauvegarde & Restauration Complète :** Exportation et réimportation transactionnelle en JSON de l'intégralité de la configuration du serveur (15 domaines de données).

### 2.6. Sécurité Granulaire (RBAC) & Annuaire Active Directory
- **Matrice Fine de 29 Permissions :** Contrôle d'accès basé sur les rôles régissant chaque action unitaire (lecture, écriture, suppression, synchronisation, scan réseau, gestion des réglages, etc.).
- **Intégration Active Directory Hybride :** Authentification transparente des comptes de domaine via `PrincipalContext` / `DirectoryEntry` avec négociation scellée et recherche LDAP récursive des groupes imbriqués (`LDAP_MATCHING_RULE_IN_CHAIN`).
- **Mappage Dynamique Rôles/Groupes :** Association directe de groupes de sécurité Active Directory aux rôles applicatifs d'Autoprint.
- **Mur de Sécurité (*Security Wall*) :** Blocage contextuel imposant le renouvellement immédiat du mot de passe en cas d'expiration périodique ou après une réinitialisation administrative.

---

## 3. Architecture & Technologies

La solution se compose de projets complémentaires articulés autour du framework unifié **.NET 10.0** :

| Domaine / Projet | Technologie | Rôle Exact dans la Solution |
| :--- | :--- | :--- |
| **Serveur & API**<br/>`Autoprint.Server` | ASP.NET Core 10, EF Core 10, SignalR | Cœur de la solution : héberge l'API REST, les contrôleurs de spouleur Windows, le hub temps réel, les tâches de fond périodiques (monitoring, découverte réseau, purge d'audit) et la persistance. Hébergé sous IIS ou en CLI autonome. |
| **Console Web Admin**<br/>`Autoprint.Web` | Blazor WebAssembly (.NET 10), Radzen.Blazor 8.3.8, Chart.js, Bootstrap 5.3 | Application Web monopage (SPA) exécutée côté client dans le navigateur. Offre la gestion complète du parc, des lieux, des profils SNMP, des rôles et des paramètres système. |
| **Client Bureau Utilisateur**<br/>`Autoprint.Client` | WPF (.NET 10-windows), MaterialDesignThemes 5.3.0, Hardcodet.NotifyIcon 2.0.1, SQLite Local | Agent de bureau résident en barre des tâches. Géolocalise le poste via sous-réseau IP CIDR, met en cache les imprimantes, pilote l'imprimante par défaut et communique avec le serveur et le service Windows local. |
| **Service Système Machine**<br/>`Autoprint.Service` | .NET 10 Windows Service (`BackgroundService`), Named Pipe IPC ACL | Service Windows d'arrière-plan exécuté en compte `LocalSystem`. Écoute sur `\\.\pipe\AutoprintPipe` pour réaliser les installations silencieuses de pilotes d'impression système via `printui.dll` et les écritures dans `HKLM`. |
| **Contrats & Modèles Partagés**<br/>`Autoprint.Shared` | .NET 10 Class Library, EF Core Annotations | Référentiel commun contenant les entités ORM, les DTOs d'API, les énumérations système, les contrats IPC et les modèles de sauvegarde partagés entre le serveur, le web et le client. |
| **Assistant Installateur Serveur**<br/>`Autoprint.Installer.Server.UI` | WPF (.NET 10-windows), Microsoft.Web.Administration | Assistant graphique d'installation serveur : configure le pool d'application IIS en `LocalSystem`, teste les connexions SQL Server, génère les clés JWT et exécute les migrations initiales. |
| **Paquet MSI Serveur**<br/>`Autoprint.Server.Setup` | WiX Toolset v5 | Projet de création de package Windows Installer (MSI) pour le déploiement automatisé du serveur d'impression. |
| **Paquet MSI Client**<br/>`Autoprint.Client.Setup` | WiX Toolset v5 | Projet de création de package Windows Installer (MSI) déployable par GPO, Microsoft Intune ou SCCM pour installer l'agent client et le service Windows. |

---

## 4. Arborescence du Projet

```text
Autoprint/
├── Autoprint.Client/                     # Application de bureau WPF (.NET 10) pour les utilisateurs finaux
│   ├── Data/                             # Contexte EF Core SQLite local (ClientDbContext)
│   ├── Models/                           # Modèles locaux et structures de données déconnectées
│   ├── Services/                         # Services réseau (CIDR), API HTTP, IPC, registre et SignalR
│   ├── ViewModels/                       # Modèles de vue MVVM (MainWindow, ManagePrinters, Options)
│   ├── Views/                            # Vues XAML (PrintersView, ManagePrintersView, OptionsView)
│   ├── App.xaml / App.xaml.cs            # Gestion du cycle de vie, Mutex d'instance unique et écouteur IPC
│   ├── MainWindow.xaml / .cs             # Fenêtre principale unifiée Material Design
│   └── MissingPrinterDialog.xaml / .cs   # Boîte de dialogue d'avertissement d'imprimante favorite retirée
│
├── Autoprint.Client.Setup/               # Projet d'installeur WiX v5 (MSI) pour l'agent de bureau
│
├── Autoprint.Installer.Server.UI/        # Assistant WPF de déploiement et configuration IIS / SQL Server
│   ├── App.xaml / App.xaml.cs            # Point d'entrée de l'installateur avec élévation UAC
│   └── MainWindow.xaml / .cs             # Assistant graphique étape par étape
│
├── Autoprint.Server/                     # API Web ASP.NET Core (.NET 10) et serveur d'impression central
│   ├── Controllers/                      # 17 contrôleurs REST (Auth, Printers, Spooler, SNMP, Audit, etc.)
│   ├── Data/                             # ApplicationDbContext, DbInitializer et migrations EF Core
│   ├── Filters/                          # Filtres d'authentification API (ApiKeyAttribute pour X-Api-Token)
│   ├── Hubs/                             # Hub SignalR temps réel (EventsHub sur /hubs/events)
│   ├── Models/                           # Entités de sécurité RBAC internes (Users, Roles, Permissions)
│   ├── Services/                         # Logique métier (Spooler P/Invoke, WMI, SNMP, Predictif, Rapports, Audit)
│   ├── appsettings.json                  # Fichier de configuration hôte, chaînes de connexion et JWT
│   └── Program.cs                        # Initialisation de l'application, injection IoC et pipeline HTTP
│
├── Autoprint.Server.Setup/               # Projet d'installeur WiX v5 (MSI) pour le serveur d'impression
│
├── Autoprint.Service/                    # Service Windows (.NET 10) privilégié en compte LocalSystem
│   ├── Services/                         # NamedPipeServer (\\.\pipe\AutoprintPipe) et PrinterEngine
│   ├── Program.cs                        # Configuration de l'hôte Windows Service
│   └── Worker.cs                         # Boucle d'exécution d'arrière-plan du service
│
├── Autoprint.Shared/                     # Bibliothèque partagée (.NET 10) entre tous les projets
│   ├── DTOs/                             # Contrats de données typés par domaine fonctionnel
│   ├── IPC/                              # Structures de messages inter-processus (IpcRequest, IpcResponse)
│   ├── AuditLog.cs                       # Entité de traçabilité des modifications
│   ├── Imprimante.cs                     # Entité centrale de file d'impression
│   ├── RefData.cs                        # Référentiels (Marque, Modele, Emplacement, EmplacementNetwork)
│   ├── ServerSetting.cs                  # Entité clé-valeur de configuration serveur
│   ├── SnmpProfile.cs                    # Entité de profil de télémétrie SNMP
│   └── SnmpProfileItem.cs                # Entité de capteur SNMP individuel
│
├── Autoprint.Web/                        # Console d'administration Web Blazor WebAssembly (.NET 10)
│   ├── Layout/                           # Squelettes de navigation (MainLayout, NavMenu, EmptyLayout)
│   ├── Pages/                            # 30 composants Razor (Dashboard, Imprimantes, SNMP, Audit, etc.)
│   ├── wwwroot/                          # Fichiers statiques web (index.html, css/app.css, images, JS)
│   ├── _Imports.razor                    # Déclarations et directives d'importation globales
│   ├── App.razor                         # Routeur d'application Blazor
│   └── Program.cs                        # Point d'entrée WebAssembly et enregistrement des services
│
├── Docs/                                 # Manuels utilisateurs, guides d'installation et audits de sécurité
├── Directory.Build.props                 # Version globale centralisée de la solution (26.8.24.3)
├── Autoprint.sln                         # Solution unifiée Visual Studio 2026
├── PROJECT_CONTEXT.md                    # Synthèse architecturale et fonctionnelle de référence
├── PROJECT_MAP.md                        # Cadastre technique exhaustif de l'arborescence et des flux
└── README.md                             # Présente documentation officielle du produit
```

---

## 5. Principes de Sécurité & Gouvernance

### 5.1. Modèle Zéro-Privilège Utilisateur & Isolation IPC
- L'application de bureau [`Autoprint.Client`](Autoprint.Client/App.xaml.cs) s'exécute exclusivement dans le contexte de la session utilisateur connectée, sans nécessiter de droits d'administration locale.
- Les opérations système requérant de hauts privilèges (installation de pilotes de périphériques dans le magasin Windows via `rundll32 printui.dll`, écriture des paramètres machine dans `HKLM\SOFTWARE\Autoprint`) sont déléguées au service Windows local [`Autoprint.Service`](Autoprint.Service/Worker.cs).
- La communication inter-processus s'effectue via un tube nommé sécurisé (`\\.\pipe\AutoprintPipe`) dont la liste de contrôle d'accès (`PipeSecurity`) restreint explicitement les connexions aux comptes authentifiés locaux (`AuthenticatedUserSid`).

### 5.2. Cryptographie, Gestion des Sessions & Authentification
- **Hachage des Mots de Passe :** Utilisation de **PBKDF2 avec sel cryptographique** via `Microsoft.AspNetCore.Identity.PasswordHasher<string>`. Détection et migration transparente automatique des anciennes empreintes SHA-256 lors de la connexion réussie d'un utilisateur.
- **Jetons d'Accès JWT :** Tokens signés avec une clé secrète d'au moins 512 bits (`HmacSha512`), émis pour une durée paramétrable (défaut : 120 minutes) avec tolérance d'horloge nulle (`ClockSkew = TimeSpan.Zero`). Interdiction absolue de démarrer le serveur en production avec la clé de développement par défaut.
- **Jetons d'Intégration Tiers :** Les jetons d'API externe (`ap_tok_...`) générés pour les outils de supervision ne sont affichés qu'une seule fois à l'écran. Seule leur empreinte SHA-256 est persistée en base dans la table `IntegrationTokens`.
- **Authentification Agent Client :** Les requêtes HTTP émises par l'agent client vers les contrôleurs `/api/emplacements` et `/api/imprimantes` sont authentifiées par un secret d'infrastructure dédié transmis dans l'en-tête `X-Agent-Secret`.

### 5.3. Neutralisation des Injections & Désinfection des Entrées
- **Protection Anti-Injection LDAP (RFC 4515) :** Toutes les entrées utilisateur destinées aux filtres Active Directory sont échappées via [`SecurityHelper.EscapeLdapFilter`](Autoprint.Server/Services/SecurityHelper.cs), neutralisant les caractères de contrôle (`*`, `(`, `)`, `\`, `\0`).
- **Protection Anti-Injection SQL :** L'intégralité des accès aux données s'effectue au travers de requêtes paramétrées compilées par **Entity Framework Core 10**.
- **Protection Anti-Injection CSV (CWE-1236) :** Les exports CSV générés par le serveur échappent systématiquement les caractères susceptibles d'être interprétés comme des formules de calcul par Microsoft Excel (`=`, `+`, `-`, `@`, tabulations).
- **Assainissement des Lignes de Commande Système :** Les arguments passés à `printui.dll` ou aux utilitaires système sont rigoureusement validés et isolés au travers de listes d'arguments typées (`ProcessStartInfo.ArgumentList`), éliminant tout risque d'injection par concaténation de chaînes.

### 5.4. Masquage des Secrets & Traçabilité Complète
- **Masquage à l'Affichage :** Les contrôleurs d'API et l'interface Web masquent systématiquement les champs sensibles (`SmtpPass`, `AdServicePassword`, `AgentApiKey`) en renvoyant la valeur factice `********`. Une valeur inchangée transmise lors d'une sauvegarde préserve le secret déjà stocké sans l'écraser.
- **Journal d'Audit Différentiel (`AuditLogs`) :** Chaque opération administrative (création, modification, suppression, synchronisation, restauration) consigne un enregistrement horodaté UTC comprenant le nom de l'utilisateur, l'adresse IP cliente, l'action exécutée, ainsi que la sérialisation JSON différentielle de l'objet avant et après modification (`OldValues` / `NewValues`).
- **Purge Programmée :** Le service hébergé [`LogCleanupWorker`](Autoprint.Server/Services/LogCleanupWorker.cs) purge quotidiennement les entrées d'audit dont l'ancienneté dépasse la durée de rétention légale configurée (`LogRetentionDays`).

---

## 6. Installation, Configuration & Démarrage

### 6.1. Prérequis Système & Logiciels

#### Serveur d'Impression & API
- **Système d'Exploitation :** Windows Server 2019, Windows Server 2022 ou Windows 10/11 Pro/Enterprise (64-bit).
- **Rôles Windows :** Rôle *Serveur d'impression et de numérisation de documents* (Spouleur d'impression Windows `spoolsv.exe` actif), Service IIS 10.0+ avec module ASP.NET Core Hosting Bundle.
- **Runtime :** [.NET 10.0 ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/).
- **Base de Données :** Microsoft SQL Server 2016+ (Standard, Enterprise ou Express) ou SQLite (intégré par défaut).
- **Droits Système :** Le pool d'application IIS exécutant `Autoprint.Server` doit être configuré avec l'identité `LocalSystem` (requis pour interagir avec le spouleur d'impression Windows `winspool.drv` et WMI).

#### Postes Clients
- **Système d'Exploitation :** Windows 10 ou Windows 11 (64-bit).
- **Composants :** .NET 10.0 Desktop Runtime x64 (si binaire non autonome).
- **Réseau :** Connectivité HTTPS vers l'instance `Autoprint.Server` (port 443 ou port personnalisé).

---

### 6.2. Installation & Déploiement

#### Déploiement du Serveur via l'Assistant Graphique
1. Exécuter [`Autoprint.Installer.Server.UI.exe`](Autoprint.Installer.Server.UI/) avec élévation de privilèges administrateur.
2. Renseigner les paramètres de la base de données (sélectionner SQL Server avec authentification Windows ou saisir une chaîne de connexion personnalisée).
3. L'assistant crée le site IIS, configure le pool d'application sur `LocalSystem`, génère une clé de signature JWT de 512 bits aléatoire et exécute les migrations initiales (`Autoprint.Server.exe --migrate-only`).

#### Déploiement Silencieux du Client Bureau (Intune, GPO, SCCM)
L'installeur MSI généré par [`Autoprint.Client.Setup`](Autoprint.Client.Setup/) s'installe en mode silencieux en transmettant les propriétés machine requises dans `HKLM\SOFTWARE\Autoprint` :

```cmd
msiexec /i Autoprint.Client.Setup.msi PRINTSERVER="https://autoprint.entreprise.corp" APIKEY="VOTRE_AGENT_API_KEY_GUID" /qn /norestart
```

- `PRINTSERVER` : URL HTTPS du serveur d'API central.
- `APIKEY` : Clé secrète d'agent configurée dans les paramètres généraux d'Autoprint (`AgentApiKey`).

---

### 6.3. Configuration Serveur (`appsettings.json` & Variables d'Environnement)

Le fichier [`Autoprint.Server/appsettings.json`](Autoprint.Server/appsettings.json) définit la configuration d'amorçage de l'hôte :

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionStrings": {
      "SqlServer": "Server=localhost;Database=AutoprintDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
      "Sqlite": "Data Source=Autoprint.db"
    }
  },
  "AllowedHosts": "*",
  "ClientUrl": "https://autoprint.entreprise.corp",
  "Jwt": {
    "Key": "VOTRE_CLE_SECRETE_MINIMUM_512_BITS_A_GENERER_ALEATOIREMENT_EN_PRODUCTION",
    "Issuer": "AutoprintServer",
    "Audience": "AutoprintClient",
    "ExpireMinutes": 120
  }
}
```

#### Variables d'Environnement Système (Équivalent `.env` pour Déploiement Conteneur / CLI)
Toutes les sections du fichier `appsettings.json` sont surchargeables à l'exécution au moyen de variables d'environnement conventionnelles ASP.NET Core (séparateur double soulignement `__`) :

```dotenv
# Fournisseur de base de données : SqlServer ou Sqlite
Database__Provider=SqlServer

# Chaîne de connexion SQL Server de production
Database__ConnectionStrings__SqlServer=Server=sql.entreprise.corp;Database=AutoprintDb;User Id=app_autoprint;Password=SecretRobuste!;TrustServerCertificate=False;Encrypt=True;

# Chaîne de connexion SQLite alternative
Database__ConnectionStrings__Sqlite=Data Source=C:\Autoprint\Data\Autoprint.db

# URL publique de la console Web
ClientUrl=https://autoprint.entreprise.corp

# Clé cryptographique JWT (Obligatoire : 64 octets / 512 bits minimum)
Jwt__Key=f3b2c1d0e9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d3e2f1a0b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d3e2f1a0b9c8d7e6f5a4b3c2
Jwt__Issuer=AutoprintServer
Jwt__Audience=AutoprintClient
Jwt__ExpireMinutes=120

# Hôtes autorisés
AllowedHosts=*
```

---

### 6.4. Paramètres en Base de Données (`ServerSettings`)

Les paramètres applicatifs dynamiques sont stockés dans la table `ServerSettings` et éditables directement depuis la console Web (`/parametres`) :

| Clé | Type | Valeur par Défaut | Rôle & Description |
| :--- | :--- | :--- | :--- |
| `SmtpHost` | `STRING` | *(Vide)* | Nom d'hôte ou IP du serveur SMTP relais. |
| `SmtpPort` | `INT` | `587` | Port d'écoute SMTP (25, 465, 587). |
| `SmtpEnableSsl` | `BOOL` | `true` | Activation du chiffrement TLS/SSL. |
| `SmtpIgnoreCertError` | `BOOL` | `false` | Tolérance aux certificats auto-signés. |
| `SmtpAuthRequired` | `BOOL` | `true` | Authentification SMTP requise. |
| `SmtpUser` / `SmtpPass` | `STRING` / `PASSWORD` | *(Vide)* | Identifiants du compte expéditeur SMTP. |
| `SmtpFromAddress` | `STRING` | `autoprint@entreprise.corp` | Adresse e-mail d'expédition des rapports. |
| `NamingTemplate` | `STRING` | `IMP_{LIEU}_{MODELE}` | Gabarit de nommage standard des files Windows. |
| `NamingEnabled` | `BOOL` | `true` | Application automatique du gabarit lors de la création d'imprimante. |
| `NamingSameShare` | `BOOL` | `true` | Attribution du même nom pour le partage réseau. |
| `AdDomain` | `STRING` | `entreprise.corp` | Nom FQDN du domaine Active Directory. |
| `AdBaseDn` | `STRING` | `DC=entreprise,DC=corp` | Racine LDAP de recherche des utilisateurs et groupes. |
| `AdLdapFilter` | `STRING` | `(&(objectClass=user)(sAMAccountName={0}))` | Filtre LDAP de recherche d'utilisateur. |
| `AdUseServiceAccount` | `BOOL` | `false` | Utilisation d'un compte de service dédié pour les requêtes LDAP. |
| `AdServiceUser` / `AdServicePassword`| `STRING` / `PASSWORD` | *(Vide)* | Identifiants du compte de service Active Directory. |
| `PasswordExpirationDays` | `INT` | `90` | Période de validité des mots de passe locaux avant blocage. |
| `AgentApiKey` | `STRING` | `00000000-0000-0000-0000-000000000000` | GUID secret requis dans l'en-tête `X-Agent-Secret` du client. |
| `LogRetentionDays` | `INT` | `180` | Durée de conservation des événements d'audit avant purge. |
| `Monitoring_Enabled` | `BOOL` | `true` | Activation du worker de supervision SNMP en tâche de fond. |
| `Monitoring_IntervalMinutes` | `INT` | `60` | Intervalle entre deux scans SNMP automatiques du parc. |
| `Monitoring_StartHour` / `EndHour` | `INT` / `INT` | `8` / `18` | Plage horaire quotidienne d'exécution des scans. |
| `Printer_DefaultDirectModeEnabled` | `BOOL` | `false` | Activation par défaut du mode d'impression directe filiale. |

---

### 6.5. Commandes de Lancement & Maintenance

#### Démarrage en Mode Développement
```bash
# Lancement de l'API Serveur et de l'Hôte Blazor WebAssembly
dotnet run --project Autoprint.Server

# Lancement simultané du Client de Bureau WPF
dotnet run --project Autoprint.Client
```

#### Commandes CLI de Maintenance Serveur
Le binaire serveur [`Autoprint.Server.exe`](Autoprint.Server/Program.cs) dispose d'options en ligne de commande pour la maintenance automatisée et les scripts de déploiement :

```cmd
:: 1. Exécuter uniquement les migrations EF Core et l'initialisation de base (sans démarrer le serveur Web)
Autoprint.Server.exe --migrate-only

:: 2. Réinitialiser le compte administrateur local par défaut (identifiant 'admin', mot de passe 'admin123', avec obligation de changement)
Autoprint.Server.exe --reset-admin
```

#### Gestion du Service Système Windows
```cmd
:: Démarrer le service d'installation de pilotes
sc.exe start AutoprintService

:: Arrêter le service
sc.exe stop AutoprintService

:: Consulter le statut
sc.exe query AutoprintService
```

---

## 7. Assurance Qualité & Tests

### 7.1. Validation de la Compilation de la Solution
La validation continue de l'intégrité de la base de code s'effectue en compilant l'ensemble de la solution sur toutes les cibles :

```bash
# Restauration des dépendances NuGet et compilation Release
dotnet build Autoprint.sln -c Release
```

### 7.2. Tests & Diagnostics Intégrés au Runtime
L'application intègre des modules natifs de diagnostic et d'auto-test directement exploitables depuis l'API et la console d'administration :
- **Diagnostic SNMP en Temps Réel (`POST /api/SnmpProfiles/scan-printer`) :** Teste instantanément la connectivité UDP/ICMP et la résolution MIB RFC 3805 sur une adresse IP donnée.
- **Auto-Test SMTP (`POST /api/Settings/test-email`) :** Vérifie la négociation TLS, l'authentification et l'envoi d'un message d'épreuve vers le serveur de messagerie d'entreprise.
- **Auto-Test Active Directory (`POST /api/Settings/test-ad`) :** Évalue la résolution DNS du domaine, l'ouverture de session LDAP (`PrincipalContext`) et la portée de recherche des conteneurs d'utilisateurs.
- **Prévisualisation de Réconciliation du Spouleur (`GET /api/Sync/preview`) :** Simule et affiche la liste exhaustive des opérations de création, modification, renommage et suppression qui seront soumises à `winspool.drv` avant toute application effective.
- **Intégrité de la Persistance :** Le commutateur `--migrate-only` vérifie au déploiement la validité des modèles relationnels EF Core et applique les scripts DDL différentiels sans risque de corruption.

---

## 8. Documentation Complémentaire

Pour approfondir l'architecture, l'exploitation et la maintenance du projet, consultez les documents techniques de référence :

- 📐 **[PROJECT_CONTEXT.md](PROJECT_CONTEXT.md)** : Vue d'ensemble architecturale, contexte métier, flux de données critiques et modèle de sécurité complet.
- 🗺️ **[PROJECT_MAP.md](PROJECT_MAP.md)** : Cadastre technique exhaustif de l'ensemble des fichiers, composants, contrôleurs, tables BDD, DTOs et recettes d'intervention pas à pas.
- 📖 **[Guide d'Installation Serveur (Docs/INSTALLATION_SERVER.MD)](Docs/INSTALLATION_SERVER.MD)** : Procédure pas à pas pour Windows Server et IIS.
- 💻 **[Documentation Technique Client (Docs/_DOC_Client.md)](Docs/_DOC_Client.md)** : Fonctionnement interne de l'agent de bureau WPF et du cache local SQLite.
- 🛡️ **[Dossier de Sécurité (Docs/_DOC_Securite.md)](Docs/_DOC_Securite.md)** : Détail des protections cryptographiques, anti-injections et règles d'audit.

---

<div align="center">
  <sub>Autoprint (Version 26.8.24.3) - Solution d'impression et supervision pour environnements d'entreprise.</sub>
</div>