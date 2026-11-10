# 🧪 Procédure de Recette — Durcissement Sécurité

Procédure à dérouler **une seule fois, à la fin de toutes les phases**, sur une installation complète. Elle est mise à jour à chaque modification ; chaque test indique la phase et le point de l'audit (`_AUDIT_SECURITE_2026-11.md`) qu'il couvre.

**Dernière mise à jour :** 04/11/2026 — Phase 3

---

## 0. Préparation de l'environnement

### 0.1 Matériel et comptes

> **Labo prêt à l'emploi :** voir `tools/lab/README.md` : 3 VM Hyper-V en français (contrôleur de domaine, serveur membre vierge, poste Windows 11 hors domaine comme sous Intune) configurées par `tools/lab/Configure-Lab.ps1`, avec une fausse imprimante qui capture les travaux.

| Élément | Détail |
|---|---|
| Serveur | Windows Server avec IIS, SQL Server (SQLite neuf : backlog B1), Autoprint Server installé via l'installeur, site en **HTTPS** avec le nom public (ex. `autoprint.maboite.fr`). |
| Poste client | Windows 10/11 **hors domaine**, client installé via le MSI (`PRINTSERVER` + `APIKEY`). |
| Active Directory | Un domaine de test avec : un compte de service (lecture annuaire), un groupe `GG_Autoprint_Admins`, un groupe `GG_Autoprint_Lecteurs`, deux utilisateurs `ad.user1` (membre de `GG_Autoprint_Lecteurs`) et `ad.user2` (aucun groupe). |
| Navigateur | Un navigateur récent avec les outils de développement (F12) pour lire les codes HTTP. |
| Outil HTTP (optionnel) | `curl` ou Postman, pour les tests d'API directs (marqués **[API]**). |

### 0.2 Comptes Autoprint à créer pendant la recette

| Compte | Rôle | Droits du rôle |
|---|---|---|
| `admin` | SuperAdmin | Tous (compte d'origine) |
| `helpdesk` | `Helpdesk` (à créer) | `USER_READ`, `USER_WRITE`, `ROLE_READ`, `ROLE_WRITE` uniquement |
| `lecteur` | `Lecteur` (à créer) | `PRINTER_READ` uniquement |

### 0.3 Obtenir un jeton pour les tests [API]

```bash
curl -s -X POST https://autoprint.maboite.fr/api/auth/login -H "Content-Type: application/json" -d "{\"username\":\"admin\",\"password\":\"<mot de passe>\"}"
```

Le champ `token` de la réponse s'utilise ensuite avec l'en-tête `Authorization: Bearer <token>`.

---

## 1. Non-régression générale (à faire en premier)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| NR-01 | Installation serveur neuve (SQL Server) via l'installeur. | Installation sans erreur, console accessible, connexion `admin` possible. | ☐ |
| NR-02 | Mise à jour d'un serveur existant (version 26.08.24.3 → nouvelle version). | Données conservées (imprimantes, lieux, utilisateurs, rôles, profils SNMP, rapports). | ☐ |
| NR-03 | Parcourir toutes les pages de la console (tableau de bord, imprimantes, lieux, marques, modèles, pilotes, profils SNMP, rapports, utilisateurs, rôles, audit, paramètres). | Aucune page vide ni erreur. | ☐ |
| NR-04 | Créer / modifier / archiver / supprimer une imprimante, puis synchroniser avec le spouleur. | La file Windows est créée / modifiée / supprimée en conséquence. | ☐ |
| NR-05 | Installer le client MSI sur un poste, ouvrir le client. | Le lieu est détecté, les imprimantes du lieu sont listées et installables. | ☐ |
| NR-06 | Générer un rapport planifié au format **PDF** (bouton « Envoyer maintenant »). | E-mail reçu, PDF lisible : bandeau bleu, titres, tableau, accents corrects. *(Bibliothèque PDF remplacée en phase 1.)* | ☐ |
| NR-07 | Générer le même rapport au format **CSV**. | CSV lisible dans Excel. | ☐ |
| NR-08 | Exporter une sauvegarde puis la restaurer. | Restauration sans erreur. | ☐ |
| NR-09 | Export supervision API (`/api/v1/Monitoring/export` avec un jeton d'intégration). | Réponse JSON correcte. | ☐ |
| NR-10 | **SQL Server** : après une installation neuve **et** après la mise à jour d'un serveur existant, créer un rapport planifié hebdomadaire (heure, minute, jour), ouvrir Profils SNMP, exporter puis restaurer une sauvegarde. | Aucune erreur « Invalid column name » ; l'heure, le jour et le seuil de prévision du rapport sont conservés (correctif B22). | ☐ |

---

## 2. Phase 1

### 2.1 Dépendances — audit #2

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-01 | Sur le poste de build : `dotnet list Autoprint.sln package --vulnerable --include-transitive`. | « aucun package vulnérable » pour tous les projets. | ☐ |
| P1-02 | GitHub › Security › Dependabot après push de la branche. | L'alerte CVE-2026-96230 passe à l'état fermé. | ☐ |

### 2.2 Scan du spouleur local — audit #1

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-03 | Imprimantes › Scanner › onglet « Ce serveur (Spouleur Windows) ». | Plus de champ « Serveur cible », « Utilisateur », « Mot de passe » ; un seul bouton « Lister les imprimantes de ce serveur ». | ☐ |
| P1-04 | Cliquer sur le bouton (compte `admin`). | Les files d'impression du serveur Autoprint s'affichent ; celles déjà connues sont signalées. | ☐ |
| P1-05 | Importer deux imprimantes de la liste. | Elles apparaissent dans la liste des imprimantes. | ☐ |
| P1-06 | **[API]** Avec le jeton de `lecteur` : `POST /api/Import/Scan/Printers`. | **403** (le scan exige désormais `PRINTER_WRITE`). | ☐ |

### 2.3 Noms d'imprimantes — audit #7

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-07 | Créer une imprimante nommée `Salle d'attente`, puis synchroniser. | Création et synchronisation OK (avant : échec à cause de l'apostrophe). | ☐ |
| P1-08 | Renommer cette imprimante en `Salle d'attente 2`, synchroniser ; puis la supprimer, synchroniser. | La file Windows est renommée puis supprimée ; **les autres files du serveur sont intactes**. | ☐ |
| P1-09 | Créer une imprimante nommée `Test' OR Name LIKE '%`, synchroniser, puis la supprimer et synchroniser. | Seule cette file est créée puis supprimée ; **aucune autre file n'est supprimée**. ⚠️ Faire ce test sur un serveur de test. | ☐ |
| P1-10 | Créer une imprimante nommée `Salle\A` ou `Salle,A`. | Refus : « Nom d'imprimante invalide ». | ☐ |
| P1-11 | Créer une imprimante avec le nom de partage `Partage:1`. | Refus : « Nom de partage invalide ». | ☐ |
| P1-12 | Modifier uniquement le commentaire d'une imprimante existante dont le nom contient un caractère désormais interdit (si existante). | Enregistrement accepté (la validation ne porte que sur les noms modifiés). | ☐ |

### 2.4 Escalade de privilèges — audit #3

Préparer le rôle `Helpdesk` et le compte `helpdesk` avec `admin`, puis se connecter en `helpdesk`.

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-13 | Utilisateurs › Créer : ouvrir la liste déroulante des rôles. | Tous les rôles sont proposés **sauf** ceux qui ont « Administration » ou « Paramètres » (Helpdesk a « Rôles - écriture », B50). | ☐ |
| P1-14 | Créer un utilisateur avec le rôle `Helpdesk`. | Création OK. | ☐ |
| P1-15 | Modifier le compte `admin` (nom, mot de passe ou statut). | Refus : « Ce compte a « Administration » ou « Paramètres » : vous ne pouvez pas le modifier. » | ☐ |
| P1-16 | Supprimer un compte SuperAdmin (autre que `admin`). | Refus. | ☐ |
| P1-17 | Rôles › modifier `Helpdesk` en lui ajoutant `SETTINGS_MANAGE` (ligne « Système »). | Case grisée ; **[API]** refus 403 « Vous ne pouvez pas accorder ces droits… ». | ☐ |
| P1-18 | Rôles › modifier ou supprimer `SuperAdmin`. | Refus. | ☐ |
| P1-19 | Rôles › créer un rôle avec `PRINTER_DELETE`, puis un rôle avec « Administration ». | Le premier est accepté (« Rôles - écriture » permet d'accorder tout sauf Administration et Paramètres, B50) ; pour le second, la case est grisée et l'API répond 403. | ☐ |
| P1-20 | Rôles › lier un groupe AD au rôle `SuperAdmin`. | Refus. | ☐ |
| P1-21 | **[API]** Avec le jeton `helpdesk` : `POST /api/users` avec `"roleId": 1`. | **403**. | ☐ |
| P1-22 | Reconnecté en `admin` : refaire P1-15 à P1-20. | Tout est autorisé. | ☐ |

### 2.5 Changement de mot de passe obligatoire — audit #4

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-23 | Sur une installation neuve, se connecter en `admin` / `admin123`. | La console impose le changement de mot de passe. | ☐ |
| P1-24 | **[API]** Avant de changer : avec ce jeton, `GET /api/users` puis `GET /api/imprimantes`. | **403** « Changement de mot de passe obligatoire » pour les deux. | ☐ |
| P1-25 | **[API]** Avec ce même jeton : `GET /api/users/profile`. | **200**. | ☐ |
| P1-26 | Changer le mot de passe dans la console. | Déconnexion automatique, reconnexion avec le nouveau mot de passe, accès complet. | ☐ |
| P1-27 | Pour un utilisateur local, cocher « Obliger le changement de mot de passe », puis se connecter avec ce compte. | Même comportement que P1-23 à P1-26. | ☐ |
| P1-28 | Avec un compte local dont le mot de passe est expiré (réduire « Expiration des mots de passe » dans les paramètres). | Même comportement. | ☐ |

### 2.6 Limitation des tentatives de connexion — audit #5

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-29 | Saisir 5 fois un mauvais mot de passe pour `lecteur`. | 5 × « Identifiants incorrects ». | ☐ |
| P1-30 | 6ᵉ tentative, même avec le **bon** mot de passe. | « Trop de tentatives pour ce compte. Réessayez dans 2 minute(s). » | ☐ |
| P1-31 | Attendre 2 minutes, se connecter avec le bon mot de passe. | Connexion OK. | ☐ |
| P1-32 | Depuis un même poste, enchaîner plus de 10 tentatives en moins d'une minute (comptes différents). | À partir de la 11ᵉ : « Trop de tentatives de connexion. Réessayez dans une minute. » | ☐ |
| P1-33 | Pendant le blocage de P1-30, se connecter en `admin` depuis un autre poste. | Connexion OK (le blocage est par compte). | ☐ |
| P1-34 | Domaine avec un seuil de verrouillage de 5 : 5 mauvais mots de passe sur un compte **AD**. | 3 × « Identifiants incorrects », puis « Trop de tentatives… Réessayez dans 30 minute(s) » sans interroger l'AD ; **dans l'AD, le compte n'est pas verrouillé** (`badPwdCount` = 3) (B53). | ☐ |

### 2.7 Rôles issus de l'AD — audit #6

Préparer : dans Autoprint, lier `GG_Autoprint_Lecteurs` au rôle `Lecteur`.

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-35 | Se connecter en `ad.user1`. | Connexion OK avec le rôle `Lecteur`. | ☐ |
| P1-36 | Retirer `ad.user1` de `GG_Autoprint_Lecteurs` dans l'AD (attendre la réplication), se reconnecter. | Refus : « aucun droit d'accès » (avant : le rôle restait). | ☐ |
| P1-37 | Donner **manuellement** le rôle `Lecteur` à `ad.user2` (Utilisateurs › modifier), puis se connecter en `ad.user2`. | Connexion OK ; le rôle manuel est **conservé** à chaque connexion. | ☐ |
| P1-38 | Ajouter `ad.user2` à `GG_Autoprint_Admins` (lié à un rôle admin), se reconnecter ; puis l'en retirer, se reconnecter. | Le rôle admin apparaît puis disparaît ; le rôle `Lecteur` manuel reste. | ☐ |
| P1-39 | **Mise à jour d'un serveur existant** ayant déjà des utilisateurs AD : se connecter avec un utilisateur AD qui a quitté son groupe avant la mise à jour. | Son ancien rôle AD est retiré. | ☐ |

### 2.8 Tests Active Directory — audit #18

Préparer : paramètres AD enregistrés (domaine, compte de service, mot de passe).

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P1-40 | Paramètres › AD › « Tester la connexion » sans ressaisir le mot de passe. | Succès (domaine enregistré). | ☐ |
| P1-41 | Remplacer le domaine par `autre.domaine.local` sans saisir de mot de passe, tester. | Refus : « Pour tester un autre domaine que celui enregistré, saisissez le mot de passe du compte de service ». | ☐ |
| P1-42 | Même chose en **saisissant** le mot de passe. | Le test est tenté (succès ou échec selon le domaine). | ☐ |
| P1-43 | Test du filtre avec l'utilisateur test `ad.user1`. | « Objet trouvé ». | ☐ |
| P1-44 | Test du filtre avec l'utilisateur test `*`. | « Aucun objet trouvé » (le `*` n'est plus interprété comme joker). | ☐ |
| P1-45 | Enregistrer un filtre LDAP global mal formé, ex. `(objectClass=user`. | Refus : « Filtre LDAP invalide ». | ☐ |
| P1-46 | Enregistrer un filtre valide, ex. `(memberOf=CN=GG_Autoprint_Lecteurs,OU=Groupes,DC=maboite,DC=fr)`. | Accepté ; la recherche AD (liaison de groupe) fonctionne. | ☐ |

---

## 3. Phase 2

### 3.1 En-têtes HTTP et nom d'accès — audit #12

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P2-01 | **Installation neuve** : à l'étape « Configuration Web », laisser « Nom public de la console » vide, puis saisir `https://autoprint` ou `autoprint:443`. | Refus « Nom public de la console invalide ». | ☐ |
| P2-02 | Saisir `autoprint.maboite.fr`, terminer l'installation, ouvrir `appsettings.json`. | `"AllowedHosts": "autoprint.maboite.fr;localhost"`. | ☐ |
| P2-03 | Ouvrir `https://autoprint.maboite.fr`. | Console fonctionnelle. | ☐ |
| P2-04 | Ouvrir la console par l'adresse IP du serveur (`https://10.x.x.x`) ou un autre alias DNS. | Page « Bad Request - Invalid Hostname » (400). | ☐ |
| P2-05 | Ouvrir `http://localhost:<port>` depuis le serveur. | Console fonctionnelle. | ☐ |
| P2-06 | F12 › Réseau › document principal › en-têtes de réponse. | Présents : `Strict-Transport-Security` (en HTTPS), `Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`. | ☐ |
| P2-07 | **Parcourir toutes les pages** de la console (cf. NR-03), avec F12 › Console ouverte. | Aucune erreur « Refused to load / execute … Content Security Policy ». Seule erreur connue tolérée : 404 sur `css/-base.css` (backlog B20). | ☐ |
| P2-08 | Tableau de bord avec au moins deux modèles d'imprimantes. | Le graphique en anneau s'affiche (Chart.js chargé avec contrôle d'intégrité). | ☐ |
| P2-09 | Exporter un rapport ou une sauvegarde depuis la console. | Le fichier se télécharge (fonction JS déplacée dans `js/app.js`). | ☐ |
| P2-10 | Créer une page HTML locale contenant `<iframe src="https://autoprint.maboite.fr">` et l'ouvrir. | La console ne s'affiche pas dans l'iframe. | ☐ |
| P2-11 | **Mise à jour d'un serveur existant** : vérifier `appsettings.json`. | `AllowedHosts` est resté `*` (backlog B17) : le renseigner à la main, redémarrer le site, refaire P2-03 à P2-05. | ☐ |
| P2-12 | Sur un poste client configuré avec le **nom public**, ouvrir le client. | Les imprimantes s'affichent. Un poste configuré avec l'IP échoue (cf. P2-31). | ☐ |

### 3.2 Politique de mot de passe — audit #13

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P2-13 | Créer un utilisateur local avec un mot de passe de 11 caractères. | Refus « au moins 12 caractères » (dans la fenêtre, avant envoi). | ☐ |
| P2-14 | Créer un utilisateur dont le mot de passe est égal à l'identifiant (12 caractères ou plus). | Refus « identique au nom d'utilisateur ». | ☐ |
| P2-15 | Changer son mot de passe en remettant le même. | Refus « doit être différent de l'actuel ». | ☐ |
| P2-16 | Un compte existant avec un mot de passe de 6 à 11 caractères se connecte. | Connexion acceptée (les anciens mots de passe restent valables jusqu'au prochain changement). | ☐ |
| P2-17 | Sur le serveur : `Autoprint.Server.exe --reset-admin` (dans le dossier d'installation, en administrateur). | Affiche un mot de passe temporaire aléatoire de 16 caractères ; la connexion avec ce mot de passe impose un changement. `admin123` ne fonctionne plus. | ☐ |

### 3.3 Messages d'erreur — audit #15

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P2-18 | Restaurer un fichier de sauvegarde corrompu (JSON modifié à la main). | Message « Erreur pendant la restauration… (réf. XXXXXXXX…) » sans détail technique ; la même référence apparaît dans les journaux du serveur avec le détail. | ☐ |
| P2-19 | Profils SNMP › tester un profil sur une IP sans imprimante. | Message de diagnostic clair (« n'a pas répondu sur le port SNMP… ») conservé. | ☐ |
| P2-20 | Profils SNMP › tester avec l'adresse `10.0.0.1; rm` ou `<script>`. | Refus « Adresse IP ou nom d'hôte de l'imprimante invalide ». | ☐ |
| P2-21 | Tests SMTP et AD avec de mauvais paramètres. | Le message garde le détail utile au diagnostic (réservé aux admins). | ☐ |

### 3.4 Sessions — audit #10

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P2-22 | Page de connexion. | Plus de case « Retenir l'identifiant » ; le champ identifiant est vide à chaque ouverture. | ☐ |
| P2-23 | Ouvrir la console avec un compte A dans un navigateur ; avec `admin` dans un autre, **désactiver** A. | Dans les 30 s, le navigateur de A est renvoyé à la connexion (ou ses actions échouent). | ☐ |
| P2-24 | Même test en **changeant le rôle** de A, puis en **réinitialisant son mot de passe**. | Même résultat. | ☐ |
| P2-25 | Modifier les permissions d'un rôle dont A est membre. | Même résultat pour A (et pour l'admin s'il est membre du rôle : backlog B21). | ☐ |
| P2-26 | A change lui-même son mot de passe puis se reconnecte immédiatement. | Reconnexion et navigation **sans erreur** (pas de 401 sur le tableau de bord). | ☐ |

### 3.5 Clé agent et client — audit #11

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P2-27 | Client › Options › cliquer 5 fois sur la version (zone technique). | La clé est masquée (`••••••••` + 4 derniers caractères). | ☐ |
| P2-28 | « Tester la connexion serveur » avec une configuration correcte. | « ✅ Connexion réussie ! » | ☐ |
| P2-29 | Modifier `HKLM\SOFTWARE\Autoprint\APIKEY` avec une clé fausse, relancer le client, tester. | « Clé refusée par le serveur… ». | ☐ |
| P2-30 | Mettre un nom de serveur inexistant dans `PRINTSERVER`, tester. | « Serveur injoignable… ». | ☐ |
| P2-31 | Mettre l'adresse IP du serveur dans `PRINTSERVER`, tester. | « Nom de serveur non autorisé… » (ou « Certificat du serveur refusé » si le certificat ne couvre pas l'IP). | ☐ |
| P2-32 | Poste sans le certificat de l'autorité interne, tester. | « Certificat du serveur refusé… ». | ☐ |
| P2-33 | Console › Paramètres › Sécurité. | La clé est masquée ; plus d'interrupteur « Modification autorisée » ; bouton « Copier la clé ». | ☐ |
| P2-34 | Cliquer « Copier la clé », coller dans un éditeur. | La clé complète est collée ; ses 4 derniers caractères correspondent à P2-27. | ☐ |
| P2-35 | Journal d'audit. | Une entrée `AGENT_KEY_REVEAL` par copie, avec l'utilisateur. | ☐ |
| P2-36 | **[API]** `GET /api/imprimantes` avec l'en-tête `X-Agent-Secret: <clé>`. | Réponse sans adresse IP, communauté SNMP, commentaire ni numéro de série ; le client continue d'afficher et d'installer les imprimantes (NR-05). | ☐ |

### 3.6 Retrait du service Windows — audit #16

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P2-37 | Sur un poste ayant une ancienne version avec le service `AutoprintService` (ou `AutoprintWorker`), installer le nouveau MSI. | Le service est arrêté et supprimé (`sc query AutoprintService` : introuvable). | ☐ |
| P2-38 | Sur ce poste, en PowerShell : `Get-ChildItem \\.\pipe\ \| Where-Object Name -like '*Autoprint*'`. | Seul le tube de réveil du client (`AutoprintClient_WakeUp_…`) peut apparaître, plus `AutoprintPipe`. | ☐ |

### 3.7 Durcissements divers — audit #17

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P2-39 | Journal d'audit › purger les anciens journaux (compte `admin`). | Purge effectuée (avant : refus systématique). | ☐ |
| P2-40 | Sauvegarde exportée **avec cette version**, puis restauration. | Les comptes locaux se connectent avec leur mot de passe habituel ; les rôles AD / manuels sont conservés. | ☐ |
| P2-41 | Restaurer une **ancienne** sauvegarde (version 26.08.24.3). | Les comptes locaux existant encore gardent leur mot de passe actuel ; tout le monde doit se reconnecter. | ☐ |
| P2-42 | Compte local migré depuis une très ancienne version (hachage SHA-256 hérité), se connecter. | Connexion acceptée puis changement de mot de passe imposé. | ☐ |
| P2-43 | GitHub › Security › Code scanning après push. | Alertes #1 à #11 fermées ; #12 à #15 à fermer manuellement comme « faux positif » si CodeQL les signale encore. | ☐ |
| P2-44 | Paramètres › Maintenance › exporter une sauvegarde. | Une fenêtre d'avertissement (données sensibles en clair) s'affiche ; « Annuler » n'exporte rien, « J'ai compris, télécharger » lance le téléchargement. | ☐ |

---

## 4. Phase 3

### 4.1 Chiffrement des secrets en base — audit #9

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P3-01 | Installation neuve, puis requête SQL : `SELECT [Key], [Value] FROM ServerSettings WHERE [Key] IN ('AgentApiKey','AdServicePassword','SmtpPass')`. | Les valeurs non vides commencent par `enc:v1:`. | ☐ |
| P3-02 | **Mise à jour** d'un serveur existant (secrets en clair), redémarrer le site, refaire la requête. | Les secrets sont désormais chiffrés (`enc:v1:`) ; journal du serveur : « N secret(s) chiffré(s) en base ». | ☐ |
| P3-03 | Après P3-02 : connexion d'un utilisateur AD, envoi d'un rapport par e-mail, ouverture du client sur un poste. | Tout fonctionne (mots de passe AD / SMTP et clé agent correctement déchiffrés). | ☐ |
| P3-04 | Paramètres › AD / SMTP : saisir un nouveau mot de passe, enregistrer, refaire la requête P3-01. | Nouvelle valeur chiffrée ; les tests AD / SMTP fonctionnent. | ☐ |
| P3-05 | Paramètres › SMTP › « Tester » sans retoucher le mot de passe (masqué). | Le test utilise le mot de passe enregistré (avant : le texte `●●●●●●●●` était envoyé). | ☐ |
| P3-06 | Même test en changeant uniquement le serveur SMTP. | Refus « saisissez son mot de passe ». | ☐ |
| P3-07 | Vérifier le dossier `C:\ProgramData\Autoprint\Keys`. | Il contient un fichier `key-….xml` ; voir P3-12 pour ses droits. | ☐ |

### 4.2 Sauvegarde et restauration des secrets — audit #9

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P3-08 | Export **sans** mot de passe de protection ; ouvrir le JSON. | `"SecretsProtected": false` ; valeurs vides pour `AgentApiKey`, `AdServicePassword`, `SmtpPass`. | ☐ |
| P3-09 | Export avec un mot de passe de moins de 12 caractères. | Refus avant téléchargement. | ☐ |
| P3-10 | Export **avec** un mot de passe ; ouvrir le JSON. | `"SecretsProtected": true` ; secrets en `bkp:v1:…` ; la clé agent n'apparaît pas en clair. | ☐ |
| P3-11 | Restaurer ce fichier : sans mot de passe, puis avec un mauvais mot de passe. | Refus clair dans les deux cas ; **aucune donnée modifiée**. | ☐ |
| P3-12 | Restaurer avec le bon mot de passe. | Succès ; AD, SMTP et postes clients fonctionnent ; comptes locaux avec leur mot de passe habituel. | ☐ |
| P3-13 | Restaurer le fichier de P3-08 (sans secrets). | Succès ; les secrets **actuels** du serveur sont conservés (clé agent inchangée). | ☐ |
| P3-14 | Restaurer une **ancienne** sauvegarde (26.08.24.3, secrets en clair). | Succès ; les secrets sont rechiffrés en base (P3-01). | ☐ |
| P3-15 | Restauration sur un **autre serveur** avec le fichier de P3-10 et son mot de passe. | Les secrets sont repris ; la clé agent est celle du serveur d'origine (les postes existants fonctionnent). | ☐ |

### 4.3 Droits NTFS posés par l'installeur — audit #9 et nouveau constat #19

Avec un compte **utilisateur standard** ouvert sur le serveur (session locale ou RDP) :

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P3-16 | Ouvrir `C:\Program Files\Autoprint Server\appsettings.json`. | Accès refusé. | ☐ |
| P3-17 | (SQLite) Ouvrir `C:\Program Files\Autoprint Server\Autoprint.db`. | Accès refusé. | ☐ |
| P3-18 | Ouvrir `C:\ProgramData\Autoprint\Keys`. | Accès refusé. | ☐ |
| P3-19 | En administrateur : Propriétés › Sécurité de `C:\Program Files\Autoprint Server`. | **NETWORK SERVICE n'apparaît plus** (avant : droit « Modification » sur tout le dossier) ; y compris après une **mise à jour**. | ☐ |
| P3-20 | Après P3-16 à P3-19 : console, rapports, synchronisation spouleur. | Tout fonctionne (le pool IIS, en SYSTEM, garde l'accès). | ☐ |

### 4.4 SMTP — audit #14

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P3-21 | Installation neuve › Paramètres › SMTP. | « Activer SSL / TLS » est coché. | ☐ |
| P3-22 | Décocher TLS. | Bandeau d'avertissement « Sans TLS, le mot de passe SMTP… circulent en clair ». | ☐ |
| P3-23 | Mise à jour d'un serveur dont le SMTP était configuré sans TLS. | Le réglage est **inchangé** (pas de rupture d'envoi). | ☐ |

### 4.5 Contrôles automatiques GitHub

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| P3-24 | Pousser la branche, onglet **Actions**. | Le workflow « Contrôles de sécurité » passe au vert (compilation + aucune dépendance vulnérable). | ☐ |
| P3-25 | Onglet **Insights › Dependency graph › Dependabot**. | Dependabot surveille NuGet et GitHub Actions ; les mises à jour Microsoft.* / System.* arrivent groupées. | ☐ |

---

## 4bis. Phase A — Correctifs du backlog (1re version)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| PA-01 | **SQLite** : installation neuve. | Installation sans erreur ; console, rapports, profils SNMP, sauvegarde fonctionnels (B1). | ☐ |
| PA-02 | **SQLite** : serveur dont l'installation précédente a échoué (base sans tables), relancer la mise à jour. | Les tables sont créées ; la console fonctionne (B1). | ☐ |
| PA-03 | Imprimantes › modifier une imprimante avec un identifiant incohérent (API) ou un modèle supprimé. | Messages « ID incohérent » / « Modèle introuvable » correctement accentués (B8). | ☐ |
| PA-04 | F12 › Console sur chaque page. | Plus d'erreur 404 sur `css/-base.css` (B20). | ☐ |
| PA-05 | Importer depuis le spouleur local une imprimante sans choisir de lieu ni de modèle. | Importée avec le statut « à corriger » ; réimporter la même : « 1 ignorée(s) » (B7). | ☐ |
| PA-06 | Base sans aucun lieu (ou sans modèle) : tenter un import. | Message « Créez au moins un lieu et un modèle… » (B7). | ☐ |
| PA-07 | **Mise à jour** d'un serveur dont `AllowedHosts` vaut `*`. | L'installeur demande le nom public (port grisé) ; `AllowedHosts` est renseigné après la mise à jour (B17). | ☐ |
| PA-08 | Mise à jour d'un serveur dont `AllowedHosts` est déjà renseigné. | L'écran n'est pas proposé ; valeur conservée. | ☐ |
| PA-09 | **Poste sans accès Internet** (ou pare-feu bloquant jsdelivr.net) : ouvrir la console. | Mise en forme, icônes et graphique du tableau de bord affichés (B16). F12 › Réseau : aucune requête vers jsdelivr. | ☐ |
| PA-10 | Sans `ReverseProxy:KnownProxies` : 11 connexions d'affilée en changeant l'en-tête `X-Forwarded-For` à chaque fois ([API]). | Blocage à la 11ᵉ (l'en-tête est ignoré) (B15). | ☐ |
| PA-11 | Avec un reverse proxy déclaré dans `ReverseProxy:KnownProxies` : journal d'audit / limitation. | L'IP réelle du client est utilisée (B15). | ☐ |
| PA-12 | Couper l'accès au contrôleur de domaine (pare-feu) puis connexion d'un utilisateur AD **déjà authentifié par Windows** (si les identifiants sont mis en cache) ou pendant une coupure transitoire. | Message « Annuaire Active Directory injoignable… » ; pas de connexion avec des droits non vérifiés (B13). | ☐ |
| PA-13 | Utilisateur AD hors de la base de recherche (Base DN) mais avec un rôle **manuel**. | Connexion avec le rôle manuel ; rôles de groupe AD retirés (B13). | ☐ |
| PA-14 | Créer un utilisateur en **décochant** « Changement du mot de passe obligatoire… ». | Première connexion sans demande de changement (B6). | ☐ |
| PA-15 | Créer un utilisateur sans toucher à la case (cochée par défaut). | Changement demandé à la première connexion (B6). | ☐ |
| PA-16 | Modifier un utilisateur ayant « Forcer changement MDP » coché (changer seulement son nom). | La case reste cochée après enregistrement (avant : décochée à chaque modification) (B6). | ☐ |
| PA-17 | Page de connexion › « Mot de passe oublié » avec l'identifiant d'un compte local ayant une adresse e-mail. | Message générique ; e-mail reçu avec un lien `https://<nom public>/reset-password?token=…` (B5). | ☐ |
| PA-18 | Même demande avec un identifiant inexistant, puis avec un compte AD. | Même message générique ; aucun e-mail (B5). | ☐ |
| PA-19 | Ouvrir le lien, saisir un mot de passe de moins de 12 caractères puis un mot de passe valide. | Refus puis « Mot de passe modifié » ; connexion avec le nouveau mot de passe ; journal d'audit : `USER_PWD_RESET_REQUEST`, `USER_PWD_RESET` (B5). | ☐ |
| PA-20 | Rouvrir le même lien, ou un lien de plus de 30 minutes. | « Lien de réinitialisation invalide ou expiré » (B5). | ☐ |
| PA-21 | Base de données : `SELECT PasswordResetToken FROM Users` pendant qu'une demande est en cours. | Valeur hexadécimale (empreinte), différente du jeton du lien (B5). | ☐ |
## 5. Version suivante — Site sans privilège (#8)

⚠️ À dérouler sur la **branche `feature/serveur-sans-localsystem`**, après la recette des phases 1 à 3. Tests à faire en **installation neuve** et en **mise à jour** (depuis la version des phases 1 à 3).

### 5.1 Installation

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| S8-01 | Installation neuve, puis `services.msc`. | Service « Autoprint Spooler » présent, démarré, démarrage automatique, compte « Système local ». | ☐ |
| S8-02 | Gestionnaire IIS › Pools d'applications › Autoprint › Paramètres avancés. | Identité : `ApplicationPoolIdentity`. | ☐ |
| S8-03 | `appsettings.json`. | `"Spooler": { "Mode": "Service" }`. | ☐ |
| S8-04 | SQL **local**, authentification Windows : SSMS › Sécurité › Connexions. | Connexion `IIS AppPool\Autoprint`, `db_owner` sur la base Autoprint. | ☐ |
| S8-05 | SQL **distant**, authentification Windows. | Connexion `DOMAINE\NOMSERVEUR$` sur le serveur SQL distant, `db_owner` sur la base. | ☐ |
| S8-06 | Authentification **SQL** (identifiant / mot de passe). | Installation sans erreur, aucune connexion Windows ajoutée. | ☐ |
| S8-07 | Droits NTFS : `appsettings.json` et `C:\ProgramData\Autoprint\Keys`. | `IIS AppPool\Autoprint` : lecture sur `appsettings.json`, modification sur `Keys` ; aucun autre utilisateur non administrateur. | ☐ |
| S8-08 | (SQLite) dossier du programme. | **Aucun** droit explicite de `IIS AppPool\Autoprint` : depuis B25 la base est dans `ProgramData\Autoprint\Data` (voir SB-01, SB-02). | ☐ |

### 5.2 Mise à jour

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| S8-09 | Mettre à jour un serveur dont le pool tourne en `LocalSystem`. | Pool passé en `ApplicationPoolIdentity`, service installé et démarré, `Spooler:Mode = Service`, autres réglages conservés. | ☐ |
| S8-10 | Même mise à jour avec SQL en authentification Windows, installeur lancé par un compte **sans** droit d'administration SQL. | Message affichant le script SQL à exécuter ; après exécution par un DBA, la console fonctionne. | ☐ |
| S8-11 | Après mise à jour : secrets toujours lisibles (connexion AD, test SMTP, postes clients). | Tout fonctionne (le pool lit les clés de chiffrement). | ☐ |

### 5.3 Fonctionnement

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| S8-12 | Créer, modifier (nom, partage, mode direct), renommer, supprimer une imprimante, avec synchronisation à chaque étape. | Les files Windows suivent. Observateur d'événements › Journaux Windows › Application, source `Autoprint.SpoolerService` : une ligne « Commande … : OK » par commande (B56). | ☐ |
| S8-13 | Créer une imprimante dont le modèle a un pilote **non installé** sur le serveur, synchroniser. | Échec de synchronisation « Pilote … non installé sur le serveur ». | ☐ |
| S8-14 | Arrêter le service `AutoprintSpooler`, synchroniser une modification. | Message clair « Service Windows AutoprintSpooler injoignable… » ; le reste de la console fonctionne. Redémarrer le service : la synchronisation passe. | ☐ |
| S8-15 | Lecture seule pendant que le service est arrêté : tableau de bord, liste des imprimantes, scan du spouleur local, prévisualisation de synchronisation. | Tout fonctionne (les lectures n'utilisent pas le service). | ☐ |
| S8-16 | Rapports PDF / CSV, supervision SNMP, envoi d'e-mails, connexion AD. | Tout fonctionne sous le compte du pool. | ☐ |
| S8-17 | Depuis une session d'un **autre** utilisateur du serveur (non administrateur), PowerShell : `$p = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'AutoprintSpooler'); $p.Connect(2000)`. | Erreur « Accès refusé ». | ☐ |
| S8-18 | Depuis un **autre poste** : `$p = New-Object System.IO.Pipes.NamedPipeClientStream('NOMSERVEUR', 'AutoprintSpooler'); $p.Connect(2000)`. | Erreur « Accès refusé » (accès réseau interdit). | ☐ |
| S8-19 | Gestionnaire des tâches › Détails : processus `w3wp.exe` du site. | Nom d'utilisateur : `Autoprint` (et non `SYSTEM`). | ☐ |

### 5.5 SNMPv3 et identifiants SNMP (phase B)

Prérequis : une imprimante (ou un simulateur) configurée en SNMPv3 avec un utilisateur `authPriv` (SHA-256 / AES-128), et une imprimante en v2c.

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| SV3-01 | Menu › Identifiants SNMP › créer un profil v3 sans utilisateur, puis en `authPriv` sans mot de passe de chiffrement, puis avec un mot de passe de 7 caractères. | Trois refus explicites. | ☐ |
| SV3-02 | Créer « SNMPv3 Parc » (utilisateur, SHA-256, AES-128, mots de passe de l'imprimante). | Créé ; la liste n'affiche aucun mot de passe. | ☐ |
| SV3-03 | Base : `SELECT V3AuthPassword, V3PrivPassword FROM SnmpCredentials`. | Valeurs `enc:v1:…` (chiffrées). | ☐ |
| SV3-04 | Rouvrir le profil, changer uniquement la description, enregistrer. | Mots de passe conservés (supervision toujours OK). | ☐ |
| SV3-05 | Modèles › attribuer « SNMPv3 Parc » au modèle de l'imprimante v3. Fiche de l'imprimante. | « Identifiants du modèle : SNMPv3 Parc » ; champs communauté / version masqués. | ☐ |
| SV3-06 | Diagnostic de l'imprimante v3 (modèle avec profil d'OID). | Toners / compteurs remontés. | ☐ |
| SV3-07 | Attendre un cycle de supervision ; tableau de bord et rapport PDF. | Niveaux et compteurs de l'imprimante v3 à jour. | ☐ |
| SV3-08 | Modifier le profil avec un **mauvais** mot de passe, lancer le diagnostic / le test de l'éditeur d'OID. | « Identifiants SNMPv3 refusés par l'imprimante… » ou « n'a pas répondu… (SNMPv3, utilisateur « … ») » ; aucun mot de passe dans le message ni dans les journaux. | ☐ |
| SV3-09 | Une imprimante du même modèle reste en v2c : dans sa fiche, choisir un profil v2c (exception). | Supervision OK pour les deux imprimantes. | ☐ |
| SV3-10 | Vider le choix de l'exception. | L'imprimante reprend le profil du modèle. | ☐ |
| SV3-11 | Imprimante sans profil (ni sur elle, ni sur son modèle). | Les champs communauté / version de la fiche s'affichent et sont **bien enregistrés** (avant : ignorés). | ☐ |
| SV3-12 | Éditeur de profils d'OID › Tester / Auto-détecter avec « Identifiants » = SNMPv3 Parc. | Valeurs lues en v3. | ☐ |
| SV3-13 | Supprimer un profil utilisé par un modèle et une imprimante. | Message « N modèle(s) et N imprimante(s) utilisent désormais leurs paramètres par défaut » ; supervision toujours fonctionnelle. | ☐ |
| SV3-14 | Sauvegarde **avec** mot de passe, puis restauration (même serveur puis autre serveur). | Profils et liens modèle / imprimante restaurés, supervision v3 OK. | ☐ |
| SV3-15 | Sauvegarde **sans** mot de passe, restauration sur le même serveur. | Profils restaurés avec leurs secrets actuels conservés. | ☐ |
| SV3-16 | Utilisateur avec seulement `PRINTER_READ` : ouvrir une fiche imprimante. | Pas d'erreur ; la liste des identifiants est simplement indisponible. | ☐ |

### 5.6 Base SQLite, état du service, délais (phase B)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| SB-01 | **SQLite**, installation neuve. | Base dans `C:\ProgramData\Autoprint\Data\Autoprint.db` ; `appsettings.json` pointe dessus ; aucun fichier `.db` dans `C:\Program Files\Autoprint Server` (B25). | ☐ |
| SB-02 | **SQLite**, mise à jour d'un serveur dont la base est dans le dossier du programme. | Base (et `-wal` / `-shm`) déplacée dans `ProgramData\Autoprint\Data`, données intactes ; `IIS AppPool\Autoprint` n'a plus de droit sur le dossier du programme (B25). | ☐ |
| SB-03 | Même mise à jour alors qu'une base existe déjà dans `ProgramData\Autoprint\Data`. | L'installeur s'arrête avec un message, rien n'est écrasé. | ☐ |
| SB-04 | Droits de `C:\ProgramData\Autoprint\Data` (compte standard). | Accès refusé ; site fonctionnel. | ☐ |
| SB-05 | Tableau de bord › État du service, service `AutoprintSpooler` démarré puis arrêté. | « EN LIGNE » puis « ARRÊTÉ » sous « Service AutoprintSpooler » (B26). | ☐ |
| SB-06 | Simuler un spouleur bloqué (suspendre le service Spouleur d'impression pendant une synchronisation). | Après 60 s : « Le spouleur Windows n'a pas répondu en 60 s… » et « Synchronisation interrompue : spouleur indisponible » ; les synchronisations suivantes aboutissent une fois le spouleur relancé (B27, B67). | ☐ |

### 5.7 Améliorations (phase C)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| SC-01 | Installation neuve : `appsettings.json`. | `Jwt:ExpireMinutes = 30`. | ☐ |
| SC-02 | Rester connecté à la console 45 min en l'utilisant normalement. | Aucune déconnexion (F12 › Réseau : un appel `api/auth/refresh` environ toutes les 25 min) (B19). | ☐ |
| SC-03 | Retirer une permission au rôle d'un utilisateur connecté. | Son prochain renouvellement échoue (session coupée) : il se reconnecte avec ses nouveaux droits (B19, phase 2 #10). | ☐ |
| SC-04 | Laisser la console ouverte plus de 10 h. | Retour à la page de connexion (durée maximale de session) (B19). | ☐ |
| SC-05 | Paramètres › Sécurité › « Générer une nouvelle clé » avec 30 jours de transition. | La nouvelle clé s'affiche une fois ; bandeau « l'ancienne clé est encore acceptée jusqu'au… » ; journal d'audit `AGENT_KEY_ROTATE` (B18). | ☐ |
| SC-06 | Pendant la transition : un poste avec l'ancienne clé, un poste avec la nouvelle. | Les deux fonctionnent (B18). | ☐ |
| SC-07 | Générer une nouvelle clé avec « Aucune (révocation immédiate) ». | Les postes avec les anciennes clés sont refusés (« Clé refusée par le serveur… » dans le test de connexion du client) ; ceux avec la nouvelle fonctionnent (B18). | ☐ |
| SC-08 | Rôles › modifier un rôle. | Bandeau expliquant que les sessions des membres seront fermées (B21). | ☐ |
| SC-09 | Restauration d'une sauvegarde **sans** mot de passe. | Message expliquant la conservation des secrets du serveur et les conséquences sur un nouveau serveur (B24). | ☐ |

### 5.8 Dette technique (phase D)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| SD-01 | Installation neuve SQL Server, puis mise à jour d'un serveur existant : journal de démarrage du site (ou de `--migrate-only`). | Migration `AlignModelSnapshot` appliquée ; aucune ligne « [DbInitializer] Mise à niveau du schéma en échec » (B2, B3). | ☐ |
| SD-02 | Installation neuve SQLite : journal de démarrage. | Aucune erreur « duplicate column » ni ligne `fail:` liée au schéma (B3). | ☐ |
| SD-03 | Supprimer une imprimante dont la file Windows ne peut pas être supprimée (droits retirés, file en cours d'utilisation), synchroniser. | La synchronisation signale l'échec (avant : file considérée comme supprimée) (B3). | ☐ |
| SD-04 | Créer une imprimante avec une adresse refusée par le spouleur, synchroniser. | Message « Création du port … impossible : … » (B3). | ☐ |
| SD-05 | Rôles › liaison AD › rechercher un groupe contenant `&`, `#` ou un espace. | Résultats corrects (paramètre désormais encodé) (B9). | ☐ |
| SD-06 | Page de connexion › mot de passe expiré › annuler la fenêtre de changement. | Retour propre à la page de connexion, plus de jeton dans le stockage du navigateur (B9). | ☐ |
| SD-07 | Console publiée (production) : depuis une autre origine, requête `fetch` vers l'API. | Pas d'en-tête `Access-Control-Allow-Origin` : requête bloquée par le navigateur (B10). | ☐ |
| SD-08 | Mise à jour d'un serveur : port proposé par l'installeur. | Port du site IIS existant (B11). | ☐ |
| SD-09 | Client : thème clair / sombre, fenêtre unique, liste des imprimantes. | Affichage correct avec MaterialDesign 5.4 (B12). | ☐ |
| SD-10 | Compilation complète de la solution (`dotnet build Autoprint.sln`) deux fois de suite. | Réussie à chaque fois (B28). | ☐ |
| SD-11 | Rapport planifié PDF : envoi manuel. | PDF reçu et lisible : bandeau, titre, tableau, accents (contrôlé ici sur base de test). | ☐ |
| SD-12 | **SQL Server** (installation neuve, puis mise à jour d'un serveur existant) : Profils SNMP › créer un profil d'OID avec au moins un élément (toner, compteur), l'enregistrer, le modifier. | Enregistré sans erreur ; aucune ligne « Cannot insert the value NULL into column 'DateCreation' » dans le journal (B60). | ☐ |
| SD-13 | Deux imprimantes sur la **même adresse IP** (ex. deux pilotes) : créer, synchroniser ; modifier l'une, synchroniser ; renommer l'une, synchroniser ; supprimer l'une, synchroniser. | Les deux files existent ; seule la file modifiée / renommée / supprimée change, l'autre est intacte (B66). | ☐ |

### 5.9 Montées de version (phase E)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| SE-01 | Serveur avec un runtime ASP.NET Core 10 plus ancien que la version embarquée (ex. 10.0.0 ou 10.0.14) : lancer l'installeur, étape des prérequis. | « Version 10.0.x trop ancienne : 10.0.15 requise » et bouton « Mettre à jour » ; l'étape suivante est bloquée (B39). | ☐ |
| SE-02 | Cliquer « Mettre à jour », puis relancer l'analyse. | « Runtime .NET 10.0.15 installé » puis « Installé (version 10.0.15) » ; le site redémarre correctement (B38). | ☐ |
| SE-03 | Serveur déjà en 10.0.15 ou plus récent. | « Installé (version …) », pas de bouton (B39). | ☐ |
| SE-04 | Console : mise à jour d'un serveur où l'on était déjà connecté (navigateur non fermé). | Session conservée, thème et colonnes de la grille Imprimantes conservés (format de stockage inchangé, B41). | ☐ |
| SE-05 | Console : changer de thème, trier / masquer des colonnes de la grille Imprimantes, recharger la page, se déconnecter. | Préférences conservées après rechargement ; jeton supprimé à la déconnexion (contrôlé ici, B41). | ☐ |
| SE-06 | Console après mise à jour : outils de développement › Réseau. | `index.html` avec `Cache-Control: no-cache` ; bibliothèques chargées en `?v=5.3.8`, `?v=1.13.2`, `?v=4.6.1` (contrôlé ici, B40, B43). | ☐ |
| SE-07 | Tableau de bord : graphique de répartition, icônes, info-bulles et menus déroulants Bootstrap. | Affichage et comportement inchangés (B40). | ☐ |
| SE-08 | Rapport planifié PDF (plus de 45 imprimantes, mesures « Compteur » et « Pannes » cochées dans un ordre quelconque) : envoi manuel. | PDF reçu (quelques dizaines de Ko), accents corrects, en-tête du tableau sur chaque page, intitulés des deux dernières colonnes conformes aux valeurs (contrôlé ici avec 62 imprimantes : 40 Ko, 2 pages, B42). | ☐ |
| SE-09 | Serveur installé : rapport PDF planifié, exécuté par le site (compte du pool d'applications, sans profil chargé). | PDF généré ; pas d'erreur `libSkiaSharp` dans les journaux ; dossier du site : `runtimes\win-x64\native\libSkiaSharp.dll` présent, `Licenses\AutoprintSans-OFL-LICENSE.txt` présent (B42). | ☐ |

### 5.10 Mise à jour d'un serveur SQLite 1.1.x (B47)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| SM-01 | Serveur 1.1.0 en SQLite (instantané) : mise à jour avec l'installeur 26.11.6.2 (ou plus récent). | « Mise à jour terminée » ; base déplacée dans `C:\ProgramData\Autoprint\Data` (contrôlé ici sur la base du serveur de test). | ☐ |
| SM-02 | Après SM-01 : tableau de bord, Imprimantes, Lieux, Modèles, Rapports, Profils SNMP. | Aucune erreur 500 ; imprimantes et lieux existants présents ; chaque lieu garde son réseau (contrôlé ici). | ☐ |
| SM-03 | Après SM-01 : créer un lieu et une imprimante, modifier une imprimante. | Enregistrement sans erreur (contrôlé ici). | ☐ |
| SM-04 | Serveur déjà passé en 26.11.4.1 avec erreurs 500 : installer la 26.11.6.2 par-dessus. | Mêmes résultats que SM-02 et SM-03, sans restaurer d'instantané (contrôlé ici sur la base d'après la mise à jour ratée). | ☐ |
| SM-05 | Après la mise à jour, compte d'un rôle qui avait « Administration » en 1.1.x (ex. Infra) : page Découverte, Rapports, Profils SNMP, Archivage. | Accès direct, sans rien cocher : « Administration » donne toutes les permissions, y compris les nouvelles (B50, contrôlé ici sur ta base). | ☐ |
| SM-06 | Rôle sans Administration mais avec « Rôles - écriture » : Rôles > ce rôle, cocher Scan réseau, Enregistrer, se reconnecter. | « Rôle enregistré » ; après reconnexion, la page Découverte s'ouvre (B50, contrôlé ici). | ☐ |
| SM-07 | Même compte : forcer l'enregistrement d'« Administration » (API) sur son rôle. | Refus 403 avec message affiché ; rien n'est modifié (B49/B50, contrôlé ici). | ☐ |
| SR-01 | Rôles : ouvrir un rôle. | Plus de colonne « Suppr. » pour la ligne Rôles : « Rôles - écriture » permet aussi de supprimer (B50). La permission « Supprimer des rôles » n'existe plus. | ☐ |
| SR-02 | Compte avec « Rôles - écriture » sans Administration : ouvrir un rôle. | Cases « Administration » grisées ; « Système » grisée si le compte n'a pas Paramètres (contrôlé côté serveur ici). | ☐ |
| SR-03 | Même compte : modifier ou supprimer SuperAdmin, lui lier un groupe AD, modifier le compte `admin`, attribuer SuperAdmin à un compte. | Refus avec message pour chaque opération (contrôlé ici). | ☐ |
| SR-04 | Avec `admin` seul administrateur actif : se désactiver, ou retirer « Administration » au rôle SuperAdmin. | Refus : « dernier administrateur actif » (contrôlé ici). | ☐ |
| SR-05 | Mise à jour d'un serveur SQL Server. | Migration `FusionPermissionsRoles` appliquée sans erreur (contrôlé ici sur LocalDB). | ☐ |

### 5.11 Installation neuve sur serveur français (B51, B52)

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| SI-01 | Installation **neuve** (SQLite) sur un Windows Server en français, puis ouvrir la console **depuis un autre poste**. | La console s'affiche (en 26.11.6.2 : délai dépassé, puis erreur 500 une fois le port ouvert). | ☐ |
| SI-02 | Sur le serveur : droits de `C:\Program Files\Autoprint Server\appsettings.json`, `C:\ProgramData\Autoprint\Data` et `...\Keys`. | `IIS APPPOOL\Autoprint` présent (lecture sur `appsettings.json`, modification sur `Data` et `Keys`), en plus de Système et Administrateurs (B51). | ☐ |
| SI-03 | Pare-feu Windows du serveur. | Règle entrante « Autoprint Server », TCP, port du site ; recréée avec le même port après une mise à jour (B52). | ☐ |
| SI-04 | Journal Application juste après l'installation. | Aucune erreur « IIS AspNetCore Module » ni « UnauthorizedAccessException » ; pas d'avertissement `AutoprintSpooler` « Identité IIS AppPool\Autoprint introuvable ». | ☐ |
| SI-05 | Installation neuve **SQL Server** sur un serveur SQL **vierge** (base inexistante) : « Tester la connexion », puis installer. | « Connexion réussie ; la base … sera créée » ; installation sans erreur, base créée, console accessible (B59). | ☐ |
| SI-06 | **Mise à jour** d'un serveur existant (même version ou version précédente), puis ouvrir la console sans rien faire d'autre. | Console accessible (pas d'erreur 503) ; Gestionnaire IIS : pool « Autoprint » démarré (B61). | ☐ |
| SI-07 | Mise à jour d'un serveur 1.1.x : `appsettings.json` › `Jwt:ExpireMinutes`. | 30 (et non plus 120) ; un jeton émis après la mise à jour expire au bout de 30 min (B63). | ☐ |

### 5.4 Désinstallation

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| S8-20 | Désinstaller Autoprint Server. | Le service `AutoprintSpooler` est arrêté et supprimé. | ☐ |

---

## Historique des mises à jour

| Date | Phase | Modification |
|---|---|---|
| 04/11/2026 | Phase 1 | Création : non-régression générale, tests P1-01 à P1-46. |
| 04/11/2026 | Phase 2 | Ajout des tests P2-01 à P2-44 (en-têtes, mots de passe, erreurs, sessions, clé agent, service Windows, divers). |
| 04/11/2026 | Phase 3 | Ajout des tests P3-01 à P3-25 (chiffrement des secrets, sauvegarde, droits NTFS, SMTP, contrôles GitHub). |
| 04/11/2026 | Correctif B22 | Ajout du test NR-10 (schéma SQL Server : rapports planifiés, profils SNMP, sauvegarde). |
| 04/11/2026 | #8 (version suivante) | Ajout des tests S8-01 à S8-20 (site sans privilège, service AutoprintSpooler, droits SQL / NTFS). |
| 04/11/2026 | Phase A | Ajout des tests PA-01 à PA-21 (SQLite, import, nom public, bibliothèques embarquées, proxy, AD, premier login, mot de passe oublié). |
| 04/11/2026 | Phase B | Ajout des tests SV3-01 à SV3-16 (SNMPv3, identifiants SNMP) et SB-01 à SB-06 (SQLite dans ProgramData, état du service, délais). |
| 04/11/2026 | Phase C | Ajout des tests SC-01 à SC-09 (renouvellement de session, changement de clé agent, messages). |
| 04/11/2026 | Phase D | Ajout des tests SD-01 à SD-11 (schéma, erreurs remontées, avertissements, CORS, installeur, mises à jour). |
| 04/11/2026 | Phase E | Ajout des tests SE-01 à SE-09 (runtime exigé par l'installeur, stockage du navigateur, bibliothèques web, cache, rapports PDF avec SkiaSharp). |
| 06/11/2026 | Correctif B47 | Ajout des tests SM-01 à SM-05 (mise à jour d'un serveur SQLite 1.1.x). |
| 06/11/2026 | Correctif B49 | Ajout des tests SM-06 et SM-07 (refus d'accorder des droits affiché à l'écran). |
| 06/11/2026 | B50 (règles des rôles) | SM-05 à SM-07 réécrits ; P1-13, P1-15, P1-17, P1-19 adaptés ; ajout de SR-01 à SR-05. |
| 08/11/2026 | B56, recette en labo | S8-08 mis à jour (B25) ; S8-12 : source `Autoprint.SpoolerService` (B56). |
| 08/11/2026 | B51, B52 (recette en labo) | Ajout de SI-01 à SI-04 (installation neuve sur serveur français : droits du pool IIS, pare-feu). Labo Hyper-V décrit au § 0.1. |
| 08/11/2026 | B59 (recette en labo) | Ajout de SI-05 (installation neuve sur un SQL Server vierge). |
| 08/11/2026 | B60 (recette en labo) | Ajout de SD-12 (profil d'OID avec éléments sur SQL Server). |
| 08/11/2026 | B61 (recette en labo) | Ajout de SI-06 (pool IIS redémarré après une mise à jour). |
| 08/11/2026 | B63 (recette en labo) | Ajout de SI-07 (durée des jetons après mise à jour). |
| 08/11/2026 | B66, B67 (recette en labo) | Ajout de SD-13 (imprimantes partageant une IP) ; SB-06 : échec attendu en 60 s avec interruption de la synchronisation. |
