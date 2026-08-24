<div align="center">

# 🖨️ Autoprint

**Solution Entreprise de Gestion Dynamique et Supervision d'Impression pour Windows**

[![Version](https://img.shields.io/badge/version-26.08.24.2-blue.svg?style=for-the-badge&logo=github)](Docs/PATCH_NOTES_V2.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor WASM](https://img.shields.io/badge/Console-Blazor%20WASM-7852FF.svg?style=for-the-badge&logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![WPF Client](https://img.shields.io/badge/Client-WPF%20Agent-0078D4.svg?style=for-the-badge&logo=windows)](https://github.com/dotnet/wpf)
[![Database](https://img.shields.io/badge/Database-SQL%20Server%20%7C%20SQLite-CC292B.svg?style=for-the-badge&logo=microsoft-sql-server)](https://www.microsoft.com/sql-server)
[![Security](https://img.shields.io/badge/Security-Hardened%20%2F%20RBAC-success.svg?style=for-the-badge&logo=shield)](Docs/_DOC_Securite.md)

<p align="center">
  <b>Autoprint</b> simplifie le déploiement, la mobilité et la maintenance proactive de vos imprimantes en entreprise.<br/>
  Conçu pour les environnements <b>Zero-Trust</b> et <b>Microsoft Intune</b>, il allie légèreté pour l'utilisateur et puissance de supervision pour les administrateurs IT.
</p>

[Fonctionnalités](#-fonctionnalités-clés) • [Architecture](#-architecture-des-composants) • [Sécurité](#-sécurité--conformité) • [Déploiement](#-installation--déploiement) • [Documentation](#-documentation)

---

</div>

## 🌟 Fonctionnalités Clés

### 📍 Mobilité & Déploiement Agile
* **Détection Automatique de Lieu (Location Awareness)** : L'agent client analyse le sous-réseau (CIDR IP) du poste et connecte instantanément les imprimantes du bureau où se trouve l'utilisateur.
* **Mode Filiale / Direct IP** : Impression directe vers les imprimantes IP sur les sites distants à faible bande passante, sans saturer le serveur central.
* **Synchronisation Sécurisée & Staging** : Gestion centralisée des files d'attente avec prévisualisation et application contrôlée sur le spouleur d'impression Windows (`winspool.drv`).
* **Offline-First** : Cache local SQLite sur le poste utilisateur pour conserver l'accès aux imprimantes même lors d'une coupure réseau.

### 📡 Supervision Réseau & Intelligence SNMP
* **Profils SNMP Dynamiques & Multi-consommables** : Suivi illimité et précis des toners, bacs à papier (*Trays*), bacs de récupération, tambours et compteurs de pages (compatibilité RFC 3805).
* **⚡ Auto-détection & Scan Réseau** : Assistant de découverte automatique des OIDs par balayage d'imprimante de test en un clic.
* **⏳ Algorithme Prédictif** : Estimation du nombre de jours restants avant l'épuisement des toners pour anticiper les commandes de consommables.
* **📑 Rapports Modulaires Automatisés** : Génération et expédition planifiée par e-mail de rapports PDF haute fidélité et d'exports CSV/Excel.
* **🔗 API d'Intégration Tiers** : Connecteurs REST sécurisés par jetons pour vos outils ITSM/Monitoring (GLPI, PRTG, Zabbix).

---

## 🏛️ Architecture des Composants

L'architecture d'Autoprint s'articule autour de trois modules complémentaires :

| Composant | Rôle | Technologies |
|---|---|---|
| **🌐 Console Web (Admin)** | Interface d'administration centralisée, cartographie des lieux, gestion des profils SNMP, supervision du parc et attribution des droits. | Blazor WebAssembly, Radzen UI, .NET 10 |
| **⚙️ Serveur & API Web** | Moteur central : API REST, synchronisation du spouleur Windows, scans d'arrière-plan, algorithmes prédictifs et génération des rapports. | ASP.NET Core API, EF Core, SQL Server / SQLite, SignalR |
| **💻 Agent Client (Bureau)** | Application légère en barre des tâches pour l'utilisateur. Connecte automatiquement les imprimantes selon le lieu sans droits administrateur. | WPF (.NET 10), SQLite Local, P/Invoke `printui.dll` |

---

## 🛡️ Sécurité & Conformité

Autoprint intègre un modèle de sécurité rigoureux et conforme aux exigences des environnements d'entreprise :

* **Architecture Zéro-Privilège (Zero-Trust)** : L'agent client s'exécute exclusivement dans le contexte de la session utilisateur standard, sans aucun service d'arrière-plan à privilèges élevés (`SYSTEM`).
* **Contrôle d'Accès Granulaire (RBAC)** : Matrice fine de permissions applicatives (`NETWORK_SCAN`, `REPORT_MANAGE`, `SNMP_PROFILE_WRITE`, `SETTINGS_MANAGE`, `PRINTER_ARCHIVE`, etc.) avec synchronisation et possibilité de liaison Active Directory.
* **Protection des Données & Secrets** :
  * Hachage fort des mots de passe locaux via **PBKDF2 salé** (recommandation NIST).
  * Masquage systématique des données sensibles (mots de passe de service AD, identifiants SMTP, clés API) dans l'API et l'interface Web.
  * Validation stricte des certificats SSL/TLS pour l'ensemble des flux réseau.
* **Désinfection des Entrées & Neutralisation des Injections** :
  * Échappement systématique des filtres LDAP Active Directory.
  * Assainissement des paramètres d'appel d'impression système.
  * Protection contre l'injection de formules CSV (CWE-1236) lors de l'ouverture des exports dans Excel.
* **Audit & Traçabilité Complète** : Journalisation immuable de toutes les actions d'administration avec historique comparatif des modifications.

---

## 🚀 Installation & Déploiement

### 1. Serveur d'Impression
Le déploiement du serveur s'effectue via un assistant graphique interactif (`Autoprint.Server.Setup.exe`) :
1. Détection automatique des prérequis (IIS, rôles Windows Server, Runtime .NET).
2. Configuration de la base de données (SQL Server d'entreprise ou SQLite local).
3. Création automatique du site Web IIS et initialisation des données.

### 2. Postes Clients (Déploiement Silencieux)
Le client est packagé sous forme de package MSI standard, optimisé pour les outils de gestion de parc (**Microsoft Intune**, **GPO Active Directory**, **SCCM**) :

```cmd
msiexec /i AutoprintClient.msi PRINTSERVER="https://autoprint.entreprise.corp" APIKEY="votre-cle-api-agent" /qn
```

* `PRINTSERVER` : URL HTTPS sécurisée de votre instance Autoprint.
* `APIKEY` : Clé d'authentification agent générée dans la console d'administration.

---

## 📚 Documentation

Consultez nos guides détaillés dans le dossier [`Docs/`](Docs/) :

* 📖 **[Guide d'Installation Serveur](Docs/INSTALLATION_SERVER.MD)** : Procédure pas-à-pas pour Windows Server & IIS.
* 🛡️ **[Rapport de Sécurisation & Contrôles](Docs/_DOC_Securite.md)** : Détail des 11 mesures de sécurité et protections implémentées.
* 🔍 **[Audit de Sécurité](Docs/_AUDIT_SECURITE_V2.md)** : Matrice des vulnérabilités traitées.
* 📝 **[Notes de Version (Patch Notes)](Docs/PATCH_NOTES_V2.md)** : Historique exhaustif des évolutions par version.
* 👤 **[Manuel Utilisateur Client](Docs/_Manuel_Client.md)** : Guide d'utilisation de l'agent de bureau.
* 🎛️ **[Manuel Administrateur Serveur](Docs/_Manuel_Serveur.md)** : Guide de gestion de la console web.

---

<div align="center">
  <sub>Autoprint - Conçu pour simplifier et sécuriser l'impression en entreprise.</sub>
</div>