# Rapport de Sécurisation de l'Application Autoprint (V2)

Ce document récapitule l'ensemble des correctifs et des améliorations de sécurité apportés à l'application Autoprint (branche `feature/v2`). Ces modifications visent à répondre aux exigences de sécurité pour un déploiement en production et à prévenir les remarques d'audits de sécurité ou de tests d'intrusion.

---

## Sommaire des Correctifs Applicatifs

| N° | Composant | Type de Menace | Action corrective |
|---|---|---|---|
| 1 | Client & Setup | Élévation locale de privilèges (LPE) | Suppression totale du service Windows d'arrière-plan |
| 2 | Client | Man-In-The-Middle (MITM) | Rétablissement de la validation des certificats SSL/TLS |
| 3 | Client | Injection d'arguments (printui.dll) | Désinfection et retrait des guillemets dans les chemins d'imprimantes |
| 4 | Serveur | Injection LDAP (Active Directory) | Échappement des caractères spéciaux LDAP sur les entrées utilisateur |
| 5 | Serveur | Hachage de mot de passe faible (SHA-256) | Migration vers PBKDF2 salé avec mise à niveau automatique des comptes |
| 6 | Serveur | Secret par défaut en clair (JWT Key) | Blocage du démarrage en production avec la clé par défaut & Documentation |
| 7 | Serveur | Contrôle d'accès Découverte Réseau (CWE-306) | Création de la permission `NETWORK_SCAN` et verrouillage de `DiscoveryController` |
| 8 | Serveur | Fuite de secrets en clair (CWE-200) | Masquage des mots de passe AD, SMTP et clé API dans `SettingsController` |
| 9 | Serveur | Probe réseau / SSRF (CWE-918) | Restriction des actions de scan et test SNMP à la permission `SNMP_PROFILE_WRITE` |
| 10 | Serveur | Injection de formules CSV (CWE-1236) | Neutralisation des caractères initiateurs de formules (`=`, `+`, `-`, `@`) dans l'export CSV |
| 11 | Serveur | Intégrité Sauvegarde & Restauration (CWE-404) | Prise en charge intégrale des Profils SNMP, Rapports, Jetons et Numéros de série |

---

## Détails des Vulnérabilités et Correctifs Appliqués

### 1. Suppression du Service Local (`Autoprint.Service`)
* **Pourquoi ?**
  Le service Windows (`AutoprintWorker`) s'exécutait avec les privilèges maximum (`LocalSystem`) pour installer les pilotes d'impression. Cependant, l'appel à `printui.dll` se faisait par concaténation de chaînes non sécurisées depuis un canal de communication local (Named Pipes) ouvert à tous. Un utilisateur standard local pouvait forger une requête malveillante pour faire installer au service un pilote arbitraire et exécuter du code en tant que `SYSTEM` (Local Privilege Escalation). De plus, ce comportement déclenchait des blocages silencieux par les outils de sécurité (EDR/Antivirus).
* **Ce qui a été fait :**
  Le service a été entièrement retiré. Le client s'exécute désormais uniquement avec les privilèges de l'utilisateur connecté en effectuant les connexions d'imprimantes réseau via `printui.dll` en mode standard (les pilotes doivent être pré-déployés sur les postes via des outils managés comme Intune).

---

### 2. Validation des Certificats SSL/TLS (Client)
* **Pourquoi ?**
  Dans `ApiService.cs`, le code désactivait volontairement la vérification des certificats SSL/TLS (`ServerCertificateCustomValidationCallback = (...) => true`). Un attaquant réseau (Man-In-The-Middle) pouvait ainsi usurper l'identité du serveur d'impression, intercepter la clé API (`X-Agent-Secret`) en clair, et injecter de fausses imprimantes.
* **Ce qui a été fait :**
  Ce contournement a été retiré. Le client valide désormais strictement la chaîne de confiance et les dates de validité du certificat SSL (Let's Encrypt ou autorité interne).

---

### 3. Injection d'Arguments dans `printui.dll` (Client)
* **Pourquoi ?**
  Le chemin réseau de l'imprimante (`uncPath`) contenant le nom de partage provenant de la base de données était concaténé directement dans la ligne d'arguments passée à `printui.dll`. Si la base de données était compromise, un attaquant pouvait nommer une imprimante avec des guillemets doubles (ex: `imprimante" /y "autre_chose`) pour injecter des paramètres non autorisés lors de l'exécution système de la commande.
* **Ce qui a été fait :**
  Dans `MainWindowViewModel.cs`, tout caractère de guillemet double (`"`) présent dans le nom de l'imprimante est systématiquement supprimé avant la construction de la ligne d'arguments, garantissant que l'argument reste encapsulé dans sa chaîne.

---

### 4. Protection contre les Injections LDAP (Serveur)
* **Pourquoi ?**
  Lors de l'authentification Windows AD ou de la recherche d'utilisateurs/groupes, le nom d'utilisateur saisi par l'opérateur était inséré tel quel par concaténation brute dans le filtre de recherche LDAP de l'Active Directory. Un attaquant pouvait entrer des caractères spéciaux (ex: `*)(objectClass=*`) pour altérer la requête et contourner des contrôles de sécurité ou surcharger l'AD.
* **Ce qui a été fait :**
  Une fonction d'échappement robuste `SecurityHelper.EscapeLdapFilter` a été créée pour neutraliser les caractères LDAP spéciaux (`\`, `*`, `(`, `)`, `\0`). Toutes les requêtes LDAP (connexion, recherche et synchronisation) utilisent désormais des variables échappées.

---

### 5. Hachage Fort des Mots de Passe Locaux (Serveur)
* **Pourquoi ?**
  Les mots de passe des comptes locaux (comme le compte `admin` par défaut) étaient hachés en utilisant l'algorithme SHA-256 brut sans sel. Les hachages SHA-256 bruts sont vulnérables au cassage ultra-rapide par force brute ou par table de correspondance précalculée (*Rainbow Tables*).
* **Ce qui a été fait :**
  L'application a été migrée vers `Microsoft.AspNetCore.Identity.PasswordHasher` (hachage PBKDF2 avec sel unique automatique).
  * **Migration transparente** : La méthode de vérification est rétrocompatible. Lorsqu'un utilisateur se connecte avec succès pour la première fois, le serveur détecte l'ancienne signature SHA-256, valide la connexion, recalcule immédiatement l'empreinte au format PBKDF2 et la met à jour en base de données. Aucun utilisateur n'est bloqué et les anciens hashes disparaissent progressivement au fil des connexions.

---

### 6. Protection du Secret en Clair (JWT Key)
* **Pourquoi ?**
  Le fichier `appsettings.json` contenait une clé JWT par défaut (`CeciEstUneCleSecrete...`). Même s'il s'agissait d'une valeur de développement, les outils d'audit statique et les testeurs la signalaient comme une faille majeure.
* **Ce qui a été fait :**
  * **Garde-fou applicatif** : Dans `Program.cs`, un test de sécurité vérifie la clé au démarrage. Si la clé par défaut est utilisée alors que le serveur s'exécute en dehors du mode de développement (`!IsDevelopment()`), le serveur s'arrête immédiatement et refuse de démarrer.
  * **Documentation dans le JSON** : Une propriété `"__Note__"` a été rajoutée dans `appsettings.json` juste sous la clé de développement pour expliciter clairement aux auditeurs que l'installateur du serveur d'impression génère et injecte automatiquement une clé aléatoire sécurisée de 512 bits lors du déploiement en production.

---

### 7. Contrôle d'Accès sur la Découverte Réseau (`DiscoveryController`)
* **Pourquoi ?**
  Le contrôleur de découverte réseau n'était protégé par aucun attribut d'autorisation, permettant à des utilisateurs anonymes d'accéder aux profils et de lancer des scans.
* **Ce qui a été fait :**
  Création d'une permission granulaire `NETWORK_SCAN` (« Scan réseau / Découverte »), intégrée à la matrice des rôles et sécurisation de toutes les actions de `DiscoveryController` par `[Authorize(Policy = "NETWORK_SCAN")]`.

---

### 8. Masquage des Secrets dans les Paramètres (`SettingsController`)
* **Pourquoi ?**
  L'appel à `GET /api/Settings` renvoyait en clair le mot de passe du compte de service Active Directory et les identifiants SMTP.
* **Ce qui a été fait :**
  Restriction de l'endpoint aux utilisateurs disposant de `SETTINGS_MANAGE` et masquage systématique des champs sensibles (`AdServicePassword`, `SmtpPass`, `AgentApiKey`) en `"●●●●●●●●"`.

---

### 9. Restriction des Actions de Diagnostic et Scan SNMP
* **Pourquoi ?**
  Les endpoints de test et scan SNMP acceptaient n'importe quel compte authentifié (`[Authorize]` simple).
* **Ce qui a été fait :**
  Verrouillage strict avec `[Authorize(Policy = "SNMP_PROFILE_WRITE")]` pour éviter toute utilisation comme proxy d'interrogation UDP arbitraire.

---

### 10. Protection contre l'Injection de Formules CSV
* **Pourquoi ?**
  Un champ contenant des symboles `=`, `+`, `-`, `@` pouvait être interprété comme une formule à l'ouverture du CSV dans Microsoft Excel.
* **Ce qui a été fait :**
  Dans `ReportGeneratorService.EscapeCsv`, toute valeur commençant par ces caractères est désormais automatiquement préfixée par une apostrophe `'`.

---

### 11. Complétude du Module de Sauvegarde & Restauration
* **Pourquoi ?**
  Les profils SNMP, les règles de planification des rapports et les jetons d'intégration n'étaient pas intégrés à l'export/import.
* **Ce qui a été fait :**
  Mise à niveau de `BackupRootDto` et de la méthode `Restore()` dans `BackupController.cs` pour inclure l'intégralité de ces données.
