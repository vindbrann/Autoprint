# 🛡️ Rapport d'Audit de Sécurité - Autoprint

**Date de l'audit :** 24 Août 2026  
**Cible :** Solution Autoprint (API Serveur, Authentification, SNMP, Rapports, Découverte)  
**Objectif :** Identifier les vulnérabilités résiduelles et proposer un plan d'action correctif avant mise en production en entreprise.

---

## 📊 Synthèse des Vulnérabilités Identifiées

| Niveau | Vulnérabilité | Composant / Fichier | Risque |
|---|---|---|---|
| 🔴 **Critique** | Absence totale d'authentification sur le contrôleur de découverte | `DiscoveryController.cs` | Accès public anonyme, déclenchement de scans réseau |
| 🟠 **Élevé** | Fuite de mots de passe sensibles (Active Directory, SMTP) en clair | `SettingsController.cs` (`GetSettings`) | Compromission du compte de service AD et du SMTP |
| 🟡 **Moyen** | Droits insuffisants sur les fonctions de scan et test SNMP | `SnmpProfilesController.cs` | Scan d'adresses internes par des utilisateurs restreints (SSRF) |
| 🟡 **Moyen** | Injection de formules dans les exports CSV (CSV Injection) | `ReportGeneratorService.cs` (`EscapeCsv`) | Exécution de code/DDE à l'ouverture du CSV dans Excel |
| 🔵 **Faible** | Incomplétude de la sauvegarde / restauration système | `BackupController.cs` | Perte des profils SNMP et des rapports lors d'un restore |

---

## 🔍 Analyse Détaillée et Recommandations

---

### 🔴 1. Absence d'authentification sur la Découverte Réseau (`DiscoveryController`)

* **Localisation :** `Autoprint.Server/Controllers/DiscoveryController.cs`
* **CWE :** CWE-306 (Missing Authentication for Critical Function)
* **Constat :**
  La classe `DiscoveryController` ne dispose d'aucun attribut `[Authorize]`. Aucune des méthodes (`GetProfiles`, `SaveProfile`, `DeleteProfile`, `RunScanNow`, `ScanNetwork`) n'est protégée.
* **Impact en entreprise :**
  Un poste non authentifié sur le réseau d'entreprise peut :
  1. Consulter la cartographie réseau (`GET /api/discovery`).
  2. Modifier ou supprimer des profils de découverte.
  3. Lancer un scan réseau massif arbitraire (`POST /api/discovery/scan-network` avec n'importe quel sous-réseau `10.0.0.0/8`), entraînant des ralentissements ou servant de reconnaissance interne pour un attaquant.
* **Correction à apporter :**
  1. Ajouter `[Authorize]` au niveau de la classe `DiscoveryController`.
  2. Appliquer les politiques de permissions granulaires :
     * `[Authorize(Policy = "SETTINGS_MANAGE")]` sur les actions de configuration et d'exécution des scans.

---

### 🟠 2. Exposition des Mots de Passe en Clair (`SettingsController.GetSettings`)

* **Localisation :** `Autoprint.Server/Controllers/SettingsController.cs`
* **CWE :** CWE-200 (Exposure of Sensitive Information) / CWE-319
* **Constat :**
  La méthode `GetSettings()` retourne l'ensemble brut des lignes de la table `ServerSettings` à n'importe quel utilisateur connecté (y compris un utilisateur avec de simples droits de consultation).
  Ces paramètres contiennent notamment :
  * `AdServicePassword` : Le mot de passe du compte de service Active Directory.
  * `SmtpPass` : Le mot de passe du serveur de messagerie SMTP.
  * `AgentApiKey` : La clé secrète partagée avec les agents clients.
* **Impact en entreprise :**
  Un utilisateur standard ou un compte applicatif peut extraire le mot de passe du compte de service Active Directory et tenter une élévation de privilèges ou un mouvement latéral sur le domaine d'entreprise.
* **Correction à apporter :**
  1. Restreindre `GetSettings()` avec `[Authorize(Policy = "SETTINGS_MANAGE")]`.
  2. Masquer systématiquement les valeurs de `AdServicePassword`, `SmtpPass` et `AgentApiKey` lors de la sérialisation (ex: renvoyer `"●●●●●●●●"` ou `null` si non modifié).

---

### 🟡 3. Contrôle d'Accès Insuffisant sur les Scans et Tests SNMP

* **Localisation :** `Autoprint.Server/Controllers/SnmpProfilesController.cs`
* **CWE :** CWE-918 (Server-Side Request Forgery - SSRF) / CWE-862 (Missing Authorization)
* **Constat :**
  Les endpoints `POST /api/SnmpProfiles/scan-printer` et `POST /api/SnmpProfiles/test-profile` n'utilisent qu'un attribut `[Authorize]` générique sans exiger de politique (`Policy = "SNMP_PROFILE_WRITE"`).
* **Impact en entreprise :**
  Un utilisateur avec un accès restreint (ex: lecture seule) peut envoyer des requêtes UDP arbitraires vers n'importe quelle adresse IP du réseau interne depuis le serveur d'impression.
* **Correction à apporter :**
  Ajouter explicitement `[Authorize(Policy = "SNMP_PROFILE_WRITE")]` sur ces deux endpoints.

---

### 🟡 4. Injection de Formules CSV (CSV Formula Injection)

* **Localisation :** `Autoprint.Server/Services/ReportGeneratorService.cs` (`EscapeCsv`)
* **CWE :** CWE-1236 (Improper Neutralization of Formula Elements in CSV)
* **Constat :**
  La méthode `EscapeCsv()` protège contre la rupture de syntaxe CSV (doublage des guillemets), mais n'échappe pas les caractères d'initiation de formules (`=`, `+`, `-`, `@`).
* **Impact en entreprise :**
  Si une imprimante est nommée (ou renvoie une alerte) avec une chaîne commençant par `=cmd|'/C calc'!A0`, à l'ouverture du rapport CSV dans Microsoft Excel par un gestionnaire ou un administrateur, Excel tente d'exécuter la formule ou le protocole DDE.
* **Correction à apporter :**
  Préfixer toute chaîne commençant par `=`, `+`, `-`, `@` par une apostrophe `'` dans `EscapeCsv`.

---

### 🔵 5. Données Manquantes dans l'Export/Restauration (`BackupController`)

* **Localisation :** `Autoprint.Server/Controllers/BackupController.cs`
* **CWE :** CWE-404 (Resource Management / Data Integrity)
* **Constat :**
  Le contrôleur de sauvegarde n'inclut pas les nouvelles entités créées récemment :
  * `SnmpProfiles` et `SnmpProfileItems`
  * `ReportSchedules`
  * `IntegrationTokens`
  * `Imprimantes.SerialNumber`
* **Impact en entreprise :**
  Lors d'une restauration de sauvegarde, toutes les configurations de profils SNMP et de rapports planifiés sont écrasées et perdues, provoquant des erreurs sur les modèles d'imprimantes liés.
* **Correction à apporter :**
  Compléter `BackupRootDto` et la méthode `Restore()` pour inclure la sauvegarde et la restauration de ces entités.

---

## 📋 Plan de Remédiation Recommandé

1. **Phase 1 (Immédiate - Sécurité critique & accès)** :
   * Protéger `DiscoveryController` avec `[Authorize(Policy = "SETTINGS_MANAGE")]`.
   * Masquer les mots de passe et restreindre l'accès à `GET /api/Settings`.
   * Renforcer l'autorisation sur `scan-printer` et `test-profile` (`SNMP_PROFILE_WRITE`).
2. **Phase 2 (Durcissement)** :
   * Sécuriser l'export CSV contre l'injection de formules dans `ReportGeneratorService`.
3. **Phase 3 (Maintenance & Intégrité)** :
   * Mettre à niveau le contrôleur de sauvegarde `BackupController`.
