Tu as tout à fait raison. Mes excuses pour cette tonalité inadaptée. "Le cerveau" ou "les muscles", c'est bon pour une présentation marketing grand public, pas pour une documentation d'exploitation destinée à des professionnels de l'IT.

Reprenons avec une approche purement technique, factuelle et orientée "Architecture Système", conforme à un standard de documentation technique d'entreprise.

Voici la version révisée pour la partie **Serveur**.

---

# 📘 Documentation Technique : Module Serveur (Autoprint.Server)

## 1. Présentation de l'Architecture
Le module **Autoprint.Server** est une application **ASP.NET Core Web API** (.NET 10) agissant comme point central de configuration et d'orchestration. Il assure l'interface entre la base de données relationnelle, le sous-système d'impression Windows (Spouleur) et les agents clients déployés.

* **Rôle :** Contrôleur de domaine d'impression, API Rest, Interface d'administration.
* **Hébergement :** IIS (Internet Information Services) sur Windows Server 2019/2022.
* **Exécution :** Pool d'application en mode "No Managed Code" avec identité de service (`NetworkService`).

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
        1.  Activation de l'attribut spouleur `PRINTER_ATTRIBUTE_RAW_ONLY` (Pilotes V4).
        2.  Injection de la clé de registre `EnableBranchOfficePrinting` via `SetPrinterDataEx` (Pilotes V3 Legacy).

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

## 4. Sécurité et Contrôle d'Accès

### 4.1 Authentification Hybride
Le système supporte deux modes d'authentification simultanés :
* **Comptes Locaux :** Stockage hashé (SHA-256 Hexadécimal) pour l'administration de secours.
* **Active Directory :** Connecteur LDAP (`System.DirectoryServices`) avec mapping de groupes de sécurité AD vers des Rôles applicatifs.

### 4.2 Protocole d'Échange
* **Web UI :** Tokens JWT (JSON Web Tokens) avec injection des revendications (Claims) de rôles.
* **Agents (M2M) :** Authentification par Clé d'API (`AgentApiKey`) transmise via l'en-tête HTTP `X-Agent-Secret`. Cette clé est générée cryptographiquement à l'installation.

### 4.3 RBAC (Role-Based Access Control)
La sécurité repose sur une matrice de droits stockée en base de données, distinguant les **Modules** (Imprimantes, Lieux, Système) des **Actions** (Lecture, Écriture, Suppression, Scan, Sync).

## 5. Déploiement et Maintenance

### 5.1 Installeur Serveur
Le déploiement est assuré par un exécutable "Self-Contained" (WPF .NET 10) agissant comme wrapper intelligent.

* **Vérification des Pré-requis :** Audit WMI strict des rôles serveur (IIS, Print Services) et du Runtime .NET avant installation.
* **Configuration IIS :** Utilisation de `Microsoft.Web.Administration` pour la création du Site et du Pool, avec application des ACLs NTFS (`GenericWrite`) sur le dossier d'installation pour le compte de service.

### 5.2 Disaster Recovery (PRA)
* **Sauvegarde :** Export complet de la configuration (Metadatas) au format JSON. Les fichiers pilotes (binaires) sont exclus et relèvent de la sauvegarde infrastructure (VM).
* **Restauration :** Moteur transactionnel effectuant un nettoyage de la base et une réinjection des données avec préservation des IDs.
