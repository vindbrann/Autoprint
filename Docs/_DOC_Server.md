# 📘 Documentation Technique : Module Serveur (Autoprint.Server)

## 1. Présentation de l'Architecture
Le module **Autoprint.Server** est une application **ASP.NET Core Web API** (.NET 10) agissant comme point central de configuration, de supervision et d'orchestration. Il assure l'interface entre la base de données relationnelle, le sous-système d'impression Windows (Spouleur), les agents clients déployés et les outils tiers de supervision.

* **Rôle :** Contrôleur de domaine d'impression, API Rest, Console de supervision web, Interface d'administration.
* **Hébergement :** IIS (Internet Information Services) sur Windows Server 2019/2022.
* **Exécution :** Pool d'application en mode "No Managed Code" avec identité de service (`NetworkService`).

---

## 2. Stack Technologique & Backend

### 2.1 Socle Applicatif
* **Framework :** .NET 10 (C#).
* **ORM (Object-Relational Mapping) :** Entity Framework Core.
    * *Optimisation :* Activation du `Query Splitting Behavior` pour prévenir l'explosion cartésienne sur les requêtes relationnelles complexes (Utilisateurs > Rôles > Permissions).
* **Base de Données :**
    * **Production :** SQL Server (Authentification Windows intégrée ou Mixte).
    * **Développement/Démo :** SQLite.
* **Communication Temps Réel :** SignalR (WebSockets) implémentant une architecture "Push-to-Pull" (Notification de changement uniquement, pas de payload de données).

### 2.2 Couche d'Interopérabilité Système (Win32 API)
Pour garantir la stabilité et contourner les limitations de WMI sur les pilotes hétérogènes (V3/V4), l'application interagit directement avec le Spouleur Windows via l'API native.

* **Service :** `WindowsPrintSpoolerService`.
* **Technologie :** P/Invoke (Platform Invocation) sur `winspool.drv`.
* **Fonctions Critiques :**
    * Création/Suppression de ports TCP/IP standards.
    * Gestion du partage SMB et des ACLs d'impression.
    * **Mode Filiale (Branch Office Direct Printing) :** Application hybride des paramètres pour compatibilité totale :
        1. Activation de l'attribut spouleur `PRINTER_ATTRIBUTE_RAW_ONLY` (Pilotes V4).
        2. Injection de la clé de registre `EnableBranchOfficePrinting` via `SetPrinterDataEx` (Pilotes V3 Legacy).

---

## 3. Logique Métier et Synchronisation

### 3.1 Mécanisme de "Staging"
L'architecture dissocie la **Configuration** (Base de données) de l'**État Système** (Windows) pour éviter les incohérences en cas d'erreur système.

* **Service :** `SyncSpoolerService`.
* **Processus :** Les modifications administratives placent les objets en état "En Attente". Une validation explicite déclenche l'application sur le serveur Windows.
* **Auto-Réparation (Self-Healing) :** Lors de la synchronisation, le service vérifie l'existence réelle de l'imprimante sur le serveur hôte. Si une imprimante déclarée en base a été supprimée manuellement du serveur, elle est automatiquement recréée.

### 3.2 Gestion des Pilotes 
L'application interdit la création manuelle de fiches pilotes pour garantir l'intégrité référentielle avec le système de fichiers.

* **Inventaire :** Scan WMI 64-bit (`Win32_PrinterDriver`) pour récupérer le `FileVersion` exact.
* **Filtrage :** Exclusion native des pilotes génériques Microsoft (Fax, PDF, XPS) via Blacklist.
* **Cycle de Vie (Smart Cleaning) :**
    * Pilote disparu + Inutilisé = Suppression physique de la base (Purge).
    * Pilote disparu + Utilisé par un modèle = Maintien en base avec statut "Introuvable" pour alerte administrative.

---

## 4. Module de Supervision et de Diagnostics (Nouveauté V2)

La V2 d'Autoprint introduit un moteur complet de collecte d'informations et de reporting sur le parc d'impression :

### 4.1 Collecte Automatique (`PrinterMonitoringWorker`)
Un service d'arrière-plan hébergé (`IHostedService`) réalise des scans réguliers du parc réseau :
* **Interrogation :** Ping réseau et requêtes SNMP.
* **Plages Horaires :** Le scan s'exécute selon les réglages paramétrés (ex. entre 8h et 18h en semaine, désactivable le week-end).
* **Historisation :** Enregistre périodiquement l'évolution des niveaux de toner pour l'analyse prédictive.

### 4.2 Diagnostic Temps Réel (`SnmpService`)
Permet de tester à la demande la connectivité d'une imprimante directement depuis l'interface web (Ping et récupération des OID SNMP génériques ou spécifiques).

### 4.3 Algorithme Prédictif (`PredictiveService`)
Analyse les enregistrements historiques (`TonerHistory`) pour extrapoler la courbe de consommation des toners de chaque imprimante et prédire le nombre de jours restants avant épuisement.

### 4.4 Profils SNMP Personnalisés
Permet d'ajouter des configurations d'OIDs personnalisés par marque/modèle pour surcharger les requêtes de supervision sur les imprimantes non conformes aux MIBs standards (RFC 3805).

### 4.5 Rapports Planifiés (`ReportGeneratorService`)
Génère et transmet automatiquement par e-mail (via SMTP) des rapports périodiques d'activité et de santé (au format PDF ou CSV) aux administrateurs réseau.

---

## 5. Sécurité et Contrôle d'Accès

### 5.1 Authentification Hybride
Le système supporte deux modes d'authentification simultanés :
* **Comptes Locaux :** Stockage sécurisé via **PBKDF2 salé** (classe `PasswordHasher` d'ASP.NET Core). Lors de la première connexion réussie d'un compte héritant de l'ancienne version V1, sa signature SHA-256 brute est automatiquement et de manière transparente mise à jour vers le format PBKDF2.
* **Active Directory :** Connecteur LDAP (`System.DirectoryServices`) avec mapping de groupes de sécurité AD vers des Rôles applicatifs. Les entrées utilisateurs sont systématiquement désinfectées via `SecurityHelper.EscapeLdapFilter` pour prévenir les injections de filtres LDAP.

### 5.2 Protocole d'Échange
* **Web UI (Blazor) :** Tokens JWT (JSON Web Tokens) avec injection des claims de rôles. Une vérification bloque le démarrage de l'application en production si la clé JWT par défaut est détectée.
* **Agents (M2M) :** Authentification par clé d'API Agent (`AgentApiKey`) transmise via l'en-tête HTTP `X-Agent-Secret`.
* **Intégrations Tiers :** Authentification par jetons d'intégration via le header HTTP `X-Api-Token` pour consommer les endpoints d'exportation des métriques de supervision.

### 5.3 RBAC (Role-Based Access Control)
La matrice de droits distingue les entités gérées. La V2 a étendu ces droits avec des privilèges fins :
* `REPORT_MANAGE` : Gestion et planification des rapports d'activité.
* `SNMP_PROFILE_READ`, `SNMP_PROFILE_WRITE`, `SNMP_PROFILE_DELETE` : Gestion des profils SNMP personnalisés.
* `PRINTER_ARCHIVE` : Droit d'archiver manuellement ou de restaurer des imprimantes archivées.

---

## 6. Déploiement et Maintenance

### 6.1 Installeur Serveur (WPF)
Le déploiement automatise la configuration système :
* **Vérification des Pré-requis :** Rôles serveur (IIS, Spouleur) et runtime .NET.
* **Configuration IIS :** Utilisation de `Microsoft.Web.Administration` pour configurer le pool et le site Web.
* **Migrations de base de données :** L'installateur invoque le serveur avec l'argument `--migrate-only` pour initialiser ou mettre à jour la structure de la base (SQL Server / SQLite) avant de démarrer le service IIS.
