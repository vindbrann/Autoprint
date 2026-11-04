# 🛡️ Rapport d'Audit de Sécurité - Autoprint (Novembre 2026)

**Date de l'audit :** 4 Novembre 2026
**Version auditée :** 26.08.24.3 (`master` @ `ddd4b73`)
**Périmètre :** API Serveur, Authentification (local + AD), Console Web Blazor, Client WPF, Service Windows (code résiduel), Installeurs serveur et client, Dépendances NuGet.
**Objectif :** Préparer l'application à un déploiement en entreprise (« blindage »). Cet audit complète celui du 24 août 2026 (`_AUDIT_SECURITE_V2.md`), dont les 5 points sont confirmés corrigés.

> Les alertes GitHub (1 Dependabot, 15 CodeQL) sont rapprochées de ce rapport dans la section « Rapprochement avec les alertes GitHub ».

---

## 📊 Synthèse

**Nature de la correction** (aide à la décision) :
* 🟢 **Transparent** : rien ne change pour les utilisateurs, on peut corriger directement.
* 🟡 **Changement visible** : le comportement change un peu (message, contrainte, réglage en plus). À valider, mais sans remise en cause.
* 🔴 **Fonctionnalité à repenser** : le comportement actuel sert probablement un usage voulu. Corriger « brutalement » le casserait ; il faut d'abord choisir comment le conserver de façon sûre.

| # | Niveau | Vulnérabilité | Composant | Droit requis pour l'exploiter | Ce qui change dans l'appli si on corrige | Nature |
|---|---|---|---|---|---|---|
| 1 | 🔴 Critique | Coercition d'authentification NTLM du serveur (machine en `LocalSystem`) vers un hôte arbitraire | `ImportController` / `WindowsPrintSpoolerService.ScanPrintersAsync` | `PRINTER_READ` (droit le plus faible) | Le bouton « Scanner un serveur d'impression » (`ScanPrintersModal`) ne serait plus accessible aux lecteurs simples, seulement à `PRINTER_WRITE`. Scanner sans saisir d'identifiants, en s'appuyant sur le compte du serveur, ne marcherait plus que vers les serveurs d'une liste autorisée (nouveau paramètre) ; pour les autres, il faudrait saisir un compte. | 🔴 |
| 2 | 🔴 Critique | Dépendances avec vulnérabilités connues (8 avis « High ») | `Autoprint.Server`, `Autoprint.Client` | Aucun / variable | Rien de visible : mises à jour correctives de même version majeure. Il faudra recompiler et redéployer le serveur **et** le MSI client. ImageSharp reste en 2.1.x (la 3.x change de licence). | 🟢 |
| 3 | 🟠 Élevé | Escalade de privilèges : `USER_WRITE` / `ROLE_WRITE` permettent de devenir SuperAdmin | `UsersController`, `RolesController` | `USER_WRITE` ou `ROLE_WRITE` | Un gestionnaire d'utilisateurs non-SuperAdmin ne verrait plus, dans la liste déroulante, que les rôles qui ne dépassent pas ses propres droits. Il ne pourrait plus modifier ni désactiver un compte SuperAdmin, et plus personne ne pourrait modifier son propre rôle. Le SuperAdmin garde tout. Si tu as prévu une délégation du type « le support peut nommer des admins », elle disparaît. | 🟡 |
| 4 | 🟠 Élevé | Changement de mot de passe obligatoire / expiré non appliqué côté serveur | `AuthService`, absence de filtre serveur | Mot de passe expiré ou initial (`admin123`) | Rien pour un utilisateur de la console : elle le redirige déjà vers le changement de mot de passe. Seuls les appels directs à l'API seraient refusés tant que le mot de passe n'est pas changé. Les intégrations (PRTG, jetons `X-Api-Token`) et les postes clients ne sont pas concernés. | 🟢 |
| 5 | 🟠 Élevé | Aucune protection contre le brute-force / password spraying (local et AD) | `AuthController.Login` | Aucun (anonyme) | Après N échecs (ex. 5), message « Trop de tentatives, réessayez dans X minutes ». Risque à arbitrer : verrouiller par **compte** permet à un malveillant de bloquer `admin` exprès. Proposition : limiter par adresse IP plus un court délai par compte, sans verrouillage permanent. Les comptes AD ne sont jamais verrouillés par Autoprint, seulement ralentis. | 🟡 |
| 6 | 🟠 Élevé | Retrait d'un groupe AD ne révoque pas les droits dans Autoprint | `AuthService.ApplyAdGroupMappings` | Ancien membre d'un groupe AD | **Probablement une fonctionnalité :** cette logique permet aussi de donner un rôle **à la main** à un utilisateur AD (via `UserModal`) sans mapping de groupe. Une correction directe lui retirerait ce rôle à la connexion suivante. Il faut repenser en mémorisant l'origine de chaque rôle (AD ou manuel) : on retire seulement les rôles AD devenus invalides et on garde les rôles manuels. Cela demande une colonne en plus dans `UserRoles`. | 🔴 |
| 7 | 🟠 Élevé | Injection WQL dans les noms d'imprimantes → suppression de toutes les files du serveur | `WindowsPrintSpoolerService` | `PRINTER_WRITE` | Effet positif : les noms contenant une apostrophe (« Salle d'attente »), qui font échouer la synchronisation aujourd'hui, fonctionneraient. La validation des caractères ne s'appliquerait qu'aux créations et modifications, sans bloquer les imprimantes existantes. | 🟢 |
| 8 | 🟠 Élevé | Pool IIS exécuté en `LocalSystem` | Installeur serveur | — (amplificateur de toute faille web) | Gros chantier : le site a besoin de droits admin pour piloter le spouleur (création, suppression, mode direct). Il faut séparer l'appli en deux : un site web non privilégié et un petit service Windows privilégié qui exécute seulement des commandes précises. L'installation et la mise à jour changent. À traiter comme une évolution, pas comme un correctif. | 🔴 |
| 9 | 🟡 Moyen | Secrets en clair : base de données, fichier de sauvegarde, `appsettings.json` lisible par les utilisateurs locaux | `ServerSettings`, `BackupController`, installeur | `ADMIN_ACCESS` ou accès local au serveur | **Sauvegarde :** l'export demanderait un mot de passe pour chiffrer les secrets ; sans lui, la restauration se fait sans les mots de passe AD et SMTP, qu'il faut ressaisir. **Base :** les secrets chiffrés sont liés au serveur, donc déplacer la base vers un autre serveur oblige à ressaisir les mots de passe AD et SMTP (la sauvegarde chiffrée reste la voie de migration). **Fichiers :** seuls les administrateurs du serveur pourraient lire ou modifier `appsettings.json`. | 🟡 |
| 10 | 🟡 Moyen | Jeton JWT non révocable (désactivation / retrait de droits effectifs jusqu'à 2 h plus tard), stocké en `localStorage` | `AuthService`, `CustomAuthStateProvider` | Compte désactivé | Désactiver un compte ou changer ses droits prendrait effet presque immédiatement (au lieu de 2 h). Si on raccourcit aussi la session, il faut un renouvellement automatique pour que les admins ne soient pas déconnectés en pleine saisie. La case « Se souvenir de moi », sans effet aujourd'hui, devra être implémentée ou retirée. | 🟡 |
| 11 | 🟡 Moyen | Clé API agent unique, partagée, lisible par tout utilisateur du poste | Client (HKLM + zone technique), `ImprimantesController`, `EmplacementsController` | Utilisateur standard d'un poste | La zone technique du client (5 clics) afficherait la clé masquée. Le technicien pourrait toujours en **saisir** une nouvelle, mais plus lire l'actuelle. Les postes recevraient moins de données : le nom, le partage, le pilote, le lieu et les réseaux restent, car ils servent à détecter le lieu et à installer ; les commentaires, numéros de série et IP disparaissent, car le client ne s'en sert pas. Remplacer la clé par l'authentification Windows (Kerberos) est une évolution à part. | 🟡 |
| 12 | 🟡 Moyen | Absence d'en-têtes de sécurité HTTP (HSTS, CSP, anti-clickjacking) et `AllowedHosts: *` | `Program.cs` | — | Invisible si c'est bien réglé, mais la console Blazor/Radzen doit être retestée entièrement (la CSP peut bloquer un style ou un script). La console ne pourrait plus être affichée **dans un iframe** (portail intranet, SharePoint) : à confirmer si tu le fais. Avec `AllowedHosts` renseigné, la console ne répondrait plus que sous les noms prévus ; l'accès par IP ou par un alias non déclaré serait refusé. | 🟡 |
| 13 | 🟡 Moyen | Politique de mot de passe faible (6 caractères, aucune complexité) | DTOs utilisateurs | — | Les mots de passe actuels restent valables. Les nouveaux devraient faire 12 caractères minimum. `--reset-admin` afficherait un mot de passe aléatoire au lieu de `admin123` : la doc d'installation est à mettre à jour. Comptes locaux uniquement (pas AD). | 🟡 |
| 14 | 🟡 Moyen | Protocoles en clair par défaut : SMTP sans TLS, SNMP v1/v2c uniquement | `EmailService`, `SnmpService` | Écoute réseau | **SMTP :** TLS coché par défaut pour les **nouvelles** installations, avec un avertissement si on le décoche. Les relais internes sans TLS (port 25) restent possibles, donc pas de rupture. **SNMPv3 :** nouvelle fonctionnalité (champs utilisateur, authentification et chiffrement dans les profils), facultative ; v1/v2c restent disponibles pour les imprimantes qui n'ont que ça. | 🟡 |
| 15 | 🟡 Moyen | Fuite de messages d'exception internes vers le client (10 endroits) | Contrôleurs | Variable | Les messages d'erreur de la console seraient moins précis (« Erreur interne, réf. XXXX »), avec le détail dans les logs du serveur. Exception : les boutons de test SMTP/AD (réservés aux admins) garderaient un message de diagnostic utile. | 🟡 |
| 18 | 🟡 Moyen | Les tests AD réutilisent le mot de passe AD enregistré vers un domaine saisi librement (exfiltration du secret masqué) ; filtre de test LDAP non échappé | `SettingsController.TestAd` / `TestAdFilter` | `SETTINGS_MANAGE` | Tester sans ressaisir le mot de passe ne marcherait que sur le domaine enregistré. Pour tester un **autre** domaine, il faudrait taper le mot de passe. Le champ « utilisateur test » ne pourrait plus contenir de caractères spéciaux LDAP (`*`, `(`, `)`) : une recherche avec joker comme `jdu*` ne serait plus interprétée comme telle. | 🟢 |
| 16 | 🔵 Faible | Code du service Windows `SYSTEM` vulnérable (LPE) encore présent dans la solution | `Autoprint.Service` | Utilisateur standard (si réinstallé) | Rien : le service n'est plus déployé depuis la 26.08.24.2. On supprime le projet de la solution, on corrige la doc et on nettoie aussi l'ancien nom `AutoprintService` à l'installation du MSI. | 🟢 |
| 17 | 🔵 Faible | Divers durcissements (voir détail) | Multiples | — | Surtout transparent. Deux effets visibles et positifs : le bouton de purge de l'audit deviendrait réellement utilisable, et une restauration ne laisserait plus les comptes locaux sans mot de passe. | 🟢 |

### ✅ Décisions (4 novembre 2026)

| # | Décision |
|---|---|
| 1 | **Repensé :** le scan ne liste plus que les imprimantes **du serveur local** (celui où Autoprint Server est installé). Plus de champ « hôte cible » ni d'identifiants : la coercition NTLM disparaît complètement. L'import après scan (`ImportPrinters`) est inchangé. Abandonné : le scan d'un *autre* serveur d'impression (migration depuis un ancien serveur). |
| 2 à 8 | Validé tel que décrit. Pour #6 : on mémorise l'origine de chaque rôle (AD / manuel). Pour #8 : à traiter comme une évolution d'architecture. |
| 9 | Validé, **avec un avertissement explicite au moment de l'export** (le fichier contient ou non les secrets, et ce qu'il faudra ressaisir après restauration). |
| 10 | Validé, et **la case « Se souvenir de moi » est retirée** de la page de connexion. |
| 11 | **Option A retenue** (postes hors domaine : l'authentification Windows est exclue). La clé n'est plus affichée en clair dans la zone technique du client ; un champ vide signifie « garder la clé actuelle ». Le test de connexion existant est enrichi pour distinguer : serveur injoignable, certificat HTTPS refusé, adresse incorrecte (404), clé refusée (401), succès. Optionnel : afficher les 4 derniers caractères de la clé (`••••3f9a`) pour la comparer à la console web. Réduction des données renvoyées aux postes et comparaison de la clé en temps constant côté serveur. |
| 12 | **Validé.** Console ouverte directement dans un navigateur (jamais dans un iframe), via un nom DNS du type `autoprint.maboite.fr`, en HTTPS. Donc : anti-iframe activé, HSTS activé, CSP compatible Blazor/Radzen (retester toute la console). `AllowedHosts` = nom public + `localhost`. L'installeur demande le **nom public de la console** et s'en sert pour remplir `AllowedHosts` et `ClientUrl` (aujourd'hui `http://localhost:port`). ⚠️ Les postes clients (`PRINTSERVER`) doivent utiliser ce même nom, pas une IP ni un alias non déclaré. |
| 13 à 18 | Validé tel que décrit. |

---

## 🔍 Analyse Détaillée

### 🔴 1. Coercition NTLM via le scan d'imprimantes distant

* **Localisation :** `Autoprint.Server/Controllers/ImportController.cs` (`POST /api/Import/Scan/Printers`), `Services/WindowsPrintSpoolerService.cs` (`ScanPrintersAsync`).
* **CWE :** CWE-918 (SSRF) / CWE-294 (Authentication Bypass by Capture-replay)
* **Constat :** L'endpoint n'exige que `PRINTER_READ`. Il ouvre une connexion WMI/DCOM vers `\\{TargetHost}\root\cimv2`, `TargetHost` étant fourni librement par l'appelant. Si aucun identifiant n'est fourni, le serveur s'authentifie avec **le compte machine du serveur d'impression** (le pool IIS tourne en `LocalSystem`).
* **Impact :** Un utilisateur en lecture seule fait s'authentifier le serveur sur une machine qu'il contrôle. C'est la technique classique de coercition + relais NTLM (vers LDAP, AD CS, SMB) qui peut mener à la compromission du serveur, voire du domaine.
* **Correction :**
  1. Exiger `PRINTER_WRITE` (ou `NETWORK_SCAN`) sur cet endpoint.
  2. Valider `TargetHost` (nom d'hôte ou IP stricts, pas de `\`, `/`, espaces).
  3. Exiger des identifiants explicites, ou restreindre la cible à une liste blanche de serveurs d'impression déclarés.

### 🔴 2. Dépendances vulnérables

Résultat de `dotnet list package --vulnerable --include-transitive` :

| Projet | Package | Version | Avis |
|---|---|---|---|
| Server | `Microsoft.AspNetCore.Components.WebAssembly.Server` | 10.0.0 | 2 × High (GHSA-g27p-qf6j-c96c, GHSA-qjpx-jqm5-h2c4) |
| Server | `SixLabors.ImageSharp` | 2.1.11 | 3 × High, 2 × Moderate |
| Server | `System.Security.Cryptography.Pkcs` (transitif) | 9.0.10 | 1 × High, 1 × Moderate |
| Client | `Microsoft.AspNetCore.SignalR.Common` (transitif) | 10.0.0 | 1 × High (GHSA-334f-vvp4-7g6p) |

* **Correction :** Mettre à jour tous les packages `Microsoft.*` en 10.0.x (dernier correctif), la ligne 2.1.x patchée d'`ImageSharp`, et épingler une version corrigée de `System.Security.Cryptography.Pkcs`.
  ⚠️ `ImageSharp` 3.x passe sous licence *Six Labors Split License* (licence commerciale payante au-delà d'un certain chiffre d'affaires) : rester en 2.1.x patchée ou remplacer la bibliothèque.
* **À mettre en place :** Activer Dependabot (mises à jour automatiques) et ajouter `dotnet list package --vulnerable` en CI.

### 🟠 3. Escalade de privilèges par la gestion des utilisateurs et des rôles

* **Localisation :** `UsersController.CreateUser` / `UpdateUser`, `RolesController.UpdateRole`.
* **CWE :** CWE-269 (Improper Privilege Management)
* **Constat :**
  * Un détenteur de `USER_WRITE` peut affecter **n'importe quel rôle** (y compris `SuperAdmin`, Id 1) à lui-même ou à un autre compte, réinitialiser le mot de passe de `admin`, ou le désactiver.
  * Un détenteur de `ROLE_WRITE` peut ajouter **toutes les permissions** à son propre rôle, ou modifier le rôle `SuperAdmin`.
* **Impact :** Un gestionnaire d'utilisateurs « délégué » devient administrateur total.
* **Correction :**
  1. Interdire d'attribuer un rôle contenant une permission que l'appelant ne possède pas.
  2. Interdire à un appelant non-SuperAdmin de modifier un compte SuperAdmin ou le rôle SuperAdmin.
  3. Interdire la modification de son propre rôle.

### 🟠 4. Changement de mot de passe obligatoire non appliqué côté serveur

* **Localisation :** `AuthService.GenerateJwtToken` (claim `ForcePasswordChange`), `Autoprint.Web/Layout/MainLayout.razor`.
* **CWE :** CWE-602 (Client-Side Enforcement of Server-Side Security)
* **Constat :** Un utilisateur au mot de passe expiré ou devant le changer reçoit un JWT contenant **toutes ses permissions**. Seule l'interface Blazor le redirige vers le changement de mot de passe ; l'API, elle, accepte le jeton normalement.
* **Impact :** Sur une installation neuve, quiconque connaît `admin` / `admin123` (documenté) obtient un accès API complet sans jamais changer le mot de passe. La politique d'expiration est sans effet contre un attaquant.
* **Correction :** Lorsque le mot de passe doit être changé, émettre un jeton sans permissions (ou un filtre global qui refuse tout sauf `change-password` et `profile` si le claim est présent). Idéalement, faire choisir le mot de passe admin à l'installeur.

### 🟠 5. Aucune protection contre le brute-force

* **Localisation :** `AuthController.Login`, `Program.cs`.
* **CWE :** CWE-307 (Improper Restriction of Excessive Authentication Attempts)
* **Constat :** Ni limitation de débit, ni verrouillage de compte, ni délai progressif. Pour les comptes AD, l'application sert de relais de *password spraying* contre l'annuaire (et peut verrouiller les comptes AD des collaborateurs).
* **Correction :**
  1. `AddRateLimiter` (ASP.NET Core) sur `/api/auth/login` par IP.
  2. Verrouillage temporaire après N échecs par nom d'utilisateur (comptes locaux), journalisé dans l'audit.
  3. Message d'erreur identique quel que soit le motif.

### 🟠 6. Les droits AD ne sont jamais révoqués

* **Localisation :** `AuthService.ApplyAdGroupMappings`.
* **CWE :** CWE-284 (Improper Access Control)
* **Constat :** Les rôles d'un utilisateur AD ne sont remplacés **que si** au moins un mapping correspond (`if (applicableMappings.Any())`). Un utilisateur retiré de tous les groupes AD autorisés **conserve ses anciens rôles** indéfiniment.
* **Impact :** Le processus RH/IT habituel (retirer quelqu'un du groupe « Admins Impression ») ne coupe pas l'accès.
* **Correction :** Si aucun mapping ne correspond, supprimer les rôles hérités de l'AD (et refuser la connexion). Distinguer rôles manuels et rôles issus de l'AD si nécessaire.

### 🟠 7. Injection WQL dans le pilotage du spouleur

* **Localisation :** `WindowsPrintSpoolerService.PortExistsNative`, `SupprimerImprimante`, `RenommerImprimante`.
* **CWE :** CWE-943 (Improper Neutralization of Special Elements in Data Query Logic)
* **Constat :** Les requêtes WMI sont construites par concaténation : `$"Name = '{nom}'"`. Le nom provient de la saisie d'un utilisateur `PRINTER_WRITE`.
* **Impact :** Une imprimante nommée `x' OR Name LIKE '%` fait correspondre **toutes les files d'impression du serveur** ; `SupprimerImprimante` les supprime alors une par une (`foreach … Delete()`). Une apostrophe légitime (« Salle d'attente ») casse aussi la synchronisation.
* **Correction :** Échapper `\` et `'` pour WQL, et valider côté serveur les noms d'imprimantes et de partage (liste de caractères autorisés, longueur maximale).

### 🟠 8. Pool IIS en `LocalSystem`

* **Localisation :** `Autoprint.Installer.Server.UI/MainWindow.xaml.cs` (`ProcessModelIdentityType.LocalSystem`).
* **Constat :** Déjà listé en dette technique. Toute faille dans l'application web (y compris dans une dépendance) donne l'exécution de code en **SYSTEM** sur le serveur d'impression, et le compte machine sert d'identité réseau (cf. point 1).
* **Correction (architecture) :** Faire tourner le site sous un compte de service dédié (gMSA idéalement) à privilèges réduits, et isoler les opérations spouleur privilégiées dans un service Windows séparé exposant une API locale minimale (commandes typées, pas de chaînes libres).

### 🟡 9. Secrets stockés en clair

* **Constat :**
  * `AdServicePassword`, `SmtpPass`, `AgentApiKey` sont stockés en clair dans `ServerSettings`.
  * L'export de sauvegarde (`GET /api/Backup/export`) contient **tous les paramètres en clair**, ce qui contourne le masquage ajouté en août. Le fichier JSON téléchargé circule ensuite sur des postes et des partages.
  * `C:\Program Files\Autoprint Server\appsettings.json` (clé JWT, chaîne SQL) et la base SQLite éventuelle héritent des ACL de *Program Files* : **lisibles par tout utilisateur local du serveur**. Avec la clé JWT, on peut forger un jeton SuperAdmin.
* **Correction :**
  1. Chiffrer les secrets en base (ASP.NET Data Protection avec clés protégées par DPAPI machine).
  2. Exclure ou chiffrer les secrets dans l'export de sauvegarde (mot de passe de chiffrement saisi à l'export).
  3. Restreindre par l'installeur les ACL de `appsettings.json` et du fichier `.db` à `SYSTEM` + `Administrateurs` + l'identité du pool.

### 🟡 10. Sessions JWT non révocables

* **Constat :** Durée 120 min, aucune liste de révocation, pas de vérification de `IsActive` à chaque requête. Un compte désactivé ou dont les droits ont été retirés reste opérationnel jusqu'à expiration. Le jeton est stocké en `localStorage` (exposé en cas de XSS). Le paramètre `RememberMe` est ignoré.
* **Correction :** Ajouter un `SecurityStamp` par utilisateur, inclus dans le jeton et vérifié (avec `IsActive`) dans `OnTokenValidated` (cache court). Réduire la durée de vie à 15–30 min avec renouvellement.

### 🟡 11. Clé API agent unique et exposée

* **Constat :** Une seule clé pour tout le parc, stockée dans `HKLM\SOFTWARE\Autoprint` (lisible par tous les utilisateurs) et affichée dans la « zone technique » du client (5 clics, sans mot de passe). Elle donne accès à l'inventaire complet (imprimantes, IP, réseaux des sites, commentaires). La comparaison n'est pas en temps constant.
* **Correction :** Considérer la clé comme publique côté poste et limiter ce qu'elle expose (DTO réduit : nom, partage, lieu ; pas de commentaires, numéros de série, pilotes). Comparaison `CryptographicOperations.FixedTimeEquals`. À terme : authentification des postes par Kerberos (Windows Authentication) au lieu d'une clé partagée.

### 🟡 12. En-têtes de sécurité HTTP absents

* **Constat :** Pas de `UseHsts`, ni de `Content-Security-Policy`, `X-Frame-Options`/`frame-ancestors`, `X-Content-Type-Options`, `Referrer-Policy`. `AllowedHosts` vaut `*`.
* **Correction :** Middleware d'en-têtes (CSP compatible Blazor WebAssembly : `script-src 'self' 'wasm-unsafe-eval'`), `UseHsts()` hors développement, `AllowedHosts` renseigné par l'installeur.

### 🟡 13. Politique de mot de passe faible

* **Constat :** 6 caractères minimum, aucune complexité, réutilisation possible. `--reset-admin` remet `admin123`.
* **Correction :** 12 caractères minimum (recommandation ANSSI/NIST), refus du mot de passe actuel et du nom d'utilisateur, validation côté serveur. `--reset-admin` doit générer un mot de passe aléatoire affiché une seule fois.

### 🟡 14. Protocoles en clair

* **Constat :** `SmtpEnableSsl` vaut `false` par défaut (le mot de passe SMTP circule en clair). SNMP limité à v1/v2c (communauté en clair, `public` par défaut).
* **Correction :** TLS activé par défaut et avertissement dans l'interface si désactivé. SNMPv3 (authPriv) à prévoir en évolution.

### 🟡 15. Fuite d'informations dans les erreurs

* **Constat :** 10 contrôleurs renvoient `ex.Message` au client (ex. `AuthController` : `"Erreur serveur : " + ex.Message`, `BackupController`, `DiscoveryController`, tests LDAP/SMTP).
* **Correction :** Message générique + identifiant de corrélation ; détail uniquement dans les logs serveur. (Les tests LDAP/SMTP réservés à `SETTINGS_MANAGE` peuvent garder un message utile mais filtré.)

### 🟡 18. Tests Active Directory : réutilisation du secret enregistré vers un domaine arbitraire

* **Localisation :** `Autoprint.Server/Controllers/SettingsController.cs` (`TestAd`, `TestAdFilter`).
* **CWE :** CWE-522 (Insufficiently Protected Credentials) / CWE-90 (LDAP Injection)
* **Constat :**
  * Si le mot de passe est laissé vide, les deux tests utilisent **le mot de passe AD enregistré** mais contre le `Domain` / `BaseDn` **fournis dans la requête**. Un détenteur de `SETTINGS_MANAGE` qui pointe `Domain` vers un serveur qu'il contrôle récupère une authentification Negotiate/NTLM du compte de service AD (à casser ou relayer). Cela contourne le masquage du mot de passe.
  * Sans compte de service, la connexion part avec le compte machine du serveur (même coercition qu'au point 1).
  * `TestUserQuery` est inséré sans échappement dans le filtre LDAP (alertes CodeQL #8 à #11).
* **Correction :** N'utiliser le mot de passe enregistré qu'avec le domaine enregistré (refuser si `Domain` diffère) ; échapper `TestUserQuery` avec `EscapeLdapFilter` ; valider `Domain` (nom DNS) et la syntaxe de `BaseDn`.

### 🔵 16. Code résiduel du service Windows `SYSTEM`

* **Constat :** Le projet `Autoprint.Service` n'est plus déployé (retiré en 26.08.24.2), mais il est toujours dans la solution. Son tube nommé est ouvert à tous les utilisateurs authentifiés et permet d'injecter des arguments dans `printui.dll` exécuté en SYSTEM et de réécrire `HKLM\SOFTWARE\Autoprint` : une élévation de privilèges triviale s'il était réinstallé. `PROJECT_CONTEXT.md` et `PROJECT_MAP.md` le décrivent encore comme actif. Le MSI ne nettoie que l'ancien nom de service `AutoprintWorker`, pas `AutoprintService`.
* **Correction :** Supprimer le projet de la solution, mettre la documentation à jour, ajouter `AutoprintService` au nettoyage du MSI.

### 🔵 17. Durcissements divers

| Constat | Localisation | Correction |
|---|---|---|
| `rundll32.exe` lancé sans chemin absolu (risque de détournement de binaire) | `Autoprint.Client/ViewModels/MainWindowViewModel.cs` (`RunUserCommand`) | Utiliser `System32\rundll32.exe` et `ArgumentList`, comme dans `ManagePrintersViewModel` |
| Anciens hachages SHA-256 non salés encore acceptés | `SecurityHelper.VerifyPassword` | Forcer la réinitialisation des comptes concernés, puis supprimer ce chemin |
| DN utilisateur injecté sans échappement dans le filtre LDAP de groupes | `AuthService.ApplyAdGroupMappings` | Échapper avec `EscapeLdapFilter` |
| `CreateUser` renvoie l'entité avec `PasswordHash` | `UsersController.CreateUser` | Renvoyer un DTO |
| Le nettoyage d'audit exige le *rôle* `SuperAdmin`, jamais émis dans le jeton → endpoint inaccessible | `AuditController.Cleanup` | Utiliser une policy de permission |
| La restauration recrée les utilisateurs **sans mot de passe** (comptes locaux inutilisables) | `BackupController.Restore` | Sauvegarder les hachages (chiffrés) ou forcer une réinitialisation |
| Journalisation de la clé API en clair (`Debug.WriteLine`) | `Autoprint.Client/Services/ConfigurationService.cs` | Supprimer |
| Masquage empêchant de copier la clé API agent depuis la console | `Parametres.razor` / `SettingsController` | Endpoint dédié « révéler la clé » audité |

---

## 🔗 Rapprochement avec les alertes GitHub

| Alerte GitHub | Fichier | Verdict | Point du rapport |
|---|---|---|---|
| Dependabot #7 — CVE-2026-96230, RCE ASP.NET Core (High) | `Microsoft.AspNetCore.Components.WebAssembly.Server` | ✅ Réelle | #2 |
| CodeQL #8, #9, #10, #11 — LDAP query from user input (Critical) | `SettingsController.cs:239-248` | ⚠️ Réelle mais réservée à `SETTINGS_MANAGE` (`TestUserQuery` non échappé, domaine libre) | #18 |
| CodeQL #15 — LDAP query (Critical) | `AuthService.cs:204` | ⚠️ Partielle : la saisie utilisateur est échappée ; seul le filtre global `AdLdapFilter`, défini par un administrateur, est concaténé tel quel. Valider sa syntaxe à l'enregistrement. | #17 |
| CodeQL #12, #13, #14 — LDAP query (Critical) | `AuthService.cs:78, 257, 334` | ❌ Faux positif : valeurs échappées par `SecurityHelper.EscapeLdapFilter` (conforme RFC 4515), que CodeQL ne reconnaît pas comme assainisseur. À fermer en « False positive » après relecture. | — |
| CodeQL #1 à #6 — Log entries from user input (Medium) | `SnmpProfilesController.cs:61-101` | 🔵 Faible : journalisation structurée (paramètres `{Ip}`), mais l'IP n'est validée qu'après le log. Valider l'entrée avant de la journaliser. | #17 |
| CodeQL #7 — Log entries from user input (Medium) | `DiscoveryService.cs:36` | 🔵 Faible : interpolation `$"…{cidr}"` dans le log → passer en paramètre structuré, valider le CIDR avant. | #17 |

> CodeQL n'a pas détecté les failles de logique d'autorisation (#1, #3, #4, #5, #6), l'injection WQL (#7), ni les problèmes d'architecture (#8, #9) : ces points ne sont visibles qu'à la revue manuelle.

---

> Les anomalies et améliorations hors sécurité repérées pendant les travaux sont suivies dans `_BACKLOG_AMELIORATIONS.md`. La procédure de recette est dans `_PROCEDURE_TESTS_SECURITE.md`.

---

## ✅ Avancement de la phase 1 (branche `security/phase-1`)

| # | État | Vérification |
|---|---|---|
| 2 | ✅ Fait : packages `Microsoft.*`/`System.*` en 10.0.14, `System.Security.Cryptography.Pkcs` épinglé en 10.0.14, `System.Management.Automation` (inutilisé) retiré. ImageSharp n'a **aucune** version 2.x corrigée : **PdfSharpCore (abandonné) remplacé par PDFsharp 6.2.4 (MIT)**, qui n'en dépend pas. | `dotnet list package --vulnerable` : 0 package vulnérable. PDF de test généré avec accents. |
| 1 | ✅ Fait : scan limité au spouleur local, droit `PRINTER_WRITE`, champs hôte/compte retirés de la fenêtre de scan. | Appel API : liste les imprimantes locales. |
| 7 | ✅ Fait : échappement WQL + validation des noms (création/modification uniquement). | Nom avec `\` refusé, « Salle d'attente » accepté. |
| 3 | ✅ Fait : `PermissionGuard` sur utilisateurs, rôles et liens AD ; liste déroulante des rôles filtrée dans la console. | Compte Helpdesk : création SuperAdmin, reset du mdp admin et ajout de permissions refusés (403) ; actions dans son périmètre acceptées. |
| 4 | ✅ Fait : jeton « mot de passe à changer » limité au changement de mot de passe et au profil. | `admin/admin123` : `/api/users` → 403, changement de mot de passe → 204. |
| 5 | ✅ Fait : 10 tentatives/min par IP + 2 min de pause par compte après 5 échecs. | 5 × 401 puis 429. |
| 6 | ✅ Fait : colonne `UserRoles.IsFromAd` ; rôles AD recalculés à chaque connexion, rôles manuels conservés ; DN échappé dans le filtre de groupes. | Colonne créée au démarrage. **Non testé contre un vrai AD.** |
| 18 | ✅ Fait : secret enregistré limité au domaine enregistré, `TestUserQuery` échappé, syntaxe des filtres LDAP vérifiée (test et enregistrement). | Compilé. **Non testé contre un vrai AD.** |

---

## 📋 Plan de Remédiation Recommandé

1. **Phase 1 — Immédiat (corrections ciblées, faible risque de régression)**
   * Mise à jour des dépendances vulnérables (#2).
   * Scan distant : permission + validation de l'hôte (#1).
   * Échappement WQL + validation des noms (#7).
   * Garde-fous d'escalade de privilèges utilisateurs/rôles (#3).
   * Application serveur du changement de mot de passe obligatoire (#4).
   * Limitation de débit et verrouillage sur la connexion (#5).
   * Révocation des rôles AD (#6).
   * Tests AD : secret lié au domaine enregistré, échappement LDAP (#18) — ferme les alertes CodeQL #8 à #11.
2. **Phase 2 — Durcissement**
   * En-têtes HTTP, HSTS, `AllowedHosts` (#12).
   * Politique de mot de passe et `--reset-admin` aléatoire (#13).
   * Messages d'erreur génériques (#15).
   * Révocation des jetons / `SecurityStamp` (#10).
   * Réduction des données exposées par la clé agent, comparaison en temps constant (#11).
   * Points 16 et 17.
3. **Phase 3 — Architecture & exploitation**
   * Chiffrement des secrets (base + sauvegardes) et ACL de l'installeur (#9).
   * Sortie de `LocalSystem` pour le pool IIS (#8).
   * TLS SMTP par défaut, SNMPv3 (#14).
   * CI : CodeQL, Dependabot, `dotnet list package --vulnerable`.
