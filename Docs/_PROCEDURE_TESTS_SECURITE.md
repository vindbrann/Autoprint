# 🧪 Procédure de Recette — Durcissement Sécurité

Procédure à dérouler **une seule fois, à la fin de toutes les phases**, sur une installation complète. Elle est mise à jour à chaque modification ; chaque test indique la phase et le point de l'audit (`_AUDIT_SECURITE_2026-11.md`) qu'il couvre.

**Dernière mise à jour :** 04/11/2026 — Phase 3

---

## 0. Préparation de l'environnement

### 0.1 Matériel et comptes

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
| P1-13 | Utilisateurs › Créer : ouvrir la liste déroulante des rôles. | Seuls les rôles dont les droits sont inclus dans ceux de Helpdesk sont proposés (pas de SuperAdmin). | ☐ |
| P1-14 | Créer un utilisateur avec le rôle `Helpdesk`. | Création OK. | ☐ |
| P1-15 | Modifier le compte `admin` (nom, mot de passe ou statut). | Refus : « Ce compte dispose de droits que vous n'avez pas ». | ☐ |
| P1-16 | Supprimer un compte SuperAdmin (autre que `admin`). | Refus. | ☐ |
| P1-17 | Rôles › modifier `Helpdesk` en lui ajoutant `SETTINGS_MANAGE`. | Refus : « Vous ne pouvez pas accorder des droits que vous n'avez pas ». | ☐ |
| P1-18 | Rôles › modifier ou supprimer `SuperAdmin`. | Refus. | ☐ |
| P1-19 | Rôles › créer un rôle avec `PRINTER_DELETE`. | Refus. | ☐ |
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
| P1-34 | 5 mauvais mots de passe sur un compte **AD**. | Le compte est ralenti dans Autoprint ; **vérifier dans l'AD que le compte n'est pas verrouillé** (selon la stratégie de verrouillage du domaine). | ☐ |

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
| S8-08 | (SQLite) dossier du programme. | `IIS AppPool\Autoprint` : modification sur le dossier (backlog B25). | ☐ |

### 5.2 Mise à jour

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| S8-09 | Mettre à jour un serveur dont le pool tourne en `LocalSystem`. | Pool passé en `ApplicationPoolIdentity`, service installé et démarré, `Spooler:Mode = Service`, autres réglages conservés. | ☐ |
| S8-10 | Même mise à jour avec SQL en authentification Windows, installeur lancé par un compte **sans** droit d'administration SQL. | Message affichant le script SQL à exécuter ; après exécution par un DBA, la console fonctionne. | ☐ |
| S8-11 | Après mise à jour : secrets toujours lisibles (connexion AD, test SMTP, postes clients). | Tout fonctionne (le pool lit les clés de chiffrement). | ☐ |

### 5.3 Fonctionnement

| ID | Test | Résultat attendu | ✔ |
|---|---|---|---|
| S8-12 | Créer, modifier (nom, partage, mode direct), renommer, supprimer une imprimante, avec synchronisation à chaque étape. | Les files Windows suivent. Observateur d'événements › Journaux Windows › Application, source `AutoprintSpooler` : une ligne par commande. | ☐ |
| S8-13 | Créer une imprimante dont le modèle a un pilote **non installé** sur le serveur, synchroniser. | Échec de synchronisation « Pilote … non installé sur le serveur ». | ☐ |
| S8-14 | Arrêter le service `AutoprintSpooler`, synchroniser une modification. | Message clair « Service Windows AutoprintSpooler injoignable… » ; le reste de la console fonctionne. Redémarrer le service : la synchronisation passe. | ☐ |
| S8-15 | Lecture seule pendant que le service est arrêté : tableau de bord, liste des imprimantes, scan du spouleur local, prévisualisation de synchronisation. | Tout fonctionne (les lectures n'utilisent pas le service). | ☐ |
| S8-16 | Rapports PDF / CSV, supervision SNMP, envoi d'e-mails, connexion AD. | Tout fonctionne sous le compte du pool. | ☐ |
| S8-17 | Depuis une session d'un **autre** utilisateur du serveur (non administrateur), PowerShell : `$p = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'AutoprintSpooler'); $p.Connect(2000)`. | Erreur « Accès refusé ». | ☐ |
| S8-18 | Depuis un **autre poste** : `$p = New-Object System.IO.Pipes.NamedPipeClientStream('NOMSERVEUR', 'AutoprintSpooler'); $p.Connect(2000)`. | Erreur « Accès refusé » (accès réseau interdit). | ☐ |
| S8-19 | Gestionnaire des tâches › Détails : processus `w3wp.exe` du site. | Nom d'utilisateur : `Autoprint` (et non `SYSTEM`). | ☐ |

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
