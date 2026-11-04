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
| B11 | 🔧 | L'installeur écrit `ClientUrl = http://localhost:<port>` dans `appsettings.json`, valeur que le serveur n'utilise pas. | `Autoprint.Installer.Server.UI/MainWindow.xaml.cs` | 04/11/2026 |
| B12 | 💡 | Mises à jour majeures disponibles, sans faille connue : Radzen.Blazor 8 → 12, Swashbuckle 6 → 10, Microsoft.OpenApi 2 → 3, WiX 6 → 7, Microsoft.Data.SqlClient 6 → 7, MaterialDesign 5.3 → 5.4. | `*.csproj`, `*.wixproj` | 04/11/2026 |

## Sécurité hors périmètre des phases

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B13 | 🔐 | **Recherche des groupes AD en échec ⇒ rôles conservés.** Si l'annuaire ne répond pas pendant la connexion d'un utilisateur AD, ses rôles ne sont ni recalculés ni retirés (comportement « ouvert »). Choix à faire : refuser la connexion, ou accepter en l'état. | `AuthService.ApplyAdGroupMappings` | 04/11/2026 |
| B14 | 🔐 | **Limite de tentatives en mémoire.** Le compteur par compte est remis à zéro à chaque recyclage du pool IIS, et n'est pas partagé entre plusieurs serveurs. Suffisant pour un serveur unique. | `LoginThrottleService` | 04/11/2026 |
| B15 | 🔐 | **Limite par IP derrière un reverse proxy.** Si un proxy / répartiteur est placé devant IIS, toutes les connexions semblent venir de son IP : il faudra configurer `ForwardedHeaders` pour retrouver l'IP réelle. | `Program.cs` (`AddRateLimiter`) | 04/11/2026 |
