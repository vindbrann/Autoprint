# 🧪 Procédure de Recette — Durcissement Sécurité

Procédure à dérouler **une seule fois, à la fin de toutes les phases**, sur une installation complète. Elle est mise à jour à chaque modification ; chaque test indique la phase et le point de l'audit (`_AUDIT_SECURITE_2026-11.md`) qu'il couvre.

**Dernière mise à jour :** 04/11/2026 — Phase 1

---

## 0. Préparation de l'environnement

### 0.1 Matériel et comptes

| Élément | Détail |
|---|---|
| Serveur | Windows Server avec IIS, SQL Server (ou SQLite — cf. backlog B1 : installation neuve SQLite actuellement cassée), Autoprint Server installé via l'installeur, site en **HTTPS** avec le nom public (ex. `autoprint.maboite.fr`). |
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

## Historique des mises à jour

| Date | Phase | Modification |
|---|---|---|
| 04/11/2026 | Phase 1 | Création : non-régression générale, tests P1-01 à P1-46. |
