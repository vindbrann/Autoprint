# 📌 Backlog — Anomalies & Améliorations à étudier

Ce fichier recense ce qui a été repéré **en dehors du plan de sécurité** (`_AUDIT_SECURITE_2026-11.md`) : anomalies, dette technique, améliorations possibles. Rien ici n'est planifié ; chaque point est à étudier puis à décider.

**Légende :** 🐞 Anomalie (ne marche pas comme prévu) · 🔧 Dette technique · 💡 Amélioration · 🔐 Sécurité hors périmètre des phases

---

## Base de données & migrations

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B1 | ✅ | **Corrigé (phase A, 04/11/2026).** Schéma SQLite créé à partir du modèle (les migrations SQL Server ne sont plus tentées sur SQLite) ; les bases SQLite cassées par l'ancien comportement sont réparées au démarrage. — *Constat initial :* **Installation neuve en SQLite impossible.** Les migrations EF sont générées pour SQL Server (`nvarchar`, `datetime2`). Sur une base SQLite vide, `Migrate()` échoue après avoir créé `__EFMigrationsHistory` et `__EFMigrationsLock` ; le repli `EnsureCreated()` ne fait alors rien, car la base « existe ». Le serveur démarre sans aucune table (`no such table: ServerSettings`). | `Program.cs` (2 blocs `Migrate()`/`EnsureCreated()`) | 04/11/2026 |
| B2 | 🔧 | **Instantané EF (`ApplicationDbContextModelSnapshot.cs`) obsolète.** Les migrations de juillet–août ont été écrites à la main sans mise à jour de l'instantané. `dotnet ef migrations add` regénère donc d'anciennes opérations (colonnes déjà existantes), et `dotnet ef migrations remove` a vidé l'instantané (restauré via git). Toute évolution de schéma passe aujourd'hui par `DbInitializer`. | `Autoprint.Server/Migrations` | 04/11/2026 |
| B3 | 🔧 | **Erreurs de migration masquées.** `try { Migrate(); } catch { EnsureCreated(); }` avale l'exception : une migration SQL Server en échec passe inaperçue. Idem pour les ~45 `catch { }` vides côté serveur (dont tous les scripts de `DbInitializer`). | `Program.cs`, `DbInitializer.cs`, services | 04/11/2026 |
| B4 | 🔧 | Outil `dotnet-ef` en 10.0.0 alors que le runtime EF est en 10.0.14 (avertissement à chaque commande). | `Autoprint.Server/.config/dotnet-tools.json` | 04/11/2026 |

## Fonctionnalités incomplètes

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B5 | ✅ | **Corrigé (phase A, 04/11/2026).** Lien de réinitialisation par e-mail (comptes locaux avec adresse e-mail) : jeton aléatoire à usage unique valable 30 min, seule son empreinte est stockée, lien construit avec le nom public (jamais l'en-tête Host), réponse identique que le compte existe ou non, page `/reset-password`, sessions coupées après changement. — *Constat initial :* **« Mot de passe oublié » ne fait rien.** `POST /api/auth/forgot-password` renvoie toujours `200 OK` sans action, et aucun endpoint serveur ne correspond à la page `ResetPassword.razor` (le DTO `ResetPasswordDto` attend un jeton reçu par e-mail). | `AuthController`, `Autoprint.Web/Pages/ResetPassword.razor` | 04/11/2026 |
| B6 | ✅ | **Corrigé (phase A, 04/11/2026).** Case « Changement du mot de passe obligatoire à la première connexion » (cochée par défaut) à la création. Au passage : modifier un utilisateur effaçait toujours cette obligation (corrigé). — *Constat initial :* Un utilisateur local créé par un admin doit changer son mot de passe dès sa première connexion (`LastPasswordChangeDate` vide ⇒ expiré). Comportement probablement voulu, mais implicite : le rendre explicite (case « changer au premier login » cochée par défaut). | `UsersController.CreateUser`, `AuthService.LoginAsync` | 04/11/2026 |
| B7 | ✅ | **Corrigé (phase A, 04/11/2026).** Lieu / modèle choisis vérifiés (à défaut : premier existant + statut « à corriger »), refus clair s'il n'existe aucun lieu ou modèle, imprimantes ignorées comptées dans le message. — *Constat initial :* L'import d'imprimantes utilise par défaut le modèle n° 1 et le lieu n° 1 si rien n'est choisi (statut « à corriger »). À vérifier que ces identifiants existent toujours. | `ImportController.ImportPrinters` | 04/11/2026 |

## Qualité du code

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B8 | ✅ | **Corrigé (phase A, 04/11/2026).** Textes corrigés. — *Constat initial :* Textes mal encodés (« incohÃ©rent », « ModÃ¨le ») renvoyés à la console. | `ImprimantesController.PutImprimante` | 04/11/2026 |
| B9 | 🔧 | ~149 avertissements de compilation sur la solution (dont l'usage d'API PDFsharp 6 marquées obsolètes : conversions implicites `XUnit` ↔ `double` dans `ReportGeneratorService`). | Solution | 04/11/2026 |
| B10 | 🔧 | Politique CORS figée sur `https://localhost:7159` / `http://localhost:5139` : inutile en production (la console est servie par le même site). À supprimer ou rendre configurable. | `Program.cs` | 04/11/2026 |
| B11 | 🔧 | L'installeur écrit `ClientUrl = http://localhost:<port>` dans `appsettings.json` : le serveur ne l'utilise pas, seul l'installeur la relit pour retrouver le port lors d'une mise à jour. À renommer / clarifier. | `Autoprint.Installer.Server.UI/MainWindow.xaml.cs` | 04/11/2026 |
| B12 | 💡 | Mises à jour majeures disponibles, sans faille connue : Radzen.Blazor 8 → 12, Swashbuckle 6 → 10, Microsoft.OpenApi 2 → 3, WiX 6 → 7, Microsoft.Data.SqlClient 6 → 7, MaterialDesign 5.3 → 5.4. | `*.csproj`, `*.wixproj` | 04/11/2026 |

## Sécurité hors périmètre des phases

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B13 | ✅ | **Corrigé (phase A, 04/11/2026).** Annuaire injoignable pendant la connexion d'un utilisateur AD : connexion refusée (« Annuaire Active Directory injoignable… », code 503). Compte hors de la base de recherche : rôles de groupe AD retirés, rôles manuels conservés. — *Constat initial :* **Recherche des groupes AD en échec ⇒ rôles conservés.** Si l'annuaire ne répond pas pendant la connexion d'un utilisateur AD, ses rôles ne sont ni recalculés ni retirés (comportement « ouvert »). Choix à faire : refuser la connexion, ou accepter en l'état. | `AuthService.ApplyAdGroupMappings` | 04/11/2026 |
| B14 | 🔐 | **Limite de tentatives en mémoire.** Le compteur par compte est remis à zéro à chaque recyclage du pool IIS, et n'est pas partagé entre plusieurs serveurs. Suffisant pour un serveur unique. | `LoginThrottleService` | 04/11/2026 |
| B15 | ✅ | **Corrigé (phase A, 04/11/2026).** Paramètre `ReverseProxy:KnownProxies` (liste d'IP) : X-Forwarded-For n'est pris en compte que pour ces proxys ; sans paramètre, il est ignoré. — *Constat initial :* **Limite par IP derrière un reverse proxy.** Si un proxy / répartiteur est placé devant IIS, toutes les connexions semblent venir de son IP : il faudra configurer `ForwardedHeaders` pour retrouver l'IP réelle. | `Program.cs` (`AddRateLimiter`) | 04/11/2026 |

## Repéré pendant la phase 2

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B16 | ✅ | **Corrigé (phase A, 04/11/2026).** Bootstrap 5.3.0, Bootstrap Icons 1.11.0 et Chart.js 4.4.1 embarqués dans `wwwroot/lib` (fichiers identiques aux versions CDN, empreintes vérifiées, licences MIT jointes) ; jsDelivr retiré de la CSP. — *Constat initial :* **Console dépendante d'Internet (CDN).** Bootstrap, Bootstrap Icons et Chart.js sont chargés depuis `cdn.jsdelivr.net` (versions désormais figées + contrôle d'intégrité SRI). Sur un réseau sans accès Internet, la console perd sa mise en forme et ses graphiques. Proposition : embarquer ces fichiers dans `wwwroot/lib` et retirer jsDelivr de la CSP. | `Autoprint.Web/wwwroot/index.html`, `Program.cs` (CSP) | 04/11/2026 |
| B17 | ✅ | **Corrigé (phase A, 04/11/2026).** Mise à jour : si `AllowedHosts` vaut `*`, l'installeur affiche l'écran « Configuration Web » (port verrouillé) pour saisir le nom public. — *Constat initial :* **Mise à jour d'un serveur existant : `AllowedHosts` reste à `*`.** La fusion de configuration de l'installeur conserve l'ancienne valeur, et l'étape « nom public » n'est pas proposée en mise à jour. À renseigner à la main dans `appsettings.json` (ex. `autoprint.maboite.fr;localhost`), ou à proposer dans l'installeur en mode mise à jour. | `Autoprint.Installer.Server.UI` | 04/11/2026 |
| B18 | ✅ | **Corrigé (phase C, 04/11/2026).** Paramètres › Sécurité › « Générer une nouvelle clé » : l'ancienne clé reste acceptée 7, 30 ou 90 jours (ou est révoquée immédiatement), le temps de redéployer la nouvelle sur les postes ; bandeau rappelant la date de fin ; action tracée (`AGENT_KEY_ROTATE`) ; l'ancienne clé est chiffrée et masquée comme les autres secrets. — *Constat initial :* **Aucun moyen de changer la clé d'API agent.** L'ancien interrupteur « Modification autorisée » de la console ne faisait rien (la clé n'était pas enregistrée par le serveur) ; il a été retiré. Une rotation de clé devrait prévoir la mise à jour des postes (nouveau MSI / GPO). | `SettingsController`, `Parametres.razor` | 04/11/2026 |
| B19 | ✅ | **Corrigé (phase C, 04/11/2026).** Jeton de 30 min (installations neuves) renouvelé silencieusement par la console 5 min avant expiration ; droits relus en base à chaque renouvellement ; durée maximale de session de 10 h (reconnexion obligatoire ensuite). — *Constat initial :* Durée de session fixe de 120 min sans renouvellement automatique : un admin est déconnecté en pleine saisie au bout de 2 h. Un jeton court + renouvellement silencieux améliorerait confort et sécurité. | `AuthService`, `appsettings.json` (`Jwt:ExpireMinutes`) | 04/11/2026 |
| B20 | ✅ | **Corrigé (phase A, 04/11/2026).** `<RadzenTheme Theme="material" />` : la feuille de thème est demandée au bon emplacement. — *Constat initial :* `<RadzenTheme />` est déclaré sans thème : le navigateur demande `css/-base.css` (404) à chaque page. Sans effet visible (le thème est chargé par `index.html`), mais à corriger. | `MainLayout.razor`, `EmptyLayout.razor` | 04/11/2026 |
| B21 | ✅ | **Corrigé (phase C, 04/11/2026).** Bandeau dans la fenêtre d'édition d'un rôle : l'enregistrement ferme les sessions des membres (y compris la sienne). — *Constat initial :* Modifier un rôle coupe les sessions de tous ses membres, y compris celle de l'admin qui le modifie s'il en fait partie (il doit se reconnecter). Comportement voulu pour la sécurité ; à signaler dans l'interface. | `RolesController` | 04/11/2026 |

## Repéré pendant la phase 3

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B22 | ✅ | **Corrigé le 04/11/2026 — Schéma SQL Server incomplet sur installation neuve.** 8 colonnes manquaient (comparaison du modèle EF avec une base neuve) : `ReportSchedules.IsActive`, `PredictionThresholdDays`, `RunHour`, `RunMinute`, `RunDayOfWeek`, `RunDayOfMonth` (4 migrations du 16/07/2026 sans attributs `[DbContext]` / `[Migration]`) et `SnmpProfileItems.ModifiePar`, `EstSupprime`. Ajoutées par des scripts idempotents dans `DbInitializer` (mêmes valeurs par défaut que les migrations), sur les installations neuves **et** existantes. Les 4 migrations orphelines restent en place (inertes) ; leur nettoyage relève de B2. | `DbInitializer.cs` | 04/11/2026 |
| B23 | 🔐 | **Le dossier des clés de chiffrement doit être sauvegardé.** Les secrets en base sont chiffrés avec les clés de `C:\ProgramData\Autoprint\Keys`. Si ce dossier est perdu (réinstallation de Windows, restauration de la seule base), les mots de passe AD / SMTP et la clé agent sont illisibles : il faut les ressaisir (et redéployer la clé agent sur les postes). À intégrer à la documentation d'exploitation et de sauvegarde du serveur. | `Program.cs` (`AddDataProtection`) | 04/11/2026 |
| B24 | ✅ | **Corrigé (phase C, 04/11/2026).** Message de restauration d'une sauvegarde sans secrets : conséquences sur un nouveau serveur (ressaisie des mots de passe, postes refusés avec l'ancienne clé agent). — *Constat initial :* Après une restauration sur un **autre** serveur sans mot de passe de protection, la clé agent du nouveau serveur est conservée : les postes configurés avec l'ancienne clé seront refusés. Prévoir un message explicite dans l'écran de restauration. | `BackupController.Restore` | 04/11/2026 |

## Repéré pendant #8 (sortie de LocalSystem)

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B25 | ✅ | **Corrigé (phase B, 04/11/2026).** Nouvelles installations SQLite : base dans `C:\ProgramData\Autoprint\Data` (dossier réservé à SYSTEM, Administrateurs et au site). Mise à jour : la base (et ses fichiers -wal / -shm) est déplacée automatiquement, site arrêté pendant l'opération, sans jamais écraser une base existante. Le site n'a plus aucun droit d'écriture sur le dossier du programme. — *Constat initial :* **Base SQLite dans le dossier du programme.** Avec le site sans privilège, SQLite doit pouvoir écrire ses fichiers temporaires (`-wal`, `-shm`) à côté de la base : l'installeur donne donc au pool le droit « Modification » sur `C:\Program Files\Autoprint Server` (installations SQLite uniquement). Proposition : déplacer la base dans `C:\ProgramData\Autoprint\Data` (avec migration du fichier existant). | Installeur (`HardenSensitiveFiles`), `Program.cs` | 04/11/2026 |
| B26 | ✅ | **Corrigé (phase B, 04/11/2026).** Tableau de bord › État du service : statut du service `AutoprintSpooler` (en mode « Service »), avec info-bulle si arrêté. — *Constat initial :* Le tableau de bord pourrait afficher l'état du service `AutoprintSpooler` (comme il le fait pour le spouleur Windows), pour repérer tout de suite un service arrêté. | `DashboardController` | 04/11/2026 |
| B27 | ✅ | **Corrigé (phase B, 04/11/2026).** 60 s maximum par opération dans le service : au-delà, le site reçoit « Le spouleur Windows n'a pas répondu… » et les opérations suivantes ne sont plus bloquées. — *Constat initial :* Une opération du spouleur bloquée (WMI qui ne répond pas) bloque les suivantes dans le service, qui les exécute une par une. Ajouter un délai maximal par opération. | `Autoprint.SpoolerService` | 04/11/2026 |
---

## 🗓️ Plan de traitement (décidé le 04/11/2026)

Toutes les modifications sont faites avant la recette complète. Ordre : du moins risqué au plus lourd.

| Phase | Branche | Points |
|---|---|---|
| **A — Correctifs** | `security/phase-1` (1re version), reportée sur `feature/serveur-sans-localsystem` | B8, B20, B7, B1, B17, B16, B15, B13, B6, B5 |
| **B — SNMPv3 et service** | `feature/serveur-sans-localsystem` (version suivante) | SNMPv3 (profils d'identifiants par modèle, exception par imprimante), B25, B26, B27 |
| **C — Améliorations** | version suivante | B19, B18, B21, B24 |
| **D — Dette technique** | version suivante | B2, B3, B4, B9, B10, B11, puis B12 en dernier |

## Repéré pendant la phase A

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B28 | 🔧 | Compilation de la solution complète parfois en échec (« Autoprint.Shared.dll introuvable ») : le MSI client lance un `dotnet publish` Release en parallèle de la compilation. Relancer la compilation suffit. Rendre l'étape de publication dépendante de la compilation de `Autoprint.Shared`. | `Autoprint.Client.Setup.wixproj` | 04/11/2026 |
| B29 | 🔧 | `index.html` charge la feuille du thème Radzen en double (balise `<link id="theme">` + composant `<RadzenTheme>`). Sans effet visible. | `Autoprint.Web/wwwroot/index.html` | 04/11/2026 |

## Repéré pendant la phase B

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B30 | 🐞 | ✅ **Corrigé en phase B.** La modification d'une imprimante n'enregistrait pas la communauté, la version ni le port SNMP saisis dans la console (champs absents de la mise à jour). | `ImprimantesController.PutImprimante` | 04/11/2026 |
| B31 | 🐞 | ✅ **Corrigé en phase B.** La sauvegarde ne contenait ni les paramètres SNMP des imprimantes ni le mode d'impression directe : perdus à la restauration. Ajoutés (communauté traitée comme un secret). Toujours absents : code, commentaire, partage activé ou non, statut de supervision. | `BackupController` | 04/11/2026 |
| B32 | 🔐 | Communautés SNMP « ancien mode » (champ de l'imprimante, sans profil) toujours en clair en base et visibles dans la liste des imprimantes par tout utilisateur connecté. Recommandation : passer les imprimantes sur des profils d'identifiants (chiffrés, jamais réaffichés), puis supprimer ces champs. | `Imprimante.SnmpCommunity`, `ImprimantesController` | 04/11/2026 |
| B33 | 💡 | Le diagnostic n'interroge pas l'imprimante en SNMP quand son modèle n'a pas de profil d'OID (« Aucun profil SNMP configuré ») : on ne peut alors pas vérifier les identifiants depuis la fiche. Utiliser le test de l'éditeur de profils d'OID en attendant. | `SnmpService.GetPrinterDiagnosticAsync` | 04/11/2026 |

## Repéré pendant la phase C

| # | Type | Constat | Où | Repéré le |
|---|---|---|---|---|
| B34 | 💡 | Les serveurs **existants** gardent `Jwt:ExpireMinutes = 120` (valeur conservée par la mise à jour). Le renouvellement automatique fonctionne aussi, mais un jeton volé reste utilisable 2 h. Passer la valeur à 30 dans `appsettings.json`, ou la faire réduire par l'installeur en mise à jour. | `appsettings.json`, installeur | 04/11/2026 |
| B35 | 💡 | Un onglet de console laissé ouvert renouvelle sa session sans activité de l'utilisateur, jusqu'à la limite de 10 h. Une déconnexion après inactivité (ex. 30 min) pourrait être ajoutée. | `CustomAuthStateProvider` | 04/11/2026 |
