# PROJECT_MAP.md

Cadastre technique exhaustif de l'arborescence, des composants, du graphe d'appels et des flux de données de la solution **Autoprint** (Version `26.8.24.3`).

---

## 1. Démarrage & Cycle de Vie

### 1.1. Points d'Entrée & Commandes de Lancement

| Sous-Système | Projet | Fichier Point d'Entrée | Commande de Lancement / Mode |
| :--- | :--- | :--- | :--- |
| **Serveur d'API & Hôte Web** | `Autoprint.Server` | [`Program.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Program.cs) | `dotnet run --project Autoprint.Server`<br/>Production : Hébergé sous IIS (`w3wp.exe`) via le module ASP.NET Core Hosting Bundle.<br/>CLI Maintenance : `Autoprint.Server.exe --migrate-only` ou `Autoprint.Server.exe --reset-admin` |
| **Frontend Web Admin** | `Autoprint.Web` | [`Program.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Program.cs) | Téléchargé et exécuté dans le navigateur client via WebAssembly depuis l'hôte serveur (`_framework/blazor.webassembly.js`). En dev local : `dotnet watch --project Autoprint.Web` |
| **Client Bureau Utilisateur** | `Autoprint.Client` | [`App.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/App.xaml.cs) | `Autoprint.Client.exe`<br/>Arguments optionnels : `--firstrun`, `--silent`, `--api-key <CLE>`, `--print-server <SERVEUR>`, `--config-path <DOSSIER>` |
| **Service Système Windows** | `Autoprint.Service` | [`Program.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Service/Program.cs) | Géré par le Windows Service Control Manager (`sc.exe start AutoprintService`). En debug console : exécution directe du binaire. |
| **Installateur Serveur** | `Autoprint.Installer.Server.UI` | [`App.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Installer.Server.UI/App.xaml.cs) | `Autoprint.Installer.Server.UI.exe` (Exécute avec élévation UAC `requireAdministrator`). |

---

### 1.2. Séquence d'Initialisation Détaillée

#### A. Initialisation du Serveur (`Autoprint.Server`)
1. **Configuration de l'environnement hôte :** [`Program.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Program.cs) instancie le `WebApplicationBuilder` avec `ContentRootPath = AppContext.BaseDirectory`.
2. **Fournisseur de Persistance :** Lecture de la section `Database:Provider` (`SqlServer` ou `Sqlite`). Injection de [`ApplicationDbContext`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/ApplicationDbContext.cs) avec fractionnement des requêtes (`SplitQuery`) pour SQL Server, ou chemin absolu de fichier pour `Autoprint.db` en SQLite.
3. **Injection des Services Métier (Scoped) :** Enregistrement de [`WindowsPrintSpoolerService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/WindowsPrintSpoolerService.cs), [`DriverService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/DriverService.cs), [`NamingService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/NamingService.cs), [`AuthService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/AuthService.cs), [`EmailService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/EmailService.cs), [`ReportGeneratorService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/ReportGeneratorService.cs), [`SyncSpoolerService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/SyncSpoolerService.cs), [`SnmpService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/SnmpService.cs), [`PredictiveService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/PredictiveService.cs), [`AuditService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/AuditService.cs), [`DiscoveryService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/DiscoveryService.cs).
4. **Hub Temps Réel :** Enregistrement de SignalR (`builder.Services.AddSignalR()`).
5. **Démons d'Arrière-Plan (Hosted Services) :** Enregistrement de [`DiscoveryWorker`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/DiscoveryWorker.cs), [`LogCleanupWorker`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/LogCleanupWorker.cs), et [`PrinterMonitoringWorker`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/PrinterMonitoringWorker.cs).
6. **Sécurité JWT :** Extraction de `Jwt:Key`. Si la clé par défaut de développement est détectée hors environnement `Development`, le démarrage est interrompu immédiatement par une exception bloquante. Configuration de l'authentification `JwtBearer` avec horloge sans dérive (`ClockSkew = TimeSpan.Zero`).
7. **Contrôleurs & Sérialisation :** Configuration de `ControllersWithViews` avec `ReferenceHandler.IgnoreCycles` et camelCase.
8. **Sécurité RBAC :** Déclaration de 29 politiques d'autorisation granulaire (`ADMIN_ACCESS`, `PRINTER_READ`, `PRINTER_WRITE`, etc.).
9. **Construction du Pipeline HTTP (`WebApplication.Build`) :**
   - Interception de l'argument `--migrate-only` : exécution de `Database.Migrate()` / `EnsureCreated()`, appel à [`DbInitializer.Initialize()`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/DbInitializer.cs), puis sortie avec code 0 (ou 1 en cas d'erreur).
   - Middleware Swagger UI activé en environnement de développement.
   - Redirection HTTPS, fichiers Blazor WebAssembly (`UseBlazorFrameworkFiles`), fichiers statiques (`UseStaticFiles`), politique CORS (`AllowBlazorOrigin`).
   - Authentification (`UseAuthentication`) et Autorisation (`UseAuthorization`).
   - Routage : Razor Pages, Contrôleurs API, Hub SignalR [`EventsHub`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Hubs/EventsHub.cs) sur `/hubs/events`, et page de repli `index.html`.
10. **Amorçage Automatique de la Base au Démarrage :** Portée de service temporaire pour exécuter les migrations EF Core et [`DbInitializer.Initialize()`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/DbInitializer.cs) (création des tables, injection des permissions, création du compte `admin` initial si absent, synchronisation des clés de configuration par défaut).
11. **Interception `--reset-admin` :** Si spécifié en ligne de commande, réinitialisation du mot de passe `admin` à `admin123` avec `ForceChangePassword = true`, puis arrêt.
12. **Exécution continue :** `app.Run()`.

#### B. Initialisation du Client Desktop (`Autoprint.Client`)
1. **Verrouillage Mutex :** [`App.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/App.xaml.cs) tente d'acquérir le mutex `Local\AutoprintClient_Mutex_{UserName}`. Si une instance existe déjà, le client transmet un signal `WAKE_UP` via le tube nommé local `AutoprintClient_WakeUp_{UserName}` et termine le processus dupliqué.
2. **Écouteur IPC de Réveil :** L'instance maîtresse écoute en tâche de fond sur `AutoprintClient_WakeUp_{UserName}` pour restaurer la fenêtre principale au premier plan lors des lancements ultérieurs.
3. **Thématisation Dynamique :** Abonnement aux événements système `UserPreferenceChanged` pour synchroniser le thème sombre/clair Material Design avec Windows.
4. **Intégration Barre des Tâches :** Création de l'icône systray [`Hardcodet.Wpf.TaskbarNotification`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/App.xaml).
5. **Services & Configuration :**
   - [`ConfigurationService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/ConfigurationService.cs) extrait `PRINTSERVER` et `APIKEY` de `HKLM\SOFTWARE\Autoprint` (avec prise en compte des arguments CLI).
   - [`PathService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/PathService.cs) prépare les répertoires locaux `%LOCALAPPDATA%\Autoprint\` et `%USERPROFILE%\Documents\Autoprint\`.
   - [`UserPreferencesService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/UserPreferencesService.cs) charge `user-settings.json`, configure le démarrage automatique Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) et génère le raccourci bureau au premier démarrage.
   - [`DataService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/DataService.cs) initialise ou répare le cache local SQLite `client_cache.db`.
   - [`ApiService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/ApiService.cs) configure le client HTTP avec l'en-tête `X-Agent-Secret`.
6. **Liaison Temps Réel SignalR :** [`RealTimeService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/RealTimeService.cs) se connecte au hub `/hubs/events` du serveur distant et déclenche `RafraichirDonneesAsync` lors de la réception de `RefreshPrinters`.
7. **Surveillance Réseau :** Abonnement à `NetworkChange.NetworkAddressChanged` pour déclencher une détection automatique avec temporisation anti-rebond de 3 secondes.
8. **Premier Scan :** Appel de `RafraichirDonneesAsync(estDemarrage: true)` avec détection de l'IP active, matching CIDR, lecture du cache/API et application de l'imprimante par défaut.

#### C. Initialisation du Service Windows (`Autoprint.Service`)
1. [`Program.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Service/Program.cs) configure le worker d'hôte avec `AddWindowsService(ServiceName = "AutoprintService")`.
2. [`Worker.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Service/Worker.cs) démarre et instancie [`PrinterEngine`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Service/Services/PrinterEngine.cs) et [`NamedPipeServer`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Service/Services/NamedPipeServer.cs).
3. Le serveur de tube nommé écoute sur `\\.\pipe\AutoprintPipe` avec des permissions sécurisées restreintes aux utilisateurs authentifiés (`AuthenticatedUserSid`), prêt à exécuter l'installation machine de pilotes (`printui.dll /ia`) et les écritures dans `HKLM\SOFTWARE\Autoprint`.

---

## 2. Cadastre des Composants & Logique Métier

### 2.1. Noyau Système, Configuration, Sécurité & DAL

| Chemin du fichier | Classe / Rôle principal | Dépendances / Appelé par | Responsabilité exacte |
| :--- | :--- | :--- | :--- |
| [`Autoprint.Server/Program.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Program.cs) | Point d'entrée serveur | ASP.NET Core, EF Core, SignalR | Configuration du conteneur IoC, pipeline HTTP, migrations CLI, démarrage API. |
| [`Autoprint.Server/Data/ApplicationDbContext.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/ApplicationDbContext.cs) | `ApplicationDbContext` | EF Core, `Autoprint.Shared` | Déclaration des `DbSet`, relations relationnelles, interception `SaveChanges` pour horodatage UTC automatique, seed initial des permissions. |
| [`Autoprint.Server/Data/DbInitializer.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/DbInitializer.cs) | `DbInitializer` | `ApplicationDbContext`, SQL DDL | Migration dynamique DDL de secours (SQLite / SQL Server), injection des paramètres par défaut, création de l'admin initial. |
| [`Autoprint.Server/Filters/ApiKeyAttribute.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Filters/ApiKeyAttribute.cs) | `ApiKeyAttribute` | `IAsyncActionFilter`, EF Core | Validation de l'en-tête `X-Api-Token` contre la table `IntegrationTokens` (hachage SHA-256) pour les routes d'export monitoring. |
| [`Autoprint.Server/Services/SecurityHelper.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/SecurityHelper.cs) | `SecurityHelper` | `PasswordHasher<string>`, SHA256 | Hachage PBKDF2 avec sel, vérification et détection des anciens hashes SHA-256 à migrer, échappement RFC 4515 anti-injection LDAP. |
| [`Autoprint.Server/Services/AuthService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/AuthService.cs) | `AuthService` : `IAuthService` | `PrincipalContext`, `DirectoryEntry`, JWT | Authentification locale ou Active Directory, résolution récursive des groupes AD (`member:1.2.840.113556.1.4.1941`), synchronisation utilisateur, génération de tokens JWT. |
| [`Autoprint.Server/Services/AuditService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/AuditService.cs) | `AuditService` | `ApplicationDbContext`, System.Text.Json | Journalisation structurée dans `AuditLogs`, sérialisation différentielle JSON (`OldValues` vs `NewValues`). |
| [`Autoprint.Server/Hubs/EventsHub.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Hubs/EventsHub.cs) | `EventsHub` : `Hub` | SignalR Core | Canal WebSocket de diffusion des événements de rafraîchissement (`RefreshPrinters`) vers les clients Web et Desktop. |
| [`Autoprint.Client/Data/ClientDbContext.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Data/ClientDbContext.cs) | `ClientDbContext` | EF Core SQLite | Contexte de persistance local du poste de travail pour le cache déconnecté. |
| [`Autoprint.Client/Services/DataService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/DataService.cs) | `DataService` | `ClientDbContext`, SQLite | Remplacement transactionnel du cache local lors des synchronisations avec réinitialisation automatique en cas de corruption SQLite. |
| [`Autoprint.Client/Services/ConfigurationService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/ConfigurationService.cs) | `ConfigurationService` | Registre Windows (`HKLM`) | Lecture des paramètres machine `PRINTSERVER` et `APIKEY`, gestion des surcharges CLI. |
| [`Autoprint.Client/Services/UserPreferencesService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/UserPreferencesService.cs) | `UserPreferencesService` | JSON, Registre (`HKCU`) | Gestion des préférences utilisateur (`user-settings.json`), favoris par site, raccourci bureau et clé `Run`. |
| [`Autoprint.Client/Services/PathService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/PathService.cs) | `PathService` | `Environment.SpecialFolder` | Résolution des répertoires de stockage `%LOCALAPPDATA%` et `%USERPROFILE%\Documents`. |

---

### 2.2. Services Métier

| Chemin du fichier | Classe / Rôle principal | Dépendances / Appelé par | Responsabilité exacte |
| :--- | :--- | :--- | :--- |
| [`Autoprint.Server/Services/SyncSpoolerService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/SyncSpoolerService.cs) | `SyncSpoolerService` : `ISyncSpoolerService` | `IPrintSpoolerService`, SignalR | Moteur de réconciliation spouleur : calcule le différentiel d'état, orchestre les créations, modifications, renommages et suppressions de files Windows. |
| [`Autoprint.Server/Services/WindowsPrintSpoolerService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/WindowsPrintSpoolerService.cs) | `WindowsPrintSpoolerService` : `IPrintSpoolerService` | `winspool.drv` (P/Invoke), WMI | Interaction directe bas niveau avec Windows : création de ports TCP/IP (`Win32_TCPIPPrinterPort`), files (`Win32_Printer`), renommage WMI, écriture registre mode direct filiale (`BranchOffice`). |
| [`Autoprint.Server/Services/SnmpService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/SnmpService.cs) | `SnmpService` : `ISnmpService` | `SharpSnmpLib`, Ping ICMP | Diagnostic réseau, interrogation groupée UDP des OIDs de consommables/bacs, auto-découverte par balayage arborescent Printer-MIB (RFC 3805). |
| [`Autoprint.Server/Services/PredictiveService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/PredictiveService.cs) | `PredictiveService` : `IPredictiveService` | Mathématiques | Algorithme de régression linéaire par moindres carrés pour estimer le nombre de jours restant avant épuisement de chaque cartouche. |
| [`Autoprint.Server/Services/ReportGeneratorService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/ReportGeneratorService.cs) | `ReportGeneratorService` : `IReportGeneratorService` | `PdfSharpCore`, `ISnmpService` | Extraction des données filtrées par règles dynamiques, génération vectorielle des rapports PDF avec jauges graphiques et export CSV. |
| [`Autoprint.Server/Services/EmailService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/EmailService.cs) | `EmailService` : `IEmailService` | `System.Net.Mail.SmtpClient` | Envoi d'e-mails HTML et de pièces jointes (rapports, alertes découverte) selon les paramètres SMTP stockés en base. |
| [`Autoprint.Server/Services/DiscoveryService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/DiscoveryService.cs) | `DiscoveryService` | `SharpSnmpLib`, TCP, Ping | Balayage réseau multi-threadé (`Parallel.ForEachAsync`) pour la détection de nouvelles imprimantes (port 9100) et de nouveaux sous-réseaux. |
| [`Autoprint.Server/Services/NamingService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/NamingService.cs) | `NamingService` : `INamingService` | Regex, `ServerSettings` | Génération de noms standardisés d'imprimantes à partir d'un gabarit de variables (`{LIEU}`, `{MODELE}`, `{IP}`, etc.). |
| [`Autoprint.Server/Services/DriverService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/DriverService.cs) | `DriverService` : `IDriverService` | `IPrintSpoolerService` | Inventaire et catégorisation des pilotes d'impression installés sur le serveur Windows hôte. |
| [`Autoprint.Server/Services/AlertService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/AlertService.cs) | `AlertService` | `ApplicationDbContext` | Évaluation des alertes d'intégrité globale (pilotes manquants dans Windows, modèles cassés). |
| [`Autoprint.Server/Services/PrinterMonitoringWorker.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/PrinterMonitoringWorker.cs) | `PrinterMonitoringWorker` : `BackgroundService` | `ISnmpService`, `IReportGeneratorService` | Tâche de fond périodique : scan SNMP de santé, historisation des toners, archivage automatique des imprimantes inactives (>30j) et envoi des rapports planifiés. |
| [`Autoprint.Server/Services/DiscoveryWorker.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/DiscoveryWorker.cs) | `DiscoveryWorker` : `BackgroundService` | `DiscoveryService` | Tâche de fond périodique : exécution des profils de découverte réseau selon le calendrier planifié. |
| [`Autoprint.Server/Services/LogCleanupWorker.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/LogCleanupWorker.cs) | `LogCleanupWorker` : `BackgroundService` | `ApplicationDbContext` | Tâche de fond quotidienne : purge des entrées `AuditLogs` plus anciennes que le seuil de rétention. |
| [`Autoprint.Client/Services/NetworkService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/NetworkService.cs) | `NetworkService` | `System.Net.Sockets` | Détermination de l'adresse IPv4 locale du poste client routable vers le serveur d'impression cible. |
| [`Autoprint.Client/Services/IpHelper.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/IpHelper.cs) | `IpHelper` | Mathématiques réseau binaire | Vérification d'appartenance d'une adresse IPv4 à un bloc de sous-réseau CIDR (`IsIpInCidr`). |
| [`Autoprint.Client/Services/RealTimeService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/RealTimeService.cs) | `RealTimeService` | `HubConnection` (SignalR) | Maintien de la connexion WebSocket avec le serveur et notification de rafraîchissement au client WPF. |
| [`Autoprint.Client/Services/ApiService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Services/ApiService.cs) | `ApiService` | `HttpClient`, JSON | Appels HTTP vers les contrôleurs `/api/emplacements` et `/api/imprimantes` avec jeton secret machine. |
| [`Autoprint.Service/Services/NamedPipeServer.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Service/Services/NamedPipeServer.cs) | `NamedPipeServer` | `NamedPipeServerStreamAcl` | Écoute IPC sur `AutoprintPipe`, réception des commandes `INSTALL_DRIVER` et `UPDATE_CONFIG`. |
| [`Autoprint.Service/Services/PrinterEngine.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Service/Services/PrinterEngine.cs) | `PrinterEngine` | `rundll32 printui.dll` | Exécution privilégiée en compte SYSTEM de l'installation silencieuse de pilotes d'impression machine. |

---

### 2.3. Contrôleurs d'API (`Autoprint.Server/Controllers`)

| Fichier Contrôleur | Route de Base | Politiques / Sécurité | Endpoints & Rôle Métier |
| :--- | :--- | :--- | :--- |
| [`AlertsController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/AlertsController.cs) | `/api/Alerts` | `[Authorize]` | `GET` : État des alertes système (pilotes Windows absents, modèles orphelins). |
| [`AuditController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/AuditController.cs) | `/api/Audit` | `AUDIT_READ`, `SuperAdmin` | `GET` (pagination, recherche texte), `GET {id}`, `DELETE cleanup` (purge manuelle >30j). |
| [`AuthController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/AuthController.cs) | `/api/Auth` | `[AllowAnonymous]` / `[Authorize]` | `POST login` (génération JWT), `GET me`, `POST change-password`, `GET ad-search` (recherche AD). |
| [`BackupController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/BackupController.cs) | `/api/Backup` | `ADMIN_ACCESS` | `GET export` (sauvegarde JSON totale), `POST restore` (restauration transactionnelle). |
| [`DashboardController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/DashboardController.cs) | `/api/Dashboard` | `[Authorize]` | `GET` : Métriques globales du parc, répartition des modèles, statut du Spouleur Windows et version serveur. |
| [`DiscoveryController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/DiscoveryController.cs) | `/api/Discovery` | `NETWORK_SCAN` | CRUD des profils de découverte, `POST {id}/scan` (lancement immédiat), `POST scan-import` (scan CIDR port 9100/SNMP). |
| [`EmplacementsController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/EmplacementsController.cs) | `/api/Emplacements` | `LOCATION_READ`, `WRITE`, `DELETE`, `X-Agent-Secret` | CRUD des lieux physiques et de leurs sous-réseaux CIDR IPv4 (`EmplacementNetwork`). |
| [`ImportController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/ImportController.cs) | `/api/Import` | `PRINTER_READ`, `WRITE`, `DRIVER_SCAN` | `POST Scan/Printers` (WMI distant), `POST Import/Printers`, `GET Scan/Drivers` (WMI local), `POST Import/Drivers`. |
| [`ImprimantesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/ImprimantesController.cs) | `/api/Imprimantes` | `PRINTER_READ`, `WRITE`, `DELETE`, `ARCHIVE`, `X-Agent-Secret` | CRUD complet des imprimantes, archivage/désarchivage, `GET {id}/diagnostic` (SNMP live), `GET {id}/toner-history`, `GET {id}/toner-prediction/{color}`. |
| [`IntegrationTokensController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/IntegrationTokensController.cs) | `/api/IntegrationTokens` | `SETTINGS_MANAGE` | CRUD des jetons d'intégration API (`ap_tok_...`) hachés en SHA-256. |
| [`MarquesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/MarquesController.cs) | `/api/Marques` | `BRAND_READ`, `BRAND_WRITE`, `BRAND_DELETE` | CRUD des marques d'imprimantes avec décompte des modèles associés. |
| [`ModelesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/ModelesController.cs) | `/api/Modeles` | `MODEL_READ`, `MODEL_WRITE`, `MODEL_DELETE` | CRUD des modèles matériels, association du pilote d'impression et du profil SNMP. |
| [`MonitoringController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/MonitoringController.cs) | `/api/v1/Monitoring` | `[ApiKey]` (`X-Api-Token`) | `GET export` : Export JSON structuré des périphériques en alerte/hors-ligne pour intégration SI/monitoring externe. |
| [`NamingController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/NamingController.cs) | `/api/Naming` | `[Authorize]` | `POST preview` : Prévisualisation dynamique d'un nom d'imprimante selon un gabarit. |
| [`PilotesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/PilotesController.cs) | `/api/Pilotes` | `DRIVER_READ`, `DRIVER_SCAN` | Consultation et suppression des pilotes enregistrés dans le référentiel. |
| [`ReportSchedulesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/ReportSchedulesController.cs) | `/api/ReportSchedules` | `REPORT_MANAGE` | CRUD des planifications de rapports périodiques, `POST {id}/send` (génération et envoi immédiat par e-mail). |
| [`RolesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/RolesController.cs) | `/api/Roles` | `ROLE_READ`, `ROLE_WRITE`, `ROLE_DELETE` | CRUD des rôles, gestion des associations de permissions, mapping des groupes Active Directory (`AdRoleMappings`). |
| [`SettingsController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/SettingsController.cs) | `/api/Settings` | `SETTINGS_MANAGE` | Consultation avec masquage des secrets, enregistrement global avec audit, tests de connectivité SMTP et Active Directory. |
| [`SnmpProfilesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/SnmpProfilesController.cs) | `/api/SnmpProfiles` | `SNMP_PROFILE_READ`, `WRITE`, `DELETE` | CRUD des profils et items SNMP, `POST scan-printer` (auto-découverte MIB), `POST test-profile`, `GET {id}/export`, `POST import`. |
| [`SyncController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/SyncController.cs) | `/api/Sync` | `PRINTER_SYNC` | `GET preview` (liste des changements en attente), `POST apply` (application par lot dans le Spouleur Windows). |
| [`UsersController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/UsersController.cs) | `/api/Users` | `USER_READ`, `USER_WRITE`, `USER_DELETE` | CRUD des utilisateurs locaux et AD, réinitialisation de mot de passe (`POST {id}/reset-password`). |

---

## 3. Interface Utilisateur & Écrans (Frontend)

### 3.1. Cadastre des Composants Blazor WebAssembly (`Autoprint.Web`)

| Fichier Composant | Rôle & Type | Route URL | Responsabilité UI |
| :--- | :--- | :--- | :--- |
| [`App.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/App.razor) | Racine d'application | N/A | Routeur Blazor (`Router`), gestion des vues non autorisées (`NotAuthorized`) et non trouvées (`NotFound`). |
| [`_Imports.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/_Imports.razor) | Déclarations globales | N/A | Directives d'importation partagées (composants Radzen, DTOs, modèles, autorisations). |
| [`Layout/MainLayout.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Layout/MainLayout.razor) | Layout Principal | N/A | Squelette applicatif avec barre latérale, en-tête utilisateur, notification d'expiration mot de passe et corps principal. |
| [`Layout/NavMenu.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Layout/NavMenu.razor) | Barre de navigation | N/A | Menu latéral responsive filtré dynamiquement selon les permissions RBAC de l'utilisateur connecté. |
| [`Layout/EmptyLayout.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Layout/EmptyLayout.razor) | Layout Minimal | N/A | Conteneur sans barre latérale pour la page de connexion et le mur de sécurité. |
| [`Pages/Login.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Login.razor) | Page d'authentification | `/login` | Formulaire de connexion utilisateur (local ou Active Directory) avec mémorisation de session. |
| [`Pages/Home.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Home.razor) | Redirection | `/` | Redirection automatique vers `/dashboard` ou `/login`. |
| [`Pages/Dashboard.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Dashboard.razor) | Tableau de Bord | `/dashboard` | Vue synthétique : compteurs de parc, graphique doughnut Chart.js de répartition des modèles, statut du spouleur et alertes matérielles. |
| [`Pages/Imprimantes.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Imprimantes.razor) | Gestion Imprimantes | `/imprimantes` | Grille Radzen complète des imprimantes, filtres, indicateurs d'état spouleur et santé SNMP, boutons d'action. |
| [`Pages/ImprimanteModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/ImprimanteModal.razor) | Modale Imprimante | Modale | Création/édition d'imprimante, affectation lieu/modèle, switch mode filiale direct, calcul nommage auto. |
| [`Pages/PrinterDiagnosticModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/PrinterDiagnosticModal.razor) | Modale Diagnostic SNMP | Modale | Affichage temps réel de l'état Ping, des jauges de niveaux de toners, bacs papier et journal d'alertes SNMP. |
| [`Pages/SyncPreviewModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/SyncPreviewModal.razor) | Modale Synchronisation | Modale | Liste des opérations Spouleur Windows en attente (Création/Mise à jour/Suppression) avec déclenchement de la synchro. |
| [`Pages/ScanPrintersModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/ScanPrintersModal.razor) | Modale Scan & Import | Modale | Découverte distante WMI d'un serveur tiers ou balayage CIDR (port 9100), sélection et importation en base. |
| [`Pages/Lieux.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Lieux.razor) | Référentiel des Lieux | `/lieux` | Liste des sites géographiques, statut d'activation, et liste des sous-réseaux CIDR rattachés. |
| [`Pages/LieuModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/LieuModal.razor) | Modale Emplacement | Modale | Édition d'un lieu et gestion dynamique de sa collection de sous-réseaux IPv4 (`EmplacementNetwork`). |
| [`Pages/Marques.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Marques.razor) | Référentiel Marques | `/marques` | Liste des constructeurs matériels avec décompte d'imprimantes. |
| [`Pages/MarqueModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/MarqueModal.razor) | Modale Marque | Modale | Ajout ou renommage d'une marque. |
| [`Pages/Modeles.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Modeles.razor) | Référentiel Modèles | `/modeles` | Catalogue des modèles d'imprimantes avec liaison pilote et profil SNMP associé. |
| [`Pages/ModeleModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/ModeleModal.razor) | Modale Modèle | Modale | Association marque, nom, pilote d'impression cible et profil de supervision SNMP. |
| [`Pages/Pilotes.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Pilotes.razor) | Référentiel Pilotes | `/pilotes` | Liste des pilotes Windows catalogués (V3 Legacy vs V4), indicateur de présence réelle sur le serveur hôte. |
| [`Pages/ScanDriversModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/ScanDriversModal.razor) | Modale Scan Pilotes | Modale | Balayage WMI des pilotes installés sur le serveur Windows hôte et réconciliation avec le référentiel. |
| [`Pages/SnmpProfiles.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/SnmpProfiles.razor) | Profils SNMP | `/snmp-profiles` | Liste des profils de télémétrie SNMP, boutons d'export/import JSON et création. |
| [`Pages/SnmpProfileModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/SnmpProfileModal.razor) | Modale Profil SNMP | Modale | Assistant d'auto-apprentissage (scan MIB live sur imprimante test), gestion de la liste des capteurs (`Items`). |
| [`Pages/SnmpProfileItemModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/SnmpProfileItemModal.razor) | Modale Capteur SNMP | Modale | Configuration unitaire d'un OID : catégorie (Toner, Tray, etc.), formule de conversion, OID de jauge max, couleur hexadécimale. |
| [`Pages/Reports.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Reports.razor) | Rapports Planifiés | `/reports` | Liste des plannings de rapports d'activité, statut actif, prochaine échéance et déclenchement d'envoi immédiat. |
| [`Pages/ReportScheduleModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/ReportScheduleModal.razor) | Modale Planification | Modale | Configuration de la périodicité, heure/minute, seuil d'alerte prédictive de toner, filtres et destinataires. |
| [`Pages/Parametres.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Parametres.razor) | Configuration Serveur | `/parametres` | Configuration globale : SMTP, gabarit de nommage, Active Directory, durée de rétention, supervision, et jetons API. |
| [`Pages/TokenCreateModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/TokenCreateModal.razor) | Modale Jeton API | Modale | Définition de la description et de la date d'expiration pour un nouveau jeton d'intégration. |
| [`Pages/TokenShowModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/TokenShowModal.razor) | Modale Affichage Jeton | Modale | Présentation unique et sécurisée du jeton brut généré (`ap_tok_...`) avec bouton de copie dans le presse-papier. |
| [`Pages/Audit.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Audit.razor) | Journal d'Audit | `/audit` | Grille paginée des événements d'audit, recherche textuelle, badge de niveau (INFO/WARNING), consultation des détails. |
| [`Pages/AuditDetailModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/AuditDetailModal.razor) | Modale Détail Audit | Modale | Visualiseur de comparaison JSON avant/après (`OldValues` vs `NewValues`) pour traçabilité différentielle. |
| [`Pages/Users.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Users.razor) | Gestion Utilisateurs | `/users` | Liste des comptes locaux et de domaine, rôles affectés, statut actif/inactif, réinitialisation de mot de passe. |
| [`Pages/UserModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/UserModal.razor) | Modale Utilisateur | Modale | Création/édition d'un utilisateur et attribution de ses rôles RBAC. |
| [`Pages/PasswordModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/PasswordModal.razor) | Modale Mot de Passe | Modale | Boîte de dialogue de saisie d'un nouveau mot de passe administrateur. |
| [`Pages/ResetPassword.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/ResetPassword.razor) | Réinitialisation MDP | `/reset-password` | Interface autonome de réinitialisation de mot de passe par jeton. |
| [`Pages/Roles.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Roles.razor) | Rôles & Permissions | `/roles` | Matrice des rôles applicatifs et gestion des mappages de groupes Active Directory (`AdRoleMappings`). |
| [`Pages/RolePermissionModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/RolePermissionModal.razor) | Modale Permissions | Modale | Attribution granulaire des 29 permissions unitaires par cases à cocher pour un rôle sélectionné. |
| [`Pages/AdMappingModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/AdMappingModal.razor) | Modale Mappage AD | Modale | Recherche dynamique LDAP de groupes ou d'utilisateurs AD et association au rôle applicatif cible. |
| [`Pages/Profile.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Profile.razor) | Profil Utilisateur | `/profile` | Consultation des informations de l'utilisateur connecté et changement de son mot de passe. |
| [`Pages/SecurityWall.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/SecurityWall.razor) | Mur de Sécurité | Modale Bloquante | Boîte de dialogue infranchissable forçant le renouvellement immédiat d'un mot de passe expiré ou réinitialisé. |

---

### 3.2. Scripts & Feuilles de Style (`Autoprint.Web/wwwroot`)

| Fichier | Type | Rôle Exact dans l'Application |
| :--- | :--- | :--- |
| [`wwwroot/index.html`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/wwwroot/index.html) | HTML5 Hôte | Squelette HTML principal chargeant Bootstrap 5.3, Bootstrap Icons, le thème Radzen `material-base.css`, Chart.js, et injectant les scripts d'interopérabilité JS (`setupChart` pour le graphique en anneau et `downloadFileFromStream` pour le téléchargement direct des rapports PDF/CSV/JSON). |
| [`wwwroot/css/app.css`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/wwwroot/css/app.css) | Feuille de style CSS | Règles de style globales de l'application : mise en page de la barre latérale dégradée (`.sidebar`), gestion responsive desktop/mobile (`@media`), indicateurs de progression de chargement WebAssembly (`.loading-progress`), et bandeau d'erreur Blazor (`#blazor-error-ui`). |
| [`wwwroot/images/Autoprint.png`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/wwwroot/images/Autoprint.png) | Image PNG | Logo et favicon officiels de l'application Web. |
| [`wwwroot/icon-192.png`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/wwwroot/icon-192.png) | Image PNG | Icône pour la prise en charge PWA / raccourcis navigateurs. |

---

### 3.3. Vues & Composants WPF du Client Desktop (`Autoprint.Client`)

| Fichier Vue | Fichier Code-Behind | ViewModel Associé | Responsabilité UI |
| :--- | :--- | :--- | :--- |
| [`MainWindow.xaml`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/MainWindow.xaml) | [`MainWindow.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/MainWindow.xaml.cs) | [`MainWindowViewModel`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/ViewModels/MainWindowViewModel.cs) | Fenêtre principale avec barre de navigation à onglets (Imprimantes, Gérer, Options), indicateur de site détecté et bandeau de statut hors-ligne. |
| [`Views/PrintersView.xaml`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Views/PrintersView.xaml) | [`Views/PrintersView.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Views/PrintersView.xaml.cs) | `MainWindowViewModel` | Grille des imprimantes disponibles pour le lieu actuel : statut d'installation locale, bouton d'installation/suppression, étoile de sélection d'imprimante favorite. |
| [`Views/ManagePrintersView.xaml`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Views/ManagePrintersView.xaml) | [`Views/ManagePrintersView.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Views/ManagePrintersView.xaml.cs) | [`ManagePrintersViewModel`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/ViewModels/ManagePrintersViewModel.cs) | Onglet de gestion des files d'impression Windows déjà connectées sur le poste avec commande de désinstallation directe. |
| [`Views/OptionsView.xaml`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Views/OptionsView.xaml) | [`Views/OptionsView.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/Views/OptionsView.xaml.cs) | [`OptionsViewModel`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/ViewModels/OptionsViewModel.cs) | Configuration des options client : bascule auto de l'imprimante par défaut, notifications systray, lancement au démarrage Windows, panneau d'administration verrouillé par mot de passe. |
| [`MissingPrinterDialog.xaml`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/MissingPrinterDialog.xaml) | [`MissingPrinterDialog.xaml.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Client/MissingPrinterDialog.xaml.cs) | N/A | Boîte de dialogue contextuelle avertissant l'utilisateur lorsque son imprimante favorite a été supprimée ou renommée sur le serveur. |

---

## 4. Modèles de Données & Configuration

### 4.1. Entités ORM & Tables de Base de Données

| Entité | Table BDD | Clé Primaire | Propriétés Clés | Rôle & Relations |
| :--- | :--- | :--- | :--- | :--- |
| [`Marque`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/RefData.cs) | `Marques` | `Id` (int) | `Nom` | Constructeurs d'imprimantes (ex: HP, Canon, Xerox). Relation 1-N vers `Modeles`. |
| [`Modele`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/RefData.cs) | `Modeles` | `Id` (int) | `Nom`, `Code`, `MarqueId`, `PiloteId`, `SnmpProfileId` | Modèle matériel. Clés étrangères vers `Marque`, `Pilote` (pilote Windows associé) et `SnmpProfile` (profil de télémétrie). |
| [`Emplacement`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/RefData.cs) | `Emplacements` | `Id` (int) | `Nom`, `Code`, `Status` (`Active`/`Detected`) | Site physique / Établissement. Relation 1-N vers `EmplacementNetworks` et 1-N vers `Imprimantes`. |
| [`EmplacementNetwork`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/RefData.cs) | `EmplacementNetworks` | `Id` (int) | `EmplacementId`, `CidrIpv4`, `Description`, `IsPrimary` | Plages d'adresses IPv4 CIDR rattachées à un site pour la géolocalisation réseau du client. |
| [`Pilote`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/Pilote.cs) | `Pilotes` | `Id` (int) | `Nom`, `Version`, `EstInstalle` | Pilotes d'impression Windows déclarés dans le catalogue (V3 Legacy ou V4). |
| [`Imprimante`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/Imprimante.cs) | `Imprimantes` | `Id` (int) | `NomAffiche`, `Code`, `AdresseIp`, `EstPartagee`, `NomPartage`, `IsDirectPrintingEnabled`, `EmplacementId`, `ModeleId`, `Status`, `SnmpPort`, `SnmpCommunity`, `SnmpVersion`, `MonitoringStatus`, `SerialNumber`, `LastSeen`, `IsArchived` | File d'impression centrale. Clés étrangères vers `Emplacement` et `Modele`. Porte les états Spouleur (`Status`) et télémétrie (`MonitoringStatus`). |
| [`SnmpProfile`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/SnmpProfile.cs) | `SnmpProfiles` | `Id` (int) | `Name`, `IsColor`, `OidTonerBlack`, `OidTonerCyan`, `OidTonerMagenta`, `OidTonerYellow`, `OidPageCounter` | Profil de supervision regroupant les OIDs standard et la collection dynamique d'items. Relation 1-N vers `SnmpProfileItems` (Cascade Delete). |
| [`SnmpProfileItem`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/SnmpProfileItem.cs) | `SnmpProfileItems` | `Id` (int) | `SnmpProfileId`, `Category`, `Name`, `Oid`, `OidMaxCapacity`, `ValueType`, `ColorHex`, `SortOrder` | Définition granulaire d'un capteur SNMP (cartouche, bac, compteur, afficheur, maintenance). |
| [`TonerHistory`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/TonerHistory.cs) | `TonerHistories` | `Id` (int) | `ImprimanteId`, `ComponentColor`, `LevelPercent`, `RecordedAt` | Historique quotidien des niveaux de consommables servant de base à la régression linéaire prédictive. |
| [`ReportSchedule`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/ReportSchedule.cs) | `ReportSchedules` | `Id` (int) | `ReportName`, `SelectedMetricsJson`, `ScopeFilterJson`, `Frequency`, `EmailRecipients`, `Format`, `LastRunAt`, `NextRunAt`, `IsActive`, `PredictionThresholdDays`, `RunHour`, `RunMinute`, `RunDayOfWeek`, `RunDayOfMonth` | Planification des rapports automatisés par e-mail (PDF/CSV). |
| [`IntegrationToken`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/IntegrationToken.cs) | `IntegrationTokens` | `Id` (int) | `TokenHash`, `Description`, `CreatedAt`, `ExpiresAt` | Empreintes cryptographiques SHA-256 des jetons d'API pour les systèmes de monitoring externes. |
| [`ServerSetting`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/ServerSetting.cs) | `ServerSettings` | `Key` (string PK) | `Value`, `Description`, `Type` (`STRING`, `INT`, `BOOL`, `PASSWORD`) | Table clé-valeur de la configuration serveur globale. |
| [`AuditLog`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/AuditLog.cs) | `AuditLogs` | `Id` (int) | `DateAction`, `Utilisateur`, `Action`, `ResourceName`, `OldValues`, `NewValues`, `Details`, `Niveau` | Journal d'audit différentiel complet de toutes les actions d'administration. |
| [`DiscoveryProfile`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/DiscoveryProfile.cs) | `DiscoveryProfiles` | `Id` (int) | `Name`, `TargetRanges`, `ExcludedRanges`, `ProbeTargets`, `SkipKnownSubnets`, `ScheduleHour`, `ScheduleDays`, `IsEnabled`, `LastRunDate`, `LastRunResult`, `SendEmailReport`, `EmailRecipients` | Configuration et historique des scans de découverte automatique de sous-réseaux. |
| [`SystemError`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/SystemError.cs) | `SystemErrors` | `Id` (int) | `Source`, `Message`, `StackTrace`, `DateOccured` | Journalisation des exceptions et erreurs système internes. |
| [`User`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Models/Security/SecurityEntities.cs) | `Users` | `Id` (int) | `Username`, `DisplayName`, `Email`, `PasswordHash`, `IsAdUser`, `IsActive`, `LastLogin`, `ForceChangePassword`, `LastPasswordChangeDate` | Comptes utilisateurs locaux et profils synchronisés Active Directory. |
| [`Role`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Models/Security/SecurityEntities.cs) | `Roles` | `Id` (int) | `Name`, `Description` | Rôles de sécurité RBAC. |
| [`Permission`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Models/Security/SecurityEntities.cs) | `Permissions` | `Id` (int) | `Code`, `Description` | Catalogue des 29 permissions unitaires système. |
| [`UserRole`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Models/Security/SecurityEntities.cs) | `UserRoles` | `UserId`, `RoleId` (PK composite) | - | Table de jointure plusieurs-à-plusieurs entre `Users` et `Roles`. |
| [`RolePermission`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Models/Security/SecurityEntities.cs) | `RolePermissions` | `RoleId`, `PermissionId` (PK composite) | - | Table de jointure plusieurs-à-plusieurs entre `Roles` et `Permissions`. |
| [`AdRoleMapping`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Models/Security/SecurityEntities.cs) | `AdRoleMappings` | `Id` (int) | `AdIdentifier`, `MappingType` (`Group`/`User`), `RoleId` | Mappage automatique des groupes ou utilisateurs Active Directory vers les rôles RBAC. |

---

### 4.2. DTOs & Contrats par Domaine (`Autoprint.Shared/DTOs`)

- **Authentification & Utilisateurs (`UserDtos.cs`) :**
  - `LoginRequest` (Username, Password, RememberMe)
  - `LoginResponse` (Token, Username, DisplayName, Permissions, PasswordExpired)
  - `UserDto`, `CreateUserDto`, `UpdateUserDto`, `ChangePasswordDto`, `ResetPasswordDto`
- **Rôles & Permissions (`RoleDtos.cs`, `AdMappingDtos.cs`) :**
  - `RoleDto`, `CreateRoleDto`, `PermissionDto`, `UpdateRolePermissionsDto`
  - `AdSearchResultDto`, `AdRoleMappingDto`, `CreateAdMappingDto`
- **Synchronisation Spouleur & Audit Matériel (`SyncPreviewDto.cs`, `BatchResult.cs`) :**
  - `SyncPreviewDto` (Id, NomImprimante, Status, DateModification, ModifiePar, Action, Details)
  - `ServerAuditSnapshot`, `WinPrinterInfo`
  - `BatchResult` (Success, Messages)
- **Télémétrie SNMP & Diagnostics (`PrinterDiagnosticResult.cs`) :**
  - `PrinterDiagnosticResult` (Status, PingSuccess, PingRoundtripTimeMs, HasSnmpProfile, SerialNumber, UptimeSeconds, PageCounter, Toners, Trays, Alerts)
  - `TonerLevelResult`, `PaperTrayResult`
  - `SnmpScanRequestDto`, `DiscoveredOidDto`
  - `SnmpTestProfileRequestDto`, `SnmpTestItemDto`, `SnmpTestProfileResultDto`
  - `ReportScopeFilter`, `ScopeFilterRule`
- **Importation & Découverte Réseau (`ImportDtos.cs`, `DiscoveryProfile.cs`) :**
  - `DiscoveredPrinterDto`, `DiscoveredDriverDto`, `RemoteScanRequestDto`, `ImportPrinterSelectionDto`
  - `PrinterScanResult` (IpAddress, Hostname, SnmpModel, IsRegistered)
- **Journalisation & Supervision (`AuditDtos.cs`, `DashboardDtos.cs`, `SystemAlertsDto.cs`) :**
  - `AuditLogDto`, `PaginatedList<T>`
  - `DashboardStatsDto`, `ChartDataDto`
  - `SystemAlertsDto` (DriverAlert, ModelAlert)
- **Sauvegarde Totale (`BackupDto.cs`) :**
  - `BackupRootDto` agrégeant 15 sous-DTOs pour l'export/import unifié en JSON de la base entière.
- **Communication Inter-Processus (`IpcRequest.cs` dans `Autoprint.Shared/IPC`) :**
  - `IpcRequest` (Action, DriverModelName, UncPath, ConfigServerUrl, ConfigApiKey)
  - `IpcResponse` (Success, Message)

---

### 4.3. Clés de Configuration & Variables d'Environnement

#### A. Fichier `appsettings.json` (Serveur)
```json
{
  "Database": {
    "Provider": "SqlServer | Sqlite",
    "ConnectionStrings": {
      "SqlServer": "Server=...;Database=AutoprintDb;...",
      "Sqlite": "Data Source=Autoprint.db"
    }
  },
  "AllowedHosts": "*",
  "ClientUrl": "http://localhost:8080",
  "Jwt": {
    "Key": "<Cle-Cryptographique-512-Bits>",
    "Issuer": "AutoprintServer",
    "Audience": "AutoprintClient",
    "ExpireMinutes": 120
  }
}
```

#### B. Table Base de Données `ServerSettings`
- **SMTP :** `SmtpHost`, `SmtpPort`, `SmtpEnableSsl`, `SmtpIgnoreCertError`, `SmtpAuthRequired`, `SmtpUser`, `SmtpPass`, `SmtpFromAddress`, `SmtpDisplayName`.
- **Nommage Dynamique :** `NamingTemplate` (défaut : `IMP_{LIEU}_{MODELE}`), `NamingEnabled`, `NamingSameShare`.
- **Active Directory :** `AdDomain`, `AdBaseDn`, `AdLdapFilter`, `AdUseServiceAccount`, `AdServiceUser`, `AdServicePassword`, `AdAdminEmails`.
- **Sécurité :** `PasswordExpirationDays` (défaut : 90 jours), `AgentApiKey` (GUID secret pour les agents clients), `LogRetentionDays` (défaut : 180 jours).
- **Supervision & Télémétrie :**
  - `Monitoring_Enabled` (booléen)
  - `Monitoring_IntervalMinutes` (défaut : 60 min)
  - `Monitoring_StartHour` (défaut : 8)
  - `Monitoring_EndHour` (défaut : 18)
  - `Monitoring_ScanOnWeekends` (booléen)
  - `Monitoring_IncludeOffline`, `Monitoring_IncludeWarning`, `Monitoring_IncludeCritical`, `Monitoring_IncludeArchived` (filtres export API).
- **Impression :** `Printer_DefaultDirectModeEnabled` (activation par défaut du Branch Office Direct Printing).

#### C. Paramètres Machine du Client Poste
- **Registre `HKLM\SOFTWARE\Autoprint` :** `PRINTSERVER` (adresse ou nom d'hôte du serveur), `APIKEY` (clé d'agent machine).
- **Fichier `user-settings.json` :** `EnableNotifications`, `AutoSwitchDefaultPrinter`, `PreferredPrinters` (dictionnaire `{ "CodeLieu": "NomImprimante" }`), `RunAtStartup`, `HasCreatedDesktopShortcut`.

---

## 5. Recettes d'Intervention Rapide (Cheat Sheet)

### Recette 1 : Ajouter un nouveau paramètre de configuration de bout en bout

Pour introduire un nouveau paramètre global (ex: `Snmp_DefaultTimeoutMs`) :

1. **Persistance & Initialisation :**
   - Ouvrir [`Autoprint.Server/Data/ApplicationDbContext.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/ApplicationDbContext.cs).
   - Dans `OnModelCreating`, ajouter l'entrée dans le seed `modelBuilder.Entity<ServerSetting>().HasData(...)`.
   - Dans [`Autoprint.Server/Data/DbInitializer.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/DbInitializer.cs), ajouter la condition de vérification et d'insertion `if (!context.ServerSettings.Any(s => s.Key == "..."))`.
2. **DTO de mise à jour API :**
   - Ouvrir [`Autoprint.Server/Controllers/SettingsController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/SettingsController.cs).
   - Ajouter la propriété dans la classe `SettingsUpdateDto`.
   - Dans la méthode `SaveSettings`, ajouter l'appel `Check("Snmp_DefaultTimeoutMs", ...)` et l'appel `await UpdateSetting("Snmp_DefaultTimeoutMs", ...)`.
3. **Modèle de Sauvegarde (Backup) :**
   - Vérifier [`Autoprint.Server/Controllers/BackupController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/BackupController.cs) pour s'assurer que `ServerSettings` continue d'exporter/restaurer la nouvelle clé.
4. **Interface Utilisateur Web :**
   - Ouvrir [`Autoprint.Web/Pages/Parametres.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Parametres.razor).
   - Ajouter le champ de saisie Radzen (`RadzenNumeric` ou `RadzenTextBox`) relié à la propriété dans l'onglet correspondant.
   - Initialiser la valeur dans `OnInitializedAsync` lors du chargement des paramètres et l'inclure dans l'objet transmis lors de la sauvegarde.
5. **Consommation Métier :**
   - Dans le service concerné (ex: [`SnmpService.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/SnmpService.cs)), injecter `ApplicationDbContext` ou lire la valeur dans `ServerSettings`.

---

### Recette 2 : Ajouter ou modifier une fonctionnalité métier sur les imprimantes

Pour ajouter une nouvelle propriété métier sur les imprimantes (ex: `CostCenter` / Centre de coût) :

1. **Modèle Partagé :**
   - Modifier [`Autoprint.Shared/Imprimante.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/Imprimante.cs) : ajouter la propriété avec ses annotations (ex: `[MaxLength(50)] public string? CostCenter { get; set; }`).
2. **Migration Base de Données :**
   - Générer la migration EF Core : `dotnet ef migrations add AddCostCenterToImprimante --project Autoprint.Server`.
   - Dans [`Autoprint.Server/Data/DbInitializer.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Data/DbInitializer.cs), ajouter le bloc SQL brut pour la compatibilité SQLite :
     ```csharp
     try { context.Database.ExecuteSqlRaw("ALTER TABLE Imprimantes ADD COLUMN CostCenter TEXT NULL;"); } catch { }
     ```
3. **Contrôleur API & Audit :**
   - Dans [`Autoprint.Server/Controllers/ImprimantesController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/ImprimantesController.cs), vérifier les méthodes `PostImprimante` et `PutImprimante` pour mapper ou autoriser le champ.
   - [`AuditService.LogUpdateAsync`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/AuditService.cs) prendra automatiquement en compte la nouvelle propriété lors de la sérialisation différentielle JSON.
4. **Interface d'Édition Web :**
   - Ouvrir [`Autoprint.Web/Pages/ImprimanteModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/ImprimanteModal.razor) et ajouter le composant de formulaire `RadzenFormField` avec `RadzenTextBox`.
   - Si la colonne doit être visible dans la liste globale, ajouter une `RadzenDataGridColumn` dans [`Autoprint.Web/Pages/Imprimantes.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Imprimantes.razor).
5. **Propagation Client & Sauvegarde :**
   - Si le client WPF a besoin de l'information : le modèle `Autoprint.Shared` étant partagé, `client_cache.db` intégrera la colonne lors de la prochaine régénération du cache (`DataService.SaveDataInternalAsync`).
   - Mettre à jour `BackupImprimanteDto` dans [`Autoprint.Shared/DTOs/BackupDto.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Shared/DTOs/BackupDto.cs) et dans [`BackupController.cs`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Controllers/BackupController.cs).

---

### Recette 3 : Modifier l'interface utilisateur, un onglet ou une modale (Blazor WebAssembly)

Pour modifier un écran d'administration (ex: modifier la modale de diagnostic SNMP) :

1. **Repérage du Composant :**
   - Identifier le fichier dans `Autoprint.Web/Pages/` (ex: [`Pages/PrinterDiagnosticModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/PrinterDiagnosticModal.razor)).
2. **Gestion des Données & Injection :**
   - Vérifier les services injectés en en-tête (`@inject HttpClient Http`, `@inject DialogService DialogService`, `@inject NotificationService NotificationService`).
3. **Structure HTML & Composants Radzen :**
   - Mettre à jour la structure visuelle en utilisant les composants standards (`RadzenCard`, `RadzenProgressBar`, `RadzenBadge`, `RadzenButton`).
   - Ne pas utiliser de classes CSS arbitraires : s'appuyer sur le design system Material de Radzen et Bootstrap 5.
4. **Gestion des Flux Asynchrones & Événements :**
   - Implémenter ou ajuster les méthodes C# dans le bloc `@code { ... }`.
   - Encapsuler les appels HTTP dans des blocs `try/catch` avec affichage de notifications via `NotificationService.Notify()`.
5. **Validation des Droits (RBAC) :**
   - Si un bouton ou un panneau d'action est restreint, l'encapsuler dans un composant `<AuthorizeView Policy="VOTRE_PERMISSION">`.

---

### Recette 4 : Adapter la journalisation, l'audit ou la gestion des erreurs

Pour consigner un nouvel événement d'administration dans le journal d'audit :

1. **Injection du Service :**
   - Dans le contrôleur cible, injecter [`AuditService`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/AuditService.cs) via le constructeur.
2. **Choix de la Méthode d'Audit :**
   - **Pour une modification d'entité avec différentiel automatique :**
     ```csharp
     await _auditService.LogUpdateAsync<Imprimante>(id, updatedPrinter, "PRINTER_UPDATE", User.Identity?.Name, "INFO");
     ```
   - **Pour une action simple ou une opération système :**
     ```csharp
     _auditService.LogAction("ACTION_CODE", "Description claire de l'événement", User.Identity?.Name, level: "INFO", resourceName: "NomRessource");
     ```
   - **Pour une opération sur mesure avec objets personnalisés :**
     ```csharp
     _auditService.LogCustomAudit("CUSTOM_ACTION", "Détail", User.Identity?.Name, "Ressource", objetAvant, objetApres, "WARNING");
     ```
3. **Persistance :**
   - `AuditService` ajoute l'entrée au `ChangeTracker` d'EF Core. S'assurer que `await _context.SaveChangesAsync()` est bien appelé pour valider l'insertion en base.
4. **Consultation & Rétention :**
   - L'événement apparaît immédiatement dans [`Pages/Audit.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/Audit.razor). Le modal [`Pages/AuditDetailModal.razor`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Web/Pages/AuditDetailModal.razor) affichera automatiquement le comparateur JSON si `OldValues` et `NewValues` sont renseignés.
   - La purge automatique sera gérée sans intervention par [`LogCleanupWorker`](file:///C:/Git/Developpement/Autoprint/Autoprint/Autoprint.Server/Services/LogCleanupWorker.cs) selon la valeur `LogRetentionDays`.
