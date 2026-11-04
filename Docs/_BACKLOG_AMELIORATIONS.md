# 📌 Backlog — Anomalies & Améliorations à étudier

Ce fichier recense ce qui a été repéré **en dehors du plan de sécurité** (`_AUDIT_SECURITE_2026-11.md`) : anomalies, dette technique, améliorations possibles. Rien ici n'est planifié ; chaque point est à étudier puis à décider.

**Légende :** 🐞 Anomalie (ne marche pas comme prévu) · 🔧 Dette technique · 💡 Amélioration · 🔐 Sécurité hors périmètre des phases

---

## Base de données & migrations

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B1 | 🐞 | **Installation neuve en SQLite impossible.** Les migrations EF sont générées pour SQL Server (`nvarchar`, `datetime2`). Sur une base SQLite vide, `Migrate()` échoue après avoir créé `__EFMigrationsHistory` et `__EFMigrationsLock` ; le repli `EnsureCreated()` ne fait alors rien, car la base « existe ». Le serveur démarre sans aucune table (`no such table: ServerSettings`). | `Program.cs` (2 blocs `Migrate()`/`EnsureCreated()`) | 04/11/2026 |
| B2 | 🔧 | **Instantané EF (`ApplicationDbContextModelSnapshot.cs`) obsolète.** Les migrations de juillet–août ont été écrites à la main sans mise à jour de l'instantané. `dotnet ef migrations add` regénère donc d'anciennes opérations (colonnes déjà existantes), et `dotnet ef migrations remove` a vidé l'instantané (restauré via git). Toute évolution de schéma passe aujourd'hui par `DbInitializer`. | `Autoprint.Server/Migrations` | 04/11/2026 |
| B3 | 🔧 | **Erreurs de migration masquées.** `try { Migrate(); } catch { EnsureCreated(); }` avale l'exception : une migration SQL Server en échec passe inaperçue. Idem pour les ~45 `catch { }` vides côté serveur (dont tous les scripts de `DbInitializer`). | `Program.cs`, `DbInitializer.cs`, services | 04/11/2026 |
| B4 | 🔧 | Outil `dotnet-ef` en 10.0.0 alors que le runtime EF est en 10.0.14 (avertissement à chaque commande). | `Autoprint.Server/.config/dotnet-tools.json` | 04/11/2026 |

## Fonctionnalités incomplètes

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B5 | 🐞 | **« Mot de passe oublié » ne fait rien.** `POST /api/auth/forgot-password` renvoie toujours `200 OK` sans action, et aucun endpoint serveur ne correspond à la page `ResetPassword.razor` (le DTO `ResetPasswordDto` attend un jeton reçu par e-mail). | `AuthController`, `Autoprint.Web/Pages/ResetPassword.razor` | 04/11/2026 |
| B6 | 💡 | Un utilisateur local créé par un admin doit changer son mot de passe dès sa première connexion (`LastPasswordChangeDate` vide ⇒ expiré). Comportement probablement voulu, mais implicite : le rendre explicite (case « changer au premier login » cochée par défaut). | `UsersController.CreateUser`, `AuthService.LoginAsync` | 04/11/2026 |
| B7 | 🐞 | L'import d'imprimantes utilise par défaut le modèle n° 1 et le lieu n° 1 si rien n'est choisi (statut « à corriger »). À vérifier que ces identifiants existent toujours. | `ImportController.ImportPrinters` | 04/11/2026 |

## Qualité du code

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B8 | 🐞 | Textes mal encodés (« incohÃ©rent », « ModÃ¨le ») renvoyés à la console. | `ImprimantesController.PutImprimante` | 04/11/2026 |
| B9 | 🔧 | ~149 avertissements de compilation sur la solution (dont l'usage d'API PDFsharp 6 marquées obsolètes : conversions implicites `XUnit` ↔ `double` dans `ReportGeneratorService`). | Solution | 04/11/2026 |
| B10 | 🔧 | Politique CORS figée sur `https://localhost:7159` / `http://localhost:5139` : inutile en production (la console est servie par le même site). À supprimer ou rendre configurable. | `Program.cs` | 04/11/2026 |
| B11 | 🔧 | L'installeur écrit `ClientUrl = http://localhost:<port>` dans `appsettings.json` : le serveur ne l'utilise pas, seul l'installeur la relit pour retrouver le port lors d'une mise à jour. À renommer / clarifier. | `Autoprint.Installer.Server.UI/MainWindow.xaml.cs` | 04/11/2026 |
| B12 | 💡 | Mises à jour majeures disponibles, sans faille connue : Radzen.Blazor 8 → 12, Swashbuckle 6 → 10, Microsoft.OpenApi 2 → 3, WiX 6 → 7, Microsoft.Data.SqlClient 6 → 7, MaterialDesign 5.3 → 5.4. | `*.csproj`, `*.wixproj` | 04/11/2026 |

## Sécurité hors périmètre des phases

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B13 | 🔐 | **Recherche des groupes AD en échec ⇒ rôles conservés.** Si l'annuaire ne répond pas pendant la connexion d'un utilisateur AD, ses rôles ne sont ni recalculés ni retirés (comportement « ouvert »). Choix à faire : refuser la connexion, ou accepter en l'état. | `AuthService.ApplyAdGroupMappings` | 04/11/2026 |
| B14 | 🔐 | **Limite de tentatives en mémoire.** Le compteur par compte est remis à zéro à chaque recyclage du pool IIS, et n'est pas partagé entre plusieurs serveurs. Suffisant pour un serveur unique. | `LoginThrottleService` | 04/11/2026 |
| B15 | 🔐 | **Limite par IP derrière un reverse proxy.** Si un proxy / répartiteur est placé devant IIS, toutes les connexions semblent venir de son IP : il faudra configurer `ForwardedHeaders` pour retrouver l'IP réelle. | `Program.cs` (`AddRateLimiter`) | 04/11/2026 |

## Repéré pendant la phase 2

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B16 | 🔐 | **Console dépendante d'Internet (CDN).** Bootstrap, Bootstrap Icons et Chart.js sont chargés depuis `cdn.jsdelivr.net` (versions désormais figées + contrôle d'intégrité SRI). Sur un réseau sans accès Internet, la console perd sa mise en forme et ses graphiques. Proposition : embarquer ces fichiers dans `wwwroot/lib` et retirer jsDelivr de la CSP. | `Autoprint.Web/wwwroot/index.html`, `Program.cs` (CSP) | 04/11/2026 |
| B17 | 🔐 | **Mise à jour d'un serveur existant : `AllowedHosts` reste à `*`.** La fusion de configuration de l'installeur conserve l'ancienne valeur, et l'étape « nom public » n'est pas proposée en mise à jour. À renseigner à la main dans `appsettings.json` (ex. `autoprint.maboite.fr;localhost`), ou à proposer dans l'installeur en mode mise à jour. | `Autoprint.Installer.Server.UI` | 04/11/2026 |
| B18 | 💡 | **Aucun moyen de changer la clé d'API agent.** L'ancien interrupteur « Modification autorisée » de la console ne faisait rien (la clé n'était pas enregistrée par le serveur) ; il a été retiré. Une rotation de clé devrait prévoir la mise à jour des postes (nouveau MSI / GPO). | `SettingsController`, `Parametres.razor` | 04/11/2026 |
| B19 | 💡 | Durée de session fixe de 120 min sans renouvellement automatique : un admin est déconnecté en pleine saisie au bout de 2 h. Un jeton court + renouvellement silencieux améliorerait confort et sécurité. | `AuthService`, `appsettings.json` (`Jwt:ExpireMinutes`) | 04/11/2026 |
| B20 | 🐞 | `<RadzenTheme />` est déclaré sans thème : le navigateur demande `css/-base.css` (404) à chaque page. Sans effet visible (le thème est chargé par `index.html`), mais à corriger. | `MainLayout.razor`, `EmptyLayout.razor` | 04/11/2026 |
| B21 | 💡 | Modifier un rôle coupe les sessions de tous ses membres, y compris celle de l'admin qui le modifie s'il en fait partie (il doit se reconnecter). Comportement voulu pour la sécurité ; à signaler dans l'interface. | `RolesController` | 04/11/2026 |

## Repéré pendant la phase 3

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B22 | 🐞 | **⚠️ Prioritaire — Schéma SQL Server incomplet sur installation neuve.** Les 4 migrations du 16/07/2026 (`AddIsActiveToReportSchedule`, `AddMonitoringExportSettings`, `AddPredictionThresholdToReportSchedule`, `AddFineSchedulingToReportSchedule`) n'ont pas les attributs `[DbContext]` / `[Migration]` : EF ne les voit pas et ne les applique jamais. `DbInitializer` ne rattrape ces colonnes que pour SQLite. De plus, `SnmpProfileItems` est créée sans `EstSupprime` / `ModifiePar`. Conséquence sur SQL Server neuf : **rapports planifiés, sauvegarde (export / restauration) et profils SNMP en erreur** (`Invalid column name`). À vérifier aussi sur les serveurs existants. Correction proposée : scripts idempotents `IF NOT EXISTS` dans `DbInitializer` (pas d'ajout d'attributs, qui pourrait faire échouer `Migrate()` sur les bases où `DbInitializer` a déjà inséré les paramètres « Monitoring_* »). | `Autoprint.Server/Migrations`, `DbInitializer.cs` | 04/11/2026 |
| B23 | 🔐 | **Le dossier des clés de chiffrement doit être sauvegardé.** Les secrets en base sont chiffrés avec les clés de `C:\ProgramData\Autoprint\Keys`. Si ce dossier est perdu (réinstallation de Windows, restauration de la seule base), les mots de passe AD / SMTP et la clé agent sont illisibles : il faut les ressaisir (et redéployer la clé agent sur les postes). À intégrer à la documentation d'exploitation et de sauvegarde du serveur. | `Program.cs` (`AddDataProtection`) | 04/11/2026 |
| B24 | 💡 | Après une restauration sur un **autre** serveur sans mot de passe de protection, la clé agent du nouveau serveur est conservée : les postes configurés avec l'ancienne clé seront refusés. Prévoir un message explicite dans l'écran de restauration. | `BackupController.Restore` | 04/11/2026 |

## Repéré pendant #8 (sortie de LocalSystem)

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B25 | 🔧 | **Base SQLite dans le dossier du programme.** Avec le site sans privilège, SQLite doit pouvoir écrire ses fichiers temporaires (`-wal`, `-shm`) à côté de la base : l'installeur donne donc au pool le droit « Modification » sur `C:\Program Files\Autoprint Server` (installations SQLite uniquement). Proposition : déplacer la base dans `C:\ProgramData\Autoprint\Data` (avec migration du fichier existant). | Installeur (`HardenSensitiveFiles`), `Program.cs` | 04/11/2026 |
| B26 | 💡 | Le tableau de bord pourrait afficher l'état du service `AutoprintSpooler` (comme il le fait pour le spouleur Windows), pour repérer tout de suite un service arrêté. | `DashboardController` | 04/11/2026 |
| B27 | 💡 | Une opération du spouleur bloquée (WMI qui ne répond pas) bloque les suivantes dans le service, qui les exécute une par une. Ajouter un délai maximal par opération. | `Autoprint.SpoolerService` | 04/11/2026 |
