# PROJECT_CONTEXT.md

## 1. Vue d'ensemble & Problème Métier

### Résumé exécutif
Autoprint est une solution logicielle d'infrastructure conçue pour centraliser, automatiser, superviser et simplifier l'administration ainsi que l'utilisation du parc d'imprimantes en entreprise sous environnement Microsoft Windows.

L'application répond à plusieurs problématiques opérationnelles majeures :
- **Gestion et automatisation du Spouleur d'impression Windows (Print Spooler) :** Création unifiée des ports d'impression TCP/IP standards (`IP_<adresse_ip>`, port 9100 RAW/JetDirect), déploiement automatisé des files d'impression partagées, configuration du mode *Branch Office Direct Printing* (impression directe filiale évitant l'engorgement du réseau central), standardisation des noms d'imprimantes via des gabarits dynamiques, et réconciliation de l'état réel de Windows avec le référentiel métier.
- **Attribution transparente des imprimantes aux postes de travail :** Un agent client léger résident en barre des tâches analyse en permanence la connectivité réseau du poste client, associe l'adresse IP active à un sous-réseau géographique (CIDR), identifie le site physique de l'utilisateur, et applique automatiquement l'imprimante par défaut sélectionnée pour ce lieu. En cas de coupure avec l'infrastructure centrale, un cache local SQLite permet la continuité de service.
- **Supervision SNMP proactive et télémétrie des consommables :** Interrogation périodique en arrière-plan des périphériques réseau via le protocole SNMP (v1/v2c) selon la norme RFC 3805 (Printer-MIB), suivi continu des niveaux de toners/tambours, des états physiques (bacs, bourrages, portes ouvertes), analyse prédictive de date d'épuisement par régression linéaire, et diffusion automatique de rapports PDF/CSV planifiés par e-mail.
- **Gouvernance et intégration d'entreprise :** Annuaire Active Directory (authentification sécurisée LDAP, synchronisation des profils et mapping des groupes vers les rôles applicatifs RBAC), sécurité granulaire par permissions révocables, jetons d'intégration sécurisés pour exports externes, et traçabilité exhaustive de toutes les opérations via un journal d'audit différentiel.

### Ce que le projet NE fait PAS
Pour prévenir toute mauvaise supposition architecturale ou fonctionnelle :
- **Pas d'interception de flux documentaire ni de comptabilisation des impressions (pas de serveur mandataire/proxy d'impression) :** Autoprint ne se positionne pas entre le document de l'utilisateur et l'imprimante. Il n'intercepte pas les flux EMF, PCL ou PostScript, ne retient pas les documents pour libération par badge RFID (Follow-Me / Pull Printing à la machine), et ne calcule pas de facturation au clic ou quota de pages par utilisateur.
- **Pas de compatibilité serveur/client hors écosystème Windows :** Le backend et le service s'appuient directement sur l'API Win32 (`winspool.drv`), WMI (`root\cimv2`), et le Spouleur d'impression Windows (`spoolsv.exe`). Autoprint ne gère pas de files d'impression sous Linux/Unix (CUPS) ni sous macOS.
- **Pas de courtier de messages externe (Message Broker) :** L'application n'utilise pas RabbitMQ, Apache Kafka ou Redis. Les communications temps réel reposent directement sur un hub ASP.NET Core SignalR hébergé in-process, et les communications inter-processus locales (IPC) exploitent les tubes nommés Windows (*Named Pipes*).
- **Pas d'architecture multi-tenant isolée :** L'instance d'Autoprint opère pour un domaine d'infrastructure unique ; les données et la configuration ne sont pas segmentées en compartiments locataires étanches.

---

## 2. Stack Technique & Dépendances

### Langages, Frameworks & Versions Cibles
La version globale de l'ensemble de la solution est **26.8.24.3** (définie de manière centralisée dans [`Directory.Build.props`](file:///C:/Git/Developpement/Autoprint/Autoprint/Directory.Build.props)).

| Composant | Projet | Framework Cible | Type d'Application |
| :--- | :--- | :--- | :--- |
| **Backend API & Hôte Web** | `Autoprint.Server` | `.NET 10.0` (`net10.0`) | ASP.NET Core Web API / Minimal API / SignalR / Razor Pages |
| **Frontend Web d'Administration** | `Autoprint.Web` | `.NET 10.0` (`net10.0`) | Blazor WebAssembly (Standalone WASM hébergé) |
| **Client Bureau Utilisateur** | `Autoprint.Client` | `.NET 10.0 Windows` (`net10.0-windows`) | Application Windows WPF (Desktop Systray) |
| **Service d'Arrière-Plan Windows** | `Autoprint.Service` | `.NET 10.0 Windows` (`net10.0-windows`) | Windows Service Worker (`AutoprintService`) |
| **Modèles Partagés & Contrats** | `Autoprint.Shared` | `.NET 10.0` (`net10.0`) | Bibliothèque de classes C# partagée (DTOs, Entités, IPC) |
| **Installateur Serveur UI** | `Autoprint.Installer.Server.UI` | `.NET 10.0 Windows` (`net10.0-windows`) | Assistant d'installation / Assistant de migration WPF |
| **Packages Déploiement** | `*.Setup` | WiX Toolset v4/v5 | Projets d'installation MSI (`Package.wxs`) |

### Persistance & Accès aux Données
- **ORM :** Entity Framework Core 10.0.0 (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Tools`).
- **Modes de persistance supportés côté Serveur :**
  - Microsoft SQL Server (`SqlServer`) avec fractionnement des requêtes (`QuerySplittingBehavior.SplitQuery`).
  - SQLite local (`Sqlite`), base de données embarquée `Autoprint.db`.
- **Mode de persistance côté Client Poste :** SQLite (`client_cache.db` dans `%LOCALAPPDATA%\Autoprint\`) géré via `ClientDbContext`.

### Bibliothèques Tierces Clés & Rôle Exact
- **Réseau, SNMP & Impression :**
  - `Lextm.SharpSnmpLib` (v12.5.7) : Envoi de requêtes UDP SNMP v1/v2c (paquets `Messenger.Get`, parcours arborescent `Messenger.Walk`) sur le port 161 pour le diagnostic, la découverte automatique des tables MIB consommables/bacs, et la supervision.
  - `PdfSharpCore` (v1.3.67) : Moteur de rendu graphique PDF vectoriel côté serveur pour l'exportation et l'envoi automatisé des rapports d'audit et de santé des consommables.
- **Interface Utilisateur & Web :**
  - `Radzen.Blazor` (v8.3.8) : Bibliothèque de composants graphiques UI pour le tableau de bord Blazor WebAssembly (grilles de données `RadzenDataGrid`, boîtes de dialogue, notifications).
  - `Blazored.LocalStorage` (v4.5.0) : Stockage côté client WebAssembly du jeton de session JWT et des préférences de navigation.
  - `MaterialDesignThemes` & `MaterialDesignColors` (v5.3.0) : Thème graphique Google Material Design pour le client de bureau WPF.
  - `Hardcodet.NotifyIcon.Wpf` (v2.0.1) : Gestion de l'icône résidente dans la zone de notification Windows (systray), des bulles d'information contextuelles (*BalloonTips*) et du menu contextuel du client.
- **Système Windows, Administration & Déploiement :**
  - `Microsoft.Web.Administration` (v11.1.0) : Pilotage programmatique d'Internet Information Services (IIS) pour la création, la configuration et le recyclage du pool applicatif et du site web serveur.
  - `System.Management` (v10.0.0) : Requêtes WMI CIM (`Win32_Printer`, `Win32_TCPIPPrinterPort`, `Win32_PrinterDriver`, `Win32_OperatingSystem`, `Win32_ServerFeature`, `Win32_OptionalFeature`).
  - `System.DirectoryServices.AccountManagement` & `System.DirectoryServices` (v10.0.0) : Interrogation sécurisée de l'annuaire Active Directory (LDAP chiffré, validation des identifiants avec `ContextOptions.Negotiate | ContextOptions.Sealing`, navigation dans l'arbre d'annuaire et résolution récursive des groupes imbriqués via le filtre de chaîne LDAP OID `1.2.840.113556.1.4.1941`).
  - `System.ServiceProcess.ServiceController` (v10.0.0) : Vérification de l'état d'exécution du Spouleur d'impression Windows (`Spooler`).
  - `Microsoft.AspNetCore.SignalR.Client` (v10.0.0) : Client SignalR intégré au poste de travail WPF pour l'écoute en direct des événements de synchronisation émis par le serveur.

### Dépendances vis-à-vis du Système Hôte
- **Système d'exploitation :**
  - Serveur : Windows Server (2016 / 2019 / 2022 / 2025) ou Windows 10/11 x64 (avec fonctionnalités serveur activées).
  - Client : Windows 10 / Windows 11 (architecture 64 bits).
- **Rôles et Composants Windows Requis :**
  - Serveur Web IIS (`Web-Server` ou `IIS-WebServerRole`) avec module ASP.NET Core Hosting Bundle 10.0.
  - Rôle Serveur d'impression (`Print-Server` ou fonctionnalité `Printing-Foundation-Features`).
  - Service Windows Spouleur d'impression actif (`spoolsv.exe`).
- **Droits et Privilèges d'Exécution :**
  - **Serveur :** Le pool d'application IIS d'Autoprint (`Autoprint`) est configuré pour tourner sous l'identité **`LocalSystem`** (`ProcessModelIdentityType.LocalSystem`) avec un `IdleTimeout` de zéro. Ce niveau de privilège est obligatoire pour autoriser le processus web à créer des ports TCP/IP réseau via WMI, modifier les files du Spouleur Windows via P/Invoke (`winspool.drv`), et configurer les paramètres de pilotes d'impression dans le Registre Windows.
  - **Service Windows :** `Autoprint.Service` s'exécute sous le compte `NT AUTHORITY\SYSTEM`.
  - **Client :** S'exécute sous la session utilisateur standard. Les opérations requérant des privilèges administrateur (installation de pilote machine) sont déléguées au service d'arrière-plan via le tube nommé sécurisé `\\.\pipe\AutoprintPipe`.
- **Binaires Système & Fichiers Externes :**
  - `winspool.drv` (bibliothèque native Windows Print Spooler).
  - `rundll32.exe printui.dll,PrintUIEntry` (commandes natives Windows d'installation/connexion/suppression de files et pilotes).
  - Partage réseau Windows `print$` (accès distant SMB requis pour l'interrogation et le téléchargement des architectures de pilotes).
- **Arborescence Locale sur le Disque Hôte :**
  - Serveur : `C:\Program Files\Autoprint Server\` (fichiers applicatifs, `appsettings.json`, base SQLite optionnelle `Autoprint.db`).
  - Client (Cache) : `%LOCALAPPDATA%\Autoprint\client_cache.db` et jeton d'installation `.install-token`.
  - Client (Paramètres) : `%USERPROFILE%\Documents\Autoprint\user-settings.json`.
  - Client (Clés Registre Système) : `HKLM\SOFTWARE\Autoprint` (valeurs `PRINTSERVER` et `APIKEY`).
  - Client (Démarrage) : `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`AutoprintClient`).

---

## 3. Fonctionnalités & Workflows Critiques

### Inventaire Complet des Fonctionnalités Réelles
1. **Référentiel Matériel d'Impression :**
   - Gestion des Marques (`Marque`) et des Modèles (`Modele`).
   - Catalogue des Pilotes d'impression (`Pilote`), distinction automatique des versions (Type 3 Legacy 32/64 bits vs Type 4 V4).
   - Fiche Imprimante complète (`Imprimante`) : Nom affiché, code interne, adresse IPv4, nom de partage SMB, statut de partage, flag d'impression directe en filiale (*Branch Office Direct Printing*), association lieu/modèle/pilote, numéro de série détecté, statut de synchronisation spouleur, statut de supervision matérielle, et drapeau d'archivage (`IsArchived`).
2. **Gestion Territoriale & Multi-Réseaux :**
   - Référentiel des Emplacements / Lieux physiques (`Emplacement`).
   - Multi-adressage réseau par lieu (`EmplacementNetwork`) au format CIDR IPv4 (ex: `192.168.10.0/24`), avec désignation d'un réseau principal (`IsPrimary`) et statut d'activation (`LieuStatus.Active` vs `LieuStatus.Detected`).
3. **Moteur de Synchronisation Spouleur (SyncSpoolerService) :**
   - Détection des écarts d'état entre la base de données et le Spouleur Windows (`PrinterStatus.PendingCreation`, `PendingUpdate`, `PendingDelete`, `SyncError`, `Synchronized`).
   - Prévisualisation avant exécution des actions prévues.
   - Application par lot : création de ports TCP/IP (`Win32_TCPIPPrinterPort`), création de file (`Win32_Printer`), renommage via méthode WMI (`RenamePrinter`), application des attributs Win32 bas niveau, et vérification de conformité du mode filiale.
4. **Découverte Réseau & Importation Automatique :**
   - **Découverte de serveurs distants :** Analyse WMI distante d'un serveur d'impression tiers avec extraction des imprimantes, pilotes et ports partagés.
   - **Scan de sous-réseaux pour import :** Balayage parallèle multi-threadé (`Parallel.ForEachAsync`, degré 50) d'une plage CIDR avec test du port 9100 RAW, lecture de la description MIB SNMP (`1.3.6.1.2.1.25.3.2.1.3.1` / `1.3.6.1.2.1.1.1.0`), et résolution DNS inverse.
   - **Profils de découverte planifiés (`DiscoveryProfile`) :** Tâche de fond périodique (`DiscoveryWorker`) exécutant des scans programmés sur des plages réseau cibles pour identifier automatiquement les nouveaux sous-réseaux actifs, les insérer en statut *Detected*, et expédier un compte-rendu HTML par e-mail.
5. **Moteur SNMP Dynamique & Auto-Apprentissage :**
   - Modèle de profils SNMP dynamiques (`SnmpProfile` et `SnmpProfileItem`).
   - Catégorisation des capteurs : `Toner`, `Tray` (Bacs papier), `PageCounter` (Compteur global), `Status` (État machine), `Console` (Afficheur LCD), `Maintenance` (Bac récupérateur, four, tambours), `SerialNumber`.
   - Types de conversion : `Percentage`, `RawWithMax` (calcul dynamique `Level / MaxCapacity * 100`, gestion du standard RFC 3805 où `-3 = OK`, `-2 = Inconnu`), `Counter`, `String`.
   - **Auto-Découverte intelligente d'OIDs :** Analyse d'une imprimante par balayage SNMP Walk sur les tables standard Printer-MIB pour découvrir, nommer et qualifier automatiquement tous les consommables et bacs existants sans saisie manuelle.
   - Exportation et importation des profils SNMP au format JSON.
6. **Supervision Continue & Télémétrie (PrinterMonitoringWorker) :**
   - Tâche de fond résidente s'exécutant selon une périodicité paramétrable (par défaut 60 min) et des plages horaires de travail définies (`Monitoring_StartHour` à `Monitoring_EndHour`, exclusion optionnelle des week-ends).
   - Test Ping préalable ultra-rapide (800 ms), suivi d'un regroupement de requêtes SNMP en un unique paquet UDP pour optimiser la charge réseau.
   - Calcul dynamique du statut de santé : `Ok`, `Warning` (toner ≤ 10%, état alerte), `Critical` (erreur mécanique, bourrage papier, porte ouverte), `Offline` (machine injoignable).
   - Historisation quotidienne des niveaux de toners (`TonerHistories`, 1 point de mesure par composant et par jour).
   - **Archivage automatique :** Bascule automatique en `IsArchived = true` des périphériques restés hors-ligne pendant plus de 30 jours consécutifs.
7. **Modélisation Prédictive de Fin de Vie Consommables (PredictiveService) :**
   - Algorithme de régression linéaire par la méthode des moindres carrés appliqué sur l'historique des cartouches.
   - Détection automatique du remplacement de cartouche (remontée brutale du niveau) pour recalculer la pente uniquement depuis la date du dernier consommable neuf.
   - Estimation mathématique du nombre exact de jours restant avant épuisement complet.
8. **Moteur de Rapports Planifiés (ReportSchedules) :**
   - Planification de rapports (Quotidien, Hebdomadaire, Mensuel) avec heure et minute d'exécution précises.
   - Moteur de filtrage avancé par règles JSON combinables (`LocationId`, `BrandId`, `ModelId`, `SnmpProfileId`, nom).
   - Génération multi-format : CSV structuré ou rapport imprimable PDF vectoriel complet avec graphiques de jauges et tableau d'alertes.
   - Expédition automatique par SMTP multi-destinataires avec pièces jointes.
9. **Gestion Centralisée de la Configuration & Sécurité :**
   - Sauvegarde complète de la configuration et de la base de données en JSON unifié (`BackupController`), avec restauration transactionnelle respectant les contraintes d'intégrité relationnelle et réinitialisation des identifiants SQL Server (`IDENTITY_INSERT`).
   - Journal d'audit complet (`AuditLog`) stockant le différentiel JSON (`OldValues` / `NewValues`), utilisateur, niveau de gravité et horodatage UTC, avec politique de purge automatique (`LogRetentionDays`).
   - Gestion des jetons d'intégration API (`IntegrationToken`) hachés en SHA-256 pour l'interfaçage avec des systèmes de monitoring tiers via l'endpoint `/api/v1/monitoring/export`.
   - Modèle de nommage dynamique (`NamingService`) permettant de recalculer les noms de files d'attente à partir de variables (`{LIEU}`, `{LIEU_CODE}`, `{MARQUE}`, `{MODELE}`, `{IP}`, `{IP_LAST}`, `{IMP_CODE}`).
10. **Application Client Poste de Travail (Autoprint.Client) :**
    - Application WPF résidente en zone de notification Windows (systray) mono-instance par utilisateur (`Local\AutoprintClient_Mutex_{UserName}`).
    - Surveillance des événements de changement d'interface réseau physique (`NetworkChange.NetworkAddressChanged`).
    - Résolution de l'adresse IP active et identification du lieu correspondant parmi la liste des sous-réseaux configurés.
    - Synchronisation automatique de l'imprimante par défaut Windows (`winspool.drv SetDefaultPrinter`) selon les préférences enregistrées par l'utilisateur pour le site détecté.
    - Connexion/déconnexion directe des imprimantes partagées via `rundll32.exe printui.dll,PrintUIEntry /in` et `/dn`.
    - Mode Hors Ligne transparent : mise en cache locale SQLite (`client_cache.db`), régénération automatique en cas de rupture de schéma, et réactivité temps réel via SignalR (`EventsHub`).

---

### Cycles de Vie des Données et Flux Critiques

#### 1. Workflow de Synchronisation d'une Imprimante vers le Spouleur Windows
```
[Utilisateur / Blazor Web]
         │  (Demande d'application des modifications)
         ▼
[POST /api/sync/apply] ──► [Vérification RBAC: PRINTER_SYNC]
         │
         ▼
[SyncSpoolerService.ApplyChangesAsync()]
         │
         ├─► [PendingDelete] :
         │      ├─► Win32/WMI SupprimerImprimante(NomAffiche)
         │      └─► EF Core : context.Imprimantes.Remove(imp)
         │
         ├─► [PendingCreation] :
         │      ├─► Poka-Yoke : Validation présence IP et Pilote lié au Modèle
         │      ├─► Spooler.CreerPortTcp(IP) ──► ManagementClass("Win32_TCPIPPrinterPort") (Port 9100)
         │      ├─► Spooler.CreerImprimante(NomAffiche, Pilote, IP) ──► ManagementClass("Win32_Printer")
         │      ├─► Spooler.ModifierImprimante(...) :
         │      │      ├─► P/Invoke OpenPrinter(PRINTER_ALL_ACCESS)
         │      │      ├─► GetPrinter / SetPrinter (Structure PRINTER_INFO_2)
         │      │      ├─► Configuration du partage SMB (PRINTER_ATTRIBUTE_SHARED)
         │      │      └─► Configuration Win32 Branch Office Direct Printing :
         │      │             SetPrinterDataEx("PrinterDriverData", "EnableBranchOfficePrinting", 1/0)
         │      │             Ajustement masques (PRINTER_ATTRIBUTE_DIRECT vs PRINTER_ATTRIBUTE_QUEUED)
         │      ├─► Spooler.VerifierModeDirect() : Relecture registre + attributs Win32
         │      │      ├─► Succès ──► Status = Synchronized
         │      │      └─► Échec ──► Status = SyncError (Alerte pilote V3/V4 non compatible)
         │      └─► Enregistrement EF Core SaveChangesAsync()
         │
         └─► [PendingUpdate / SyncError] :
                ├─► Spooler.RecupererNomImprimanteParIp(IP)
                ├─► Si nom différent : WMI RenamePrinter(Ancien, Nouveau)
                ├─► Réapplication des attributs (Port, Partage, Mode Direct)
                └─► Validation finale de cohérence
         │
         ▼
[SignalR HubContext.Clients.All.SendAsync("RefreshPrinters")] ──► Notification temps réel de tous les postes
```

#### 2. Workflow de Détection Réseau & Configuration Automatique du Poste Client
```
[Poste Utilisateur (Autoprint.Client)]
         │
         ├─► Événement déclencheur : Démarrage de session OU NetworkChange.NetworkAddressChanged
         ▼
[NetworkService.GetActiveLocalIpAddress(ServeurCible)]
         │  (Extraction de l'adresse IPv4 locale routable vers le serveur d'impression)
         ▼
[App.RafraichirDonneesAsync()] ──► [Verrouillage par SemaphoreSlim(1,1)]
         │
         ├─► Tentative d'appel API Serveur (GetLieuxAsync + GetImprimantesAsync)
         │      ├─► Succès : Mise à jour du cache SQLite local (client_cache.db)
         │      └─► Échec (Réseau coupé) : Lecture de secours dans le cache local SQLite
         │
         ├─► Évaluation des correspondances IP / CIDR :
         │      ├─► Parcours des Emplacements actifs
         │      ├─► Test IpHelper.IsIpInCidr(ipLocale, reseau.CidrIpv4)
         │      └─► Priorisation des réseaux marqués "IsPrimary = true"
         │
         ├─► Identification du Lieu Actuel :
         │      ├─► Si Correspondance :
         │      │      ├─► Lecture des préférences (user-settings.json) pour ce code de lieu
         │      │      ├─► Si Imprimante Favorite définie :
         │      │      │      ├─► Vérification présence file d'impression locale (LocalPrintServer)
         │      │      │      └─► P/Invoke winspool.drv SetDefaultPrinter(\\serveur\imprimante)
         │      │      └─► Affichage notification Systray (Nom du site + nombre d'imprimantes)
         │      └─► Si Aucune Correspondance :
         │             └─► Notification Systray "Lieu Inconnu" (Anti-Spam : déclenchée uniquement sur changement d'état)
         ▼
[Libération du verrou SemaphoreSlim]
```

#### 3. Workflow d'Auto-Découverte et d'Apprentissage d'un Profil SNMP
```
[Administrateur / Interface Blazor]
         │  (Saisie de l'adresse IP d'une imprimante physique et demande de scan)
         ▼
[POST /api/snmpprofiles/scan-printer] ──► [Vérification RBAC: SNMP_PROFILE_WRITE]
         │
         ▼
[SnmpService.ScanPrinterOidsAsync()]
         │
         ├─► 1. Probe rapide SNMP UDP (800 ms) sur sysUpTime / sysDescr / hrPrinterStatus
         │      └─► Poka-Yoke : Si la machine ne répond pas, levée immédiate d'exception claire
         │
         ├─► 2. Balayage Consommables (Printer-MIB RFC 3805) :
         │      ├─► Messenger.Walk sur 1.3.6.1.2.1.43.11.1.1.6 (Descriptions)
         │      ├─► Messenger.Walk sur 1.3.6.1.2.1.43.11.1.1.8 (Capacités maximales)
         │      ├─► Messenger.Walk sur 1.3.6.1.2.1.43.11.1.1.9 (Niveaux actuels)
         │      ├─► Décodage intelligent OctetString (UTF-8 / ISO-8859-1 avec coupure au null-terminator)
         │      └─► Qualification automatique : Toner (Noir, Cyan, Magenta, Jaune) vs Maintenance (Four, Tambour, Récupérateur)
         │
         ├─► 3. Balayage Bacs Papier (Printer-MIB) :
         │      ├─► Messenger.Walk sur 1.3.6.1.2.1.43.8.2.1.12 / .10 / .9
         │      └─► Association OID niveau + OID capacité maximale
         │
         ├─► 4. Extraction en paquet unique GET des OIDs scalaires :
         │      ├─► Total Pages (1.3.6.1.2.1.43.10.2.1.4.1.1)
         │      ├─► Numéro de Série (1.3.6.1.2.1.43.5.1.1.17.1)
         │      ├─► Statut physique machine (1.3.6.1.2.1.25.3.5.1.1.1)
         │      ├─► Afficheur Console LCD (1.3.6.1.2.1.43.16.5.1.2.1.1)
         │      └─► Uptime (1.3.6.1.2.1.1.3.0)
         │
         ▼
[Retour de la liste pré-remplie DiscoveredOidDto vers l'interface]
         │  (L'administrateur valide ou ajuste avant enregistrement en base)
```

#### 4. Workflow de Supervision Périodique & Génération Prédictive
```
[PrinterMonitoringWorker] (Tâche de fond déclenchée toutes les X minutes)
         │
         ├─► Vérification des garde-fous de planification :
         │      ├─► Monitoring_Enabled == true ?
         │      └─► Heure actuelle dans la plage [Monitoring_StartHour - Monitoring_EndHour] ?
         │
         ├─► 1. Exécution du balayage d'archivage automatique (1 fois par jour) :
         │      └─► Imprimantes non vues depuis > 30 jours ──► IsArchived = true
         │
         ├─► 2. Boucle de supervision sur toutes les imprimantes actives non archivées :
         │      ├─► SnmpService.GetPrinterDiagnosticAsync(IP, Port, Communauté, Version, Profil)
         │      ├─► Détermination du statut : Ok / Warning / Critical / Offline
         │      ├─► Mise à jour du Numéro de Série et de LastSeen
         │      └─► Si mesure toner valide : Insertion dans TonerHistories (max 1 point/jour/couleur)
         │
         ├─► 3. Sauvegarde collective en base EF Core SaveChangesAsync()
         │
         └─► 4. Traitement des Rapports Planifiés (ReportSchedules) échus :
                ├─► Filtrage des imprimantes selon ScopeFilterJson
                ├─► Pour chaque toner : PredictiveService.PredictDaysRemaining(historique)
                │      └─► Calcul régression linéaire sur la consommation
                ├─► Génération du document PDF (PdfSharpCore) ou CSV
                ├─► Expédition par e-mail SMTP avec pièce jointe (EmailService)
                └─► Recalcul de l'horodatage NextRunAt (Quotidien / Hebdomadaire / Mensuel)
```

---

### Gestion d'État et Persistance

| Support | Emplacement / Cible | Mode | Contenu & Rôle |
| :--- | :--- | :--- | :--- |
| **Base de Données Centrale** | SQL Server ou SQLite (`Autoprint.db`) | **Lecture / Écriture** | Référentiel complet : `Imprimantes`, `Emplacements`, `EmplacementNetworks`, `Marques`, `Modeles`, `Pilotes`, `SnmpProfiles`, `SnmpProfileItems`, `TonerHistories`, `ReportSchedules`, `IntegrationTokens`, `ServerSettings`, `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, `AdRoleMappings`, `DiscoveryProfiles`, `AuditLogs`, `SystemErrors`. |
| **Spouleur Windows Serveur** | Registre système & `spoolsv.exe` | **Lecture / Écriture** | Files d'attente d'impression Windows, ports TCP/IP (`Win32_TCPIPPrinterPort`), partages SMB, attributs de pilotes (`PrinterDriverData`). |
| **Fichier de Config Serveur** | `C:\Program Files\Autoprint Server\appsettings.json` | **Lecture (Écriture Installateur)** | Fournisseur de base, chaînes de connexion, clé secrète JWT maîtresse, configuration des journaux. |
| **Base Locale Client** | `%LOCALAPPDATA%\Autoprint\client_cache.db` | **Lecture / Écriture** | Cache local SQLite des lieux et des imprimantes pour fonctionnement autonome en cas de perte réseau. |
| **Fichier Préférences Client**| `%USERPROFILE%\Documents\Autoprint\user-settings.json`| **Lecture / Écriture** | Préférences utilisateur : imprimante favorite par code de site, activation des notifications, démarrage automatique. |
| **Registre Système Client** | `HKLM\SOFTWARE\Autoprint` | **Lecture seule (Écriture MSI/Service)** | Adresse du serveur d'impression (`PRINTSERVER`) et clé d'API agent (`APIKEY`). |
| **Registre Session Client** | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | **Lecture / Écriture** | Entrée de démarrage automatique de la session utilisateur pour `AutoprintClient`. |
| **Mémoire Vive (RAM)** | Processus Serveur (`w3wp.exe` / `Autoprint.Server.exe`) | **Volatile** | Instances des démons hébergés (`DiscoveryWorker`, `PrinterMonitoringWorker`, `LogCleanupWorker`), connexions actives SignalR (`EventsHub`). |
| **Mémoire Vive (RAM)** | Processus Client (`Autoprint.Client.exe`) | **Volatile** | Verrou de synchronisation (`SemaphoreSlim`), état de connectivité, signature du dernier lieu détecté pour filtrage anti-rebond. |

---

## 4. Modèle de Sécurité, Permissions & Concurrence

### Droits Système & Élévation de Privilèges
- **Backend / Serveur IIS :** Tourne sous l'identité **`LocalSystem`**. Cette élévation est obligatoire pour interagir avec le sous-système d'impression Windows sans blocage de sécurité UAC ou d'accès mémoire protégé.
- **Service Windows (`Autoprint.Service`) :** S'exécute sous `NT AUTHORITY\SYSTEM` et contrôle le tube nommé `AutoprintPipe` avec une ACL restreinte aux utilisateurs authentifiés (`WellKnownSidType.AuthenticatedUserSid`).
- **Client Utilisateur :** Tourne sans privilèges administrateur (droits utilisateur standard).

### Gestion des Secrets & Chiffrement
- **Mots de passe utilisateurs :** Hachés avec l'algorithme standard ASP.NET Core `PasswordHasher<string>` (PBKDF2 avec sel cryptographique par mot de passe). Le système détecte dynamiquement les anciens condensats hexadécimaux SHA-256 pour les ré-hacher automatiquement lors de la première connexion réussie.
- **Mots de passe de service (AD / SMTP) :** Stockés dans la table `ServerSettings`. Lors des appels API de consultation (`GET /api/settings`), les clés sensibles (`AdServicePassword`, `SmtpPass`, `AgentApiKey`) sont systématiquement masquées par la chaîne de substitution `●●●●●●●●`. Si l'administrateur enregistre la configuration sans modifier ce champ masqué, la valeur en base est préservée sans écrasement.
- **Clé maîtresse JWT :** Générée aléatoirement sur 512 bits (64 octets cryptographiques via `RandomNumberGenerator.GetBytes(64)`) par l'installateur serveur et inscrite dans `appsettings.json`. Un garde-fou au démarrage du serveur interdit formellement l'utilisation de la clé de développement par défaut hors de l'environnement `Development`.
- **Jetons d'Intégration API (`IntegrationToken`) :** Lors de leur création, un token brut au format `ap_tok_<guid>` est présenté une seule et unique fois à l'utilisateur. Seul le condensat SHA-256 de ce jeton est persisté en base de données.

### Authentification & Contrôle d'Accès (RBAC)
- **Authentification Locale vs Active Directory :**
  - Si l'utilisateur est un compte local : validation du hachage de mot de passe en base.
  - Si l'utilisateur est un compte de domaine ou si l'authentification AD est activée : validation des identifiants via `PrincipalContext` avec chiffrement et scellement des paquets (`ContextOptions.Negotiate | ContextOptions.Sealing`). Le système résout aussi bien le format `sAMAccountName` que l'adresse e-mail (`userPrincipalName` ou `mail`).
- **Attribution Dynamique des Rôles par Groupes AD (`AdRoleMappings`) :**
  - Lors de la connexion d'un utilisateur de domaine, le serveur interroge Active Directory avec le filtre d'appartenance récursive `(member:1.2.840.113556.1.4.1941:={userDn})`. Tous les groupes de sécurité parents directs et indirects sont résolus et confrontés à la table `AdRoleMappings` pour affecter automatiquement les rôles applicatifs correspondants.
- **Matrice Granulaire des Permissions :**
  L'application implémente 29 permissions unitaires (`Permission`) portées par les rôles (`Role`) et injectées sous forme de claims (`Permission: <CODE>`) dans le token JWT :
  - `ADMIN_ACCESS` : Accès SuperAdmin total.
  - `PRINTER_READ`, `PRINTER_WRITE`, `PRINTER_DELETE`, `PRINTER_SYNC`, `PRINTER_ARCHIVE`.
  - `LOCATION_READ`, `LOCATION_WRITE`, `LOCATION_DELETE`.
  - `BRAND_READ`, `BRAND_WRITE`, `BRAND_DELETE`.
  - `MODEL_READ`, `MODEL_WRITE`, `MODEL_DELETE`.
  - `DRIVER_READ`, `DRIVER_SCAN`.
  - `USER_READ`, `USER_WRITE`, `USER_DELETE`.
  - `ROLE_READ`, `ROLE_WRITE`, `ROLE_DELETE`.
  - `SETTINGS_MANAGE`, `AUDIT_READ`, `REPORT_MANAGE`, `NETWORK_SCAN`.
  - `SNMP_PROFILE_READ`, `SNMP_PROFILE_WRITE`, `SNMP_PROFILE_DELETE`.
- **Authentification Machine Agent Client :**
  - Le client poste de travail s'authentifie auprès de l'API via l'en-tête HTTP spécifique `X-Agent-Secret`, vérifié directement par le serveur contre la clé `AgentApiKey` stockée dans `ServerSettings`.
- **Authentification API Tierce Externe :**
  - Contrôlée par le filtre `[ApiKey]` qui extrait l'en-tête `X-Api-Token`, calcule son empreinte SHA-256 et valide l'existence ainsi que la date d'expiration dans la table `IntegrationTokens`.

### Protections Applicatives & Anti-Injection
- **Protection Anti-Injection LDAP :** La méthode `SecurityHelper.EscapeLdapFilter()` échappe rigoureusement selon la RFC 4515 les caractères de contrôle LDAP (`\`, `*`, `(`, `)`, `\0`) sur toutes les entrées utilisateurs avant injection dans les filtres `DirectorySearcher`.
- **Protection Anti-Injection SQL :** Utilisation systématique de requêtes paramétrées via Entity Framework Core (LINQ). Les commandes DDL brutes exécutées dans `DbInitializer` sont strictement statiques.
- **Politique CORS :** Restreinte aux origines web Blazor autorisées (`AllowBlazorOrigin`).
- **Politique d'Expiration des Mots de Passe :** Vérification du délai `PasswordExpirationDays` (par défaut 90 jours) pour les comptes locaux. En cas de dépassement ou de réinitialisation manuelle, le claim `ForcePasswordChange = true` est émis dans le JWT, forçant l'affichage immédiat du modal de changement de mot de passe (`SecurityWall.razor`).

### Gestion de la Concurrence, Asynchronisme & Isolation
- **Accès base de données & Concurrence EF Core :** Le contexte de données `ApplicationDbContext` est injecté en durée de vie `Scoped`. Les tâches de fond (`BackgroundService`) instancient systématiquement leur propre portée d'injection de dépendances (`IServiceScopeFactory.CreateScope()`) pour isoler les unités de travail et prévenir toute utilisation concurrente du même contexte EF Core sur plusieurs threads.
- **Protection Anti-Rebond & Verrou Client :** Le client WPF utilise un `SemaphoreSlim(1,1)` pour sérialiser les appels à `RafraichirDonneesAsync()` et empêcher les collisions d'écritures dans `client_cache.db` lors de changements réseau rapides ou d'événements SignalR simultanés.
- **Sérialisation JSON & Références Circulaires :** Le serveur et le client configurent `ReferenceHandler.IgnoreCycles` pour éliminer tout risque de boucle infinie lors de la sérialisation des graphes d'objets bidirectionnels (ex: `Emplacement` <-> `EmplacementNetwork`, `Role` <-> `UserRole`).

---

## 5. Limitations Actuelles & Dette Technique

1. **Mono-serveur de Spouleur physique (absence de clustering de spouleurs) :**
   Le service de synchronisation manipule directement le spouleur local de la machine hébergeant le serveur Autoprint. Il ne pilote pas une batterie de serveurs d'impression distants hétérogènes en mode distribué actif/actif.
2. **Migrations de structure SQLite manuelles dans `DbInitializer` :**
   Pour les déploiements utilisant le moteur SQLite, les évolutions de schéma sont exécutées par des requêtes DDL brutes (`ALTER TABLE ... ADD COLUMN`, `CREATE TABLE IF NOT EXISTS`) capturées dans des blocs `try/catch` vides au sein de `DbInitializer.cs`, au lieu d'exploiter un pipeline de migration formel EF Core identique à celui de SQL Server.
3. **Sur-privilège du processus serveur web sous IIS :**
   L'exécution du pool IIS sous l'identité `LocalSystem` est imposée par les besoins d'accès au Spouleur Windows et à WMI. Cela confère au processus web des privilèges très élevés sur la machine hôte.
4. **Absence de file d'attente de réessais (Retry Queue avec Exponential Backoff) :**
   En cas d'échec d'envoi d'un e-mail SMTP (rapport planifié ou alerte de découverte) ou d'une notification SignalR, l'exception est consignée dans les logs mais le message n'est pas réinjecté dans une file de retry persistante pour rejeu différé.
5. **Supervision séquentielle dans `PrinterMonitoringWorker` :**
   Le démon de supervision parcourt la liste des imprimantes actives dans une boucle séquentielle (`foreach`), ce qui peut allonger la durée totale d'un cycle de scan sur des parcs volumineux comprenant plusieurs centaines de machines (contrairement au service de découverte qui utilise `Parallel.ForEachAsync`).
6. **Verrous de concurrence en mémoire vive (in-memory) :**
   Le contrôle de concurrence du client repose sur un `SemaphoreSlim` local au processus, ce qui est suffisant pour le poste client mais ne permettrait pas un partitionnement horizontal sans refonte.

---

## 6. AI Context Summary

```yaml
project_name: "Autoprint"
version: "26.8.24.3"
runtime: ".NET 10.0 (C# 13)"
target_environment: "Windows Server (2016-2025) / Windows 10/11 x64"
architecture:
  backend: "ASP.NET Core Web API / SignalR / Hosted Workers (net10.0)"
  frontend_web: "Blazor WebAssembly Standalone with Radzen UI (net10.0)"
  client_desktop: "WPF Systray Application with Material Design (net10.0-windows)"
  worker_service: "Windows Service Worker via NamedPipe IPC (net10.0-windows)"
  installer: "WPF Bootstrapper + WiX Toolset v4/v5 MSIs"
  databases:
    server: "Microsoft SQL Server or SQLite (Autoprint.db)"
    client: "Local SQLite (client_cache.db)"
core_purpose: >
  Centralized print server management, Windows Print Spooler automation (TCP/IP ports,
  Branch Office Direct Printing, drivers, naming templates), network CIDR geolocation-based
  printer mapping for desktop users, RFC 3805 SNMP monitoring with linear regression toner
  prediction, automated PDF/CSV reporting, and Active Directory LDAP integration.
key_workflows:
  - "Spooler synchronization (P/Invoke winspool.drv + WMI CIM Win32_Printer/Win32_TCPIPPrinterPort)"
  - "Client automatic printer assignment by IPv4 CIDR matching and preferred default printer selection"
  - "Dynamic SNMP MIB auto-discovery and UDP single-packet telemetry gathering"
  - "Predictive toner exhaustion calculation via linear regression (least squares)"
  - "Active Directory LDAP nested group mapping to internal RBAC permissions"
strict_limitations:
  - "No print job interception, quota management, or document spooling proxy (not a PaperCut replacement)"
  - "Strictly Windows-dependent: requires winspool.drv, WMI, IIS, and Windows Print Spooler"
  - "No external message broker: SignalR in-memory and local Named Pipes IPC"
  - "Single-tenant infrastructure model"
known_debt:
  - "IIS application pool requires LocalSystem identity for spooler/WMI access"
  - "Manual raw SQL schema patching in DbInitializer for SQLite runtime updates"
  - "Sequential printer polling loop in PrinterMonitoringWorker"
  - "No persistent retry queue for failed SMTP email deliveries"
```
