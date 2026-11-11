# Résultats de la recette en labo

Recette de `Docs/_PROCEDURE_TESTS_SECURITE.md` sur le labo Hyper-V (`tools/lab/README.md`) : Windows Server 2022 **français**, domaine `lab.autoprint.test`, poste Windows 11 hors domaine.
Tests **[API]** joués par `tools/lab/Recette-Api.ps1` ; les autres à la main ou dans le navigateur.

## ▶️ Reprise de la recette (état au 09/11/2026, 01:00)

**Bilan** : 225 tests sur 238 joués. 26.11.8.2 (build `66873e3`) installée par l'installeur et validée : anomalies B32 (en partie), B53, B54, B57, B58 (en partie), B59 à B69, B71 corrigées et validées ; B70 (journal des pilotes) notée, non corrigée.

**Reste à tester (13)** :
- ✅ Tous joués le 09/11 (voir « Derniers tests »). Pour mémoire : installeur graphique (avec l'utilisateur) : P2-01 (nom public invalide, installation neuve), S8-10 (mise à jour par un compte sans droit SQL : SQL distant SRV-AD), SB-03 (mise à jour SQLite avec une base déjà dans `ProgramData`), SE-01 à SE-03 (runtime ASP.NET Core trop ancien : installer un runtime 10.0.0 avant).
- Par Claude : P3-15 (restauration sur un autre serveur), PA-02 et PA-06 (base SQLite vide), S8-20 (désinstallation : sur un instantané).
- GitHub, onglet Security (connexion de l'utilisateur) : P1-02, P2-43 (joués le 09/11 via la PR brouillon vindbrann/Autoprint#6 : voir « Derniers tests »).
- SC-04 (session de 10 h) : contrôle dans le code (`AuthService.MaxSessionLifetime`).

**Livraison** : `Livraison-26.11.9.1` produite le 09/11/2026 (commit `a6d3551`) : 26.11.8.2 validée + B72 à B75. **Validée en labo** : mise à jour 26.11.8.2 → 26.11.9.1 de l'installation SQL Server par l'installeur de livraison (« Mise à jour terminée »), puis lots API AD (20/20, dont B75-1 à B75-6 : filtre étendu, joker, groupe, injection dans le nom, filtres invalides refusés au test et à l'enregistrement), Clé (7/7), Serveur (17/17). Instantané « SQL Server 26.11.9.1 (livraison validée) ». **Fusion** : PR vindbrann/Autoprint#6 fusionnée dans `master` le 09/11/2026 (commit `6e0bc80`), contrôles au vert. Sur `master` : Dependabot 0 alerte ouverte (n° 7 fermée, P1-02 ✅) ; CodeQL 13 alertes fermées, 2 anciennes encore ouvertes (n° 8 et 9, critiques : chemin LDAP `LDAP://{Domain}/{BaseDn}` du test du filtre AD, `BaseDn` non contrôlé). **Ensuite** : alertes CodeQL n° 8 et 9, B70, suites de B32 / B58.

**État du labo** (VM arrêtées le 09/11 à 01:00) :
- SRV-PRINT : installation **SQL Server** en 26.11.8.2 finale, HTTPS 443 (certificat du labo, empreinte E2CAA9A9…) et 8080. Instantanés : « SQL Server 26.11.8.2 final (livraison, recette 09-11) » (état courant), « SQL Server 26.11.8.2 (B65-B68 à chaud, avant écran) », « SQL Server 2025 redémarré, avant S8-06 » (SQL Server sans Autoprint), « Serveur en 1.1.2 (base réelle corrigée) » (base de production 1.1, à n'utiliser que dans le labo), « Recette SQLite 10-11 (avant SQL Server) », « Labo configuré », « OS installé ».
- SRV-AD : contrôleur de domaine (adresses .10 et .11 : le domaine se résout aussi en .11), SQL Server 2025 Express (S8-05), simulateur SNMP (.10 v3, .11 v2c), faux SMTP, fausse imprimante 9100.
- CLT-TEST : client 26.11.8.2 final (clé de l'installation SQL Server), thème sombre pour `LabAdmin`.
- Après une restauration d'instantané : réactiver « Interface de services d'invité » (`Copy-VMFile`) ; vérifier que SQL Server est démarré sur SRV-PRINT.
- Disque C de l'hôte : ~30 Go libres ; les 7 instantanés de CLT-TEST (~21 Go) peuvent être supprimés si besoin.

**Pour reprendre** :
1. Hyper-V : démarrer `SRV-AD`, attendre ~1 min, puis `SRV-PRINT` et `CLT-TEST` (ou demander à Claude : « reprends la recette du labo »).
2. Console : `https://srv-print.lab.autoprint.test` (le navigateur intégré force HTTPS pour ce nom).
3. Tests API : `tools\lab\Recette-Api.ps1 -BaseUrl https://srv-print.lab.autoprint.test -Profil sql -Lot <Lot>` (HTTP redirige vers HTTPS et perd l'authentification). Lots : `Comptes`, `Escalade`, `Sessions`, `Imprimantes`, `Web`, `AD`, `Cle`, `Donnees`, `Serveur`, `Email`, `Snmp`, `Divers`, `Infra`, `Limitation`. Les lots qui se connectent beaucoup peuvent atteindre la limite de 10 connexions / minute : rejouer le lot seul.

**Fichiers locaux (hors dépôt, chiffrés pour le compte Windows)** dans `%USERPROFILE%\.autoprint-lab\` : `labadmin.cred`, `recette-secrets.xml` (comptes du labo, `sql.*`, `snmp.*`), `recette-secrets-sql.xml` / `-sqlauth.xml` / `-sqldistant.xml` / `-sm.xml` (comptes Autoprint par installation), `srv-print.cer`, `dl\` (SQL Server 2025 Express, Python portable avec snmpsim, installeurs 1.1.0 / 1.1.2 / 26.08.24.2), `snmpsim-venv\`.
## Derniers tests (09/11/2026, soir)

| ID | Résultat | Remarque |
|---|---|---|
| SC-04 | ✅ (code) | Non observable sans attendre 10 h : `AuthService.RefreshAsync` refuse tout renouvellement 10 h après la connexion d'origine (`auth_time` conservé à chaque renouvellement) ; la console se déconnecte à l'expiration du jeton en cas de refus (`CustomAuthStateProvider`). |
| P3-15 | ✅ après correctif (B72) | « Autre serveur » simulé sur SRV-PRINT : export protégé, puis dossier `Keys` remplacé par des clés neuves (secrets en base illisibles), puis restauration. Avant correctif : restauration refusée « mot de passe incorrect », postes et console en 500. Après : avant restauration « Clé agent absente » (404), ancienne clé refusée (401) ; après restauration : AD, SMTP, lecture de la clé et clé agent d'origine OK. |
| S8-10 | ✅ après correctif (B73) | Mise à jour lancée par `LAB\installeur` (administrateur local, pas administrateur SQL ; accès SQL du pool retiré au préalable) : 1ᵉʳ essai, script non copiable puis échec « permission denied in database 'master' ». Après correctif : script dans `Autoprint-droits-SQL.sql` (Bureau) et presse-papiers, « Mise à jour terminée » ; site en 500 avant le script, console OK après son exécution par un administrateur SQL. |
| SB-03 | ✅ | Serveur 1.1.2 (base réelle dans le dossier du programme) + autre base déjà présente dans `ProgramData\Autoprint\Data` : l'installeur s'arrête « Une base existe déjà… déplacement annulé pour ne rien écraser » ; empreintes des deux bases inchangées ; site toujours en 200. |
| SE-03 | ✅ | Serveur vierge : prérequis IIS, Serveur d'impression et runtime signalés absents, installés par l'assistant ; après nouvelle analyse : runtime 10.0.14 « Installé », sans bouton. |
| SE-01 | ✅ | Serveur avec runtime ASP.NET Core 10.0.0 (Hosting Bundle officiel) : « Version 10.0.0 trop ancienne : 10.0.14 requise (correctifs de sécurité) », bouton « Mettre à jour » ; « Suivant » refusé « Pré-requis manquants ». Libellé du message corrigé ensuite (il ne citait que les boutons « Installer »). |
| SE-02 | ✅ | « Mettre à jour » puis nouvelle analyse : « Installé (version 10.0.14) », les trois prérequis cochés. |
| P2-01 | ✅ | Installation neuve, nom public vide, `https://autoprint`, `autoprint:443` : refusés ; `srv-print.lab.autoprint.test` accepté. |
| PA-06 | ✅ | Installation neuve SQLite (aucun lieu, modèle « GÉNÉRIQUE ») : import d'une file du spouleur refusé « Créez au moins un lieu et un modèle avant d'importer des imprimantes ». |
| PA-02 | ✅ | Base SQLite remplacée par un fichier vide (installation précédente échouée) : au démarrage, le site recrée les tables (184 Ko), connexion `admin` / `admin123` d'une base neuve OK, sans relancer l'installeur. |
| S8-20 | ✅ après correctif (B74) | Désinstallation : service AutoprintSpooler arrêté et supprimé, dossier du programme supprimé, `ProgramData` conservé ; mais site IIS, pool et règle de pare-feu restaient (B74). Après correctif : supprimés à la désinstallation, conservés lors de deux mises à jour successives. |
| P1-02 | ✅ | Après fusion dans `master` (09/11) : aucune alerte Dependabot ouverte, n° 7 fermée. Avant fusion : PR brouillon vindbrann/Autoprint#6 vers `master` : « Contrôles de sécurité » (`build-and-audit`, audit des paquets vulnérables) réussi sur la branche. L'alerte Dependabot n° 7 (CVE-2026-96230, `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.0) reste ouverte car Dependabot n'analyse que `master` (10.0.14 sur la branche) : à vérifier fermée après la fusion. |
| P2-43 | ✅ après correctif (B75) | Après correctif (commits `2172ebf`, `fbe894e`) : CodeQL « No new alerts », toutes les vérifications de la PR au vert. Constat initial : 3 nouvelles alertes, analysées comme faux positifs. N° 16 (critique, LDAP, `SettingsController.cs:324`) : `TestUserQuery` déjà échappé par `SecurityHelper.EscapeLdapFilter`, que CodeQL ne reconnaît pas ; le filtre de base est saisi par l'administrateur (droit Paramètres). N° 17 et 18 (moyennes, journal, `SnmpProfilesController.cs:65` et `:90`) : adresse déjà validée par `IsValidHost` (IP ou nom DNS, sans retour à la ligne). Les 15 alertes de `master` (n° 1 à 15) ne réapparaissent pas sur la branche. Code adapté (B75). Le test du filtre AD dans la console n'a pas encore été rejoué en labo (prochaine version). |

## Campagne B79 / B32 / charte (branche `feature/charte-graphique`, installeur de test) — 11/11/2026

SRV-AD et SRV-PRINT seulement (CLT-TEST éteint). Installation SQL Server 26.11.9.1 mise à jour par l'installeur de test de la branche.

- **Installeur (charte B77)** : 1re version plantée au démarrage (`BuildRail` : `FontFamily = null` refusé par WPF), corrigée. Nouveau look validé (étapes à gauche, pied de page). À l'écran : message du runtime coupé (retour à la ligne ajouté), icônes manquantes (fenêtre : icône du logiciel ; exe : nouvelle icône d'installeur `autoprint-installeur.ico`), corrigés après coup, à revoir au prochain passage.
- **B79 (SE-01 / SE-02)** ✅ : runtime 10.0.14 signalé « trop ancienne : 10.0.15 requise » avec « Mettre à jour » ; après mise à jour : `Microsoft.AspNetCore.App 10.0.15` seul. « Mise à jour terminée ».
- **B32 (migration SQL Server)** ✅ : avant : `nvarchar(100)`, 2 communautés en clair, 63 lignes d'audit qui citent `snmpCommunity` ; après : migration `ChiffrementCommunauteSnmp`, `nvarchar(max)`, 2/2 communautés en `enc:v1:`, 63/63 lignes d'audit masquées. Supervision v2c en communauté propre (SV3-11) et sauvegarde / restauration (lot Données) OK.
- **SqlClient 7** ⏳ : **non exercé** par cette mise à jour (la connexion SQL de l'installeur ne sert qu'à l'installation neuve et au passage du site en compte sans privilège, déjà fait en 26.11.9.1). À couvrir par une installation neuve SQL Server (ou S8-10).
- **Lots API** (`-Profil sql`, HTTPS) : Comptes + Escalade 20, Sessions 5, Imprimantes 11, Web 12, AD 24, Données 18, Email 15, SNMP 21, Divers 16, Infra 6 : **148 OK, 0 échec**. Lots Clé et Serveur à jouer avec CLT-TEST.
- **Script de recette** : SV3-11 adapté à B32 (communauté jamais renvoyée par l'API, contrôlée chiffrée en base) ; lot AD : recyclage du pool si ad.user2 est encore bloqué par un P1-34 précédent. Port 8080 : redirige maintenant vers HTTPS (307), le jeton est perdu à la redirection : jouer la recette en HTTPS (adresse par défaut).
- **Restent** : recette visuelle de la console (charte, Radzen 12) dans le labo, client (CLT-TEST), lots Clé et Serveur, SqlClient 7.
## Contrôles à l'écran (navigateur intégré, console HTTPS de l'installation SQL Server) — 09/11/2026

Compte de test `ecran` (SuperAdmin) ; certificat du labo copié de l'instantané SQLite (liaison 443 ajoutée sur l'installation SQL Server).

| ID | Résultat | Remarque |
|---|---|---|
| P2-08, SE-07 | ✅ | Graphique en anneau « Répartition par modèle » (2 modèles), icônes, info-bulles ; Bootstrap et Chart.js chargés. |
| PA-04 | ✅ | 13 pages parcourues : aucune erreur dans la console du navigateur (seulement les messages « Authorization failed » de niveau information, B58 5). |
| PA-09, SE-06 | ✅ | Aucune ressource externe ni requête vers jsdelivr ; `index.html` en `Cache-Control: no-cache` ; bibliothèques en `?v=5.3.8`, `?v=1.13.2`, `?v=4.6.1`. |
| P2-10 | ✅ | `X-Frame-Options: DENY` et CSP `frame-ancestors 'none'` : affichage dans une iframe impossible. |
| SE-05 | ✅ | Thème sombre et tri de la grille Imprimantes conservés après rechargement ; jeton supprimé à la déconnexion (préférences conservées). |
| SD-06 | ✅ | Compte à changement imposé : fenêtre « Sécurité requise », Échap : retour à la page de connexion, aucun jeton stocké. |
| P2-09, P2-44 | ✅ | Avertissement « Sauvegarde : données sensibles » puis fichier `Autoprint_Backup_…json` (12,9 Ko) transmis à la fonction de téléchargement. |
| B57 (console) | ✅ | Fichier mal formé : « Ce fichier n'est pas une sauvegarde Autoprint lisible (fichier modifié ou endommagé ?) ». |
| SC-09 | ✅ | Sauvegarde sans secrets chargée : message sur la conservation des secrets du serveur et les conséquences sur un nouveau serveur (restauration non lancée). |
| B58 | ✅ | « Dernière connexion » en heure locale (00:04 pour une connexion à 00:04) ; compte Helpdesk : pas de bouton « Lier AD » sur SuperAdmin (présent sur les autres rôles). Constaté et corrigé : tableau de bord « 1 h » au lieu de « 1 min », « 1 erreurs ». |
| B69 | ✅ après correctif | Doublon de nom de rôle (« hd-suppr ») et faux « SuperAdmin » : refus 400. |
| P2-12, SD-09 | ✅ | CLT-TEST, client 26.11.8.2 (clé de l'installation SQL Server) : `Labo-Texte` et `Labo-PCL6` listées ; le client suit le thème de Windows (Windows non activé : thème sombre réglé par le registre de la session) : affichage sombre correct, fenêtre unique. |
| P2-27, P2-34 | ✅ | Zone technique (5 clics sur la version) : clé masquée `••••••••41d3` ; console › Sécurité › « Copier la clé » : clé complète (36 caractères) dans le presse-papiers, notification « Copiée », se termine par `41d3`. |
| P2-28 à P2-30 | ✅ | « Connexion réussie » ; fausse clé : « Clé refusée par le serveur. Comparez les 4 derniers caractères avec la console » ; serveur inexistant : « Serveur injoignable. Vérifiez le nom du serveur, le réseau et le pare-feu. » |
| P2-31 | ✅ | Adresse IP dans `PRINTSERVER` : liaison HTTPS avec SNI : « Serveur injoignable » (connexion fermée par le serveur, sans certificat présenté) ; liaison sans SNI : « Certificat du serveur refusé ». |
| P2-32 | ✅ | Certificat du labo retiré des racines du poste : « Certificat du serveur refusé ». Certificat remis ensuite. |
| B71 | ✅ après correctif | Deux icônes Autoprint sur le bureau (Bureau public par le MSI + Bureau de l'utilisateur par le client au premier lancement). MSI client final installé, premier lancement simulé : une seule icône. |## Tests restants joués en labo (SQL Server, 26.11.8.2) — 08/11/2026, nuit
| SC-02 | ✅ | Console ouverte à 00:12 (jeton valable jusqu'à 00:42) : renouvelé automatiquement à 00:37, nouvelle expiration 01:07, aucune déconnexion. |
| SE-04, PA-08 | ✅ | Mise à jour finale 26.11.8.2 (build `66873e3`, avec B65 à B71) par l'installeur, console ouverte : session conservée (API 200), thème sombre et tri de la grille conservés après rechargement ; écran du nom public non proposé, `AllowedHosts` conservé. Lots rejoués sur ce build : 119 OK + lot Divers rejoué seul (16 OK, échecs précédents dus à la limite de 10 connexions / minute du script) ; PA-12, SD-04 OK ; SD-13 (B66) avec **trois** imprimantes sur la même IP : création, mise à jour, renommage, réparation, suppression de l'une sans effet sur les deux autres. |

## Tests restants joués en labo (SQL Server, 26.11.8.2) — 08/11/2026, nuit

Lots `Divers` et `Infra` ajoutés à `Recette-Api.ps1` ; corrections B65 à B68 validées en remplaçant à chaud les fichiers du site et du service (validation par l'installeur à refaire avec le build final).

| ID | Résultat | Remarque |
|---|---|---|
| P1-05, PA-05 | ✅ | Scan du spouleur local, import de 2 files ; sans lieu ni modèle : statut « à corriger » ; réimport : « 1 ignorée(s) ». |
| P1-12 | ✅ | Nom devenu interdit (`Ancien/Nom*`) : modification du seul commentaire acceptée, renommage en nom interdit refusé. |
| P2-16, P2-42 | ✅ | Ancien mot de passe de 9 caractères accepté sans changement imposé ; ancien hachage SHA-256 : connexion puis changement imposé. |
| PA-14, PA-16 | ✅ | Case décochée : pas de changement imposé ; modification du seul nom : case conservée. |
| SC-03 | ✅ | Permission retirée au rôle : renouvellement du jeton refusé (401), reconnexion avec les nouveaux droits. |
| P2-19, P2-20, P2-21 | ✅ | Profil d'OID : IP sans imprimante « n'a pas répondu… », adresses `10.0.0.1; rm` et `<script>` refusées ; SMTP / AD erronés : « Hôte inconnu » / « The server could not be contacted ». |
| SD-04 | ✅ | Nom d'hôte de 50 caractères (accepté par Autoprint, refusé par Windows) : « Création du port IP_… impossible : Paramètre non valide ». |
| SD-05 | ✅ | Recherche du groupe AD « GG Test & #1 ». |
| SD-07 | ✅ | Autre origine : aucun en-tête `Access-Control-Allow-Origin` (GET et pré-vol). |
| SD-11, SE-08, SE-09 | ✅ | Rapport PDF de 63 imprimantes, mesures Compteur + Pannes : 40 Ko, 2 pages, bandeau, titre et accents corrects, en-tête répété page 2 ; `libSkiaSharp.dll` et licence de la police présents, aucune erreur. |
| SB-06 | ✅ après correctif (B67) | Spouleur suspendu : avant, 180 s et « The operation was canceled. » ; après : 60 s, « Le spouleur Windows n'a pas répondu en 60 s… », synchronisation interrompue ; après relance : la synchronisation aboutit. |
| SD-13 (B66) | ✅ après correctif | Constaté pendant SB-06 : la reprise d'une imprimante en erreur a **renommé la file `Labo-Texte`** (même IP), puis une suppression l'a fait disparaître. Après correctif : deux imprimantes sur la même IP (création, mise à jour, renommage, réparation, suppression) sans effet sur l'autre ; une file créée à la main n'est pas reprise. |
| SD-03 | ✅ | File interdite de suppression (refus explicite à SYSTEM) : la synchronisation signale l'échec. |
| PA-12 | ✅ après correctif (B68) | DC injoignable : avant, « Identifiants incorrects » (et échec compté) ; après : 503 « Annuaire Active Directory injoignable… » à chaque essai, connexion immédiate au retour. |
| PA-13 | ✅ | Base DN hors de l'utilisateur, rôle manuel Lecteur, membre d'un groupe lié à SuperAdmin : seulement `PRINTER_READ`. |
| PA-11 | ✅ | `ReverseProxy:KnownProxies` = l'hôte : 12 connexions de 12 IP (`X-Forwarded-For`) sans 429 ; sans l'en-tête, la limite par IP s'applique. |
| P2-37, P2-38 | ✅ | CLT-TEST : client 1.1.0 (service `AutoprintWorker`, tube `AutoprintPipe`) mis à jour en 26.11.8.2 : service supprimé, tube absent, clé conservée. |
| P1-01 | ✅ | `dotnet list package --vulnerable --include-transitive` : aucun paquet vulnérable (9 projets). |
| SD-10 | ✅ après correctif (B65) | 1ʳᵉ compilation en échec (fichier verrouillé) ; après correctif : 3 compilations consécutives réussies. |
| P3-24 | ✅ | Workflow « Contrôles de sécurité » vert sur tous les pushs de la branche. |
| P3-25 | ✅ (configuration) | `dependabot.yml` : NuGet hebdomadaire (groupe `Microsoft.*` / `System.*`), GitHub Actions mensuel. Affichage dans Insights : à l'écran. |
| P3-16 à P3-18, P3-20, S8-11 | ✅ | Droits (S8-07) : aucun utilisateur standard sur `appsettings.json`, `Keys`, `Data` ; console, rapports, synchronisation, AD, SMTP et clé agent fonctionnels après mise à jour. |
| P2-11, P3-23, P3-03 | sans objet | P2-11 : comportement remplacé par B17 (PA-07) ; P3-23 et P3-03 : versions / AD de production non disponibles. |
## Corrections avant fin de recette (26.11.8.2, build 7d5612d+) — 08/11/2026

Mise à jour de l'installation SQL Server du labo avec le build corrigé, puis tous les lots API rejoués (Escalade, Sessions, Imprimantes, Web, Données, Serveur, SNMPv3, E-mail, AD, Limitation) : **145 OK, 0 échec**.

| ID | Résultat | Remarque |
|---|---|---|
| P1-34 (B53) | ✅ | Seuil de verrouillage du domaine mis à 5 pour le test : 5 mauvais mots de passe AD → 401, 401, 401, 429, 429 ; dans l'AD : non verrouillé, `badPwdCount` = 3. Seuil remis à sa valeur d'origine. |
| P1-29 à P1-33 | ✅ | Comptes locaux inchangés : 5 × 401 puis 429 « 2 minute(s) », admin possible, limite par IP, déblocage après 2 min. |
| SI-04 client, P2-36 (B54) | ✅ | CLT-TEST, MSI client 26.11.8.2 en SYSTEM avec `/l*v` : mise à jour depuis 26.11.8.1, désinstallation, installation type Intune (`APIKEY=` en paramètre), réparation : **0 ligne** contenant la clé dans les 4 journaux (`APIKEY=**********`) ; clé conservée (mise à jour, réparation), supprimée (désinstallation). |
| P2-18 (B57) | ✅ | Sauvegarde corrompue : 400 « Fichier de sauvegarde invalide ou endommagé… Aucune donnée n'a été modifiée. », sans détail technique. Console : message clair à la lecture du fichier (à voir à l'écran). |
| B32 | ✅ | Communauté SNMP « ancien mode » : visible par `admin`, vide pour `lecteur` (liste et fiche) ; enregistrement avec une communauté vide : valeur conservée. |
| B58 | à l'écran | API : « Dernière connexion » envoyée en UTC sans fuseau (cas traité par la console). Affichage en heure locale et bouton « Lier AD » masqué sur SuperAdmin (compte sans Administration) : à vérifier à l'écran. |
## Mise à jour 1.1.2 (base réelle) → 26.11.8.2 — 08/11/2026

Base du serveur 1.1.0 de production, corrigée au schéma 1.1.2 (`Livraison-26.11.6.1\Correctif-base-1.1.0-vers-1.1.2`), placée sous une installation 1.1.2 en SQLite sur SRV-PRINT (l'installeur 1.1.2 seul échoue en SQLite sur une machine vierge : migrations écrites pour SQL Server, cf. B47), puis mise à jour par l'installeur 26.11.8.2. Vérifications faites **dans la VM** (la base contient des secrets de production) ; compte `admin` de la copie réinitialisé par `--reset-admin`.

| ID | Résultat | Remarque |
|---|---|---|
| SM-01 | ✅ | « Mise à jour terminée » ; base déplacée dans `C:\ProgramData\Autoprint\Data` ; journal : « Mise à niveau SQLite : 6 table(s) et 12 colonne(s) ajoutée(s) ». Pool en `ApplicationPoolIdentity`, démarré (B61) ; service AutoprintSpooler ; pare-feu 8080. |
| SM-02, NR-02 | ✅ | Imprimante, lieu SORGUES avec son réseau 10.22.200.0/24, modèle, 2 comptes, 4 rôles, 3 liaisons AD, 19 lignes d'audit conservés ; toutes les pages en 200. |
| SM-03 | ✅ | Lieu et imprimante créés, imprimante existante modifiée. |
| SM-05 | ✅ | Rôle Infra (« Administration » en 1.1.x) : Découverte, Rapports, Profils et identifiants SNMP, archivage / désarchivage. |
| SM-06, SM-07 | ✅ | Rôle avec « Rôles - écriture » : ajout de Scan réseau à son rôle puis Découverte accessible après reconnexion ; « Administration » forcée : 403 avec message, rien modifié. |
| P3-02 | ✅ | `AdServicePassword` et `AgentApiKey`, en clair en 1.1.x, chiffrés (`enc:v1:`) après la mise à jour. |
| SI-07 | ✅ après correctif (B63) | Premier essai : `Jwt:ExpireMinutes` resté à 120 (valeur 1.1.x). Mise à jour refaite avec le build corrigé : 30 dans la configuration, jeton émis de 30 min. |
| B64 | ✅ après correctif | `ResetPassword.bat` annonçait « admin123 » ; texte corrigé (mot de passe temporaire aléatoire, affiché une fois), vérifié après la mise à jour. |
| SM-04, P1-39, P2-41, P3-14 | sans objet | 26.11.4.1 / 26.08.24.x jamais installées ailleurs (décision du 08/11). P1-39 : AD de production injoignable depuis le labo. |
| S8-09, SB-02, PA-07, P2-02, SD-08 | ✅ | Même mise à jour : pool `LocalSystem` → `ApplicationPoolIdentity`, service installé et démarré, `Spooler:Mode=Service` ; base déplacée du dossier du programme vers `ProgramData` ; `AllowedHosts` `*` → nom public demandé par l'installeur (`srv-print.lab.autoprint.test;localhost`) ; port 8080 du site conservé. |
| S8-07, S8-08, SB-04 | ✅ | `appsettings.json` : SYSTEM, Administrateurs, `IIS APPPOOL\Autoprint` (lecture) ; `Keys` et `Data` : SYSTEM, Administrateurs, pool (modification) ; aucun droit explicite du pool sur le dossier du programme ; aucun droit des utilisateurs standard sur `Data`. || NR-05 (poste Intune réel) | ✅ (décision) | Validé par décision : un poste Intune réel n'apporterait rien de plus que le poste du labo (08/11). |
## Version 26.11.8.2 (en cours), installation neuve SQL Server 2025 Express — 08/11/2026

SRV-PRINT repartie de l'instantané « Labo configuré » ; SQL Server 2025 Express (instance `SQLEXPRESS`, TCP 1433, mode mixte) installé par `tools/lab/Install-LabSql.ps1` ; Autoprint installé par l'installeur graphique (SQL Server, authentification Windows). Tests API : `Recette-Api.ps1 -BaseUrl http://srv-print.lab.autoprint.test:8080 -Profil sql` (comptes Autoprint propres à cette installation : `recette-secrets-sql.xml`).

| ID | Résultat | Remarque |
|---|---|---|
| SI-05, NR-01 | ✅ après correctif (B59) | 26.11.8.1 : « Tester la connexion » en échec (base inexistante), puis installation en échec (« IIS AppPool\Autoprint not found »). 26.11.8.2 : base créée, installation sans erreur, console OK. |
| S8-04 | ✅ | Connexion `IIS AppPool\Autoprint`, `db_owner` sur la base `Autoprint`. |
| SD-01, SR-05 | ✅ (installation neuve) | 14 migrations, dont `AlignModelSnapshot` et `FusionPermissionsRoles` ; aucune erreur `[DbInitializer]`. |
| P1-06, P1-14 à P1-26, SR-04, P2-23 à P2-26, P2-03 à P2-06, P2-13 à P2-15 | ✅ | Mêmes résultats qu'en SQLite. |
| P1-34 à P1-46 | ✅ | AD (B53 inchangé : `badPwdCount` = 5). |
| SC-05 à SC-07, P2-35 | ✅ | Rotation de la clé agent. |
| P1-04, P1-07 à P1-11, NR-04, S8-13 | ✅ | Après redémarrage de SRV-PRINT : juste après l'installation de SQL Server (redémarrage en attente), la création de file par WMI en SYSTEM renvoyait « Accès refusé » (aussi hors Autoprint) ; non reproduit ensuite. |
| P3-08 à P3-13, NR-08, P2-39, NR-09 | ✅ | Sauvegarde / restauration sur SQL Server. P2-18 : refus 400 avec détail technique (B57, comme en SQLite). |
| S8-01 à S8-03, S8-12, S8-14 à S8-19, P3-01 | ✅ | B56 confirmé dans le build 26.11.8.2 (une ligne par commande du service). Secrets `enc:v1:` en base SQL. |
| P3-04 à P3-06, NR-06, NR-07, NR-10, PA-17 à PA-21 | ✅ | NR-10 sur SQL Server : planifications PDF / CSV conservées (7:30, hebdomadaire, seuil 14). |
| SV3-01 à SV3-16 | ✅ après correctif (B60) | Simulateur snmpsim sur SRV-AD (`tools/lab/Install-LabSnmp.ps1`) : .10 en SNMPv3 authPriv SHA-256/AES-128, .11 en v2c. Création du profil d'OID en 500 sur SQL Server (B60). Mot de passe v3 erroné : « Identifiants SNMPv3 refusés… », mot de passe absent des messages et journaux. SV3-05, SV3-07 (affichage, cycle, PDF) et SV3-14 sur un autre serveur : à l'écran / non joués. |
| SI-06, NR-02 (partiel) | ✅ après correctif (B61) | Mise à jour 26.11.8.2 → 26.11.8.2 (build avec B60) sur l'installation SQL Server : configuration et données conservées. Premier essai : console en **503**, pool IIS laissé arrêté (B61). Après correctif : pool démarré, console OK, règle de pare-feu 8080 en place. |
| SD-12, SD-01, SR-05, NR-10 (après mise à jour) | ✅ | DbInitializer a remis la valeur par défaut de `DateCreation` (B60, retirée avant la mise à jour) ; création de profil d'OID OK ; tous les lots rejoués après la mise à jour : 193 OK, aucun nouvel échec (P2-18 = B57). |## Version 26.11.8.1 (commit 7603b87), installation neuve SQLite — 08/11/2026
| S8-06 | ✅ | Installation neuve en **authentification SQL** (`autoprint.sql`, dbcreator) sur SQL Server vierge : base créée et possédée par `autoprint.sql`, **aucune connexion Windows ajoutée**, 14 migrations, console OK ; `appsettings.json` (mot de passe SQL en clair) lisible seulement par SYSTEM, Administrateurs et le pool (lecture). Lots rejoués (Comptes, Escalade, Sessions, Imprimantes, Données, Serveur, SNMPv3, E-mail, AD) : 167 OK ; seuls échecs : B57 (P2-18) et des effets d'ordre des lots du script, corrigés. |
| S8-05, B62 | ✅ | SQL Server **distant** (SQL Server 2025 Express sur SRV-AD, port 1433), authentification Windows : connexion `LAB\SRV-PRINT$` créée sur SRV-AD, `db_owner` sur la base, 14 migrations, console OK ; lots Comptes, Imprimantes, Données, SNMPv3 : 59 OK (échecs attendus : AD non configuré, B57). Port saisi d'abord avec la syntaxe SQL « serveur,1433 », jugée peu claire : champ **Port** ajouté (B62) et validé par une nouvelle installation. |

## Version 26.11.8.1 (commit 7603b87), installation neuve SQLite — 08/11/2026

| ID | Résultat | Remarque |
|---|---|---|
| SI-01 à SI-04 | ✅ | Console joignable depuis le poste hôte ; droits du pool posés ; règle « Autoprint Server » 8080 ; aucune erreur au démarrage. En 26.11.6.2 : site inutilisable (B51) et port fermé (B52). |
| PA-01, SD-02 | ✅ | Installation neuve SQLite : aucune erreur au démarrage (ni « duplicate column » ni `fail:` de schéma) ; console, rapports (NR-06), profils SNMP (NR-03), sauvegarde (NR-08) fonctionnels. |
| SR-03 | ✅ | Couvert par P1-15 à P1-21 (lot Escalade) : modifier / supprimer SuperAdmin, lier un groupe AD, modifier `admin`, attribuer SuperAdmin : refus avec message. |
| P1-06 | ✅ | `lecteur` : scan du spouleur refusé (403). |
| P1-14 à P1-17, P1-19 à P1-21 | ✅ | Refus 403 avec message pour chaque tentative de `helpdesk`. |
| P1-18 | ✅ | Modification de SuperAdmin : 403 ; suppression : 400 « Impossible de supprimer SuperAdmin » (refus). |
| P1-22 | ✅ | `admin` : tout est autorisé. |
| P1-23 à P1-26 | ✅ | `admin123` impose le changement ; 403 partout sauf le profil ; ancien jeton refusé ≤ 30 s après le changement. |
| P1-29 à P1-33 | ✅ | 5 × 401 puis 429 « Réessayez dans 2 minute(s) » ; `admin` reste possible ; 11ᵉ tentative par IP refusée ; déblocage après 2 min. |
| P2-23 à P2-26 | ✅ | Ancien jeton refusé ≤ 30 s après désactivation, changement de rôle, réinitialisation du mot de passe, modification des permissions du rôle ; reconnexion immédiate sans 401. |
| SR-04 | ✅ | Dernier administrateur : désactivation et retrait d'« Administration » refusés (400). |
| P1-04, P1-07 à P1-11, NR-04 | ✅ | Files créées, renommées, supprimées sur SRV-PRINT ; `Test' OR Name LIKE '%` ne touche que sa propre file ; noms et partages invalides refusés. Impression réelle vers la fausse imprimante (port 9100) reçue. |
| P2-03, P2-04, P2-06 | ✅ | 200 par le nom public ; 400 par l'IP et par un autre nom ; CSP, `X-Frame-Options: DENY`, `nosniff`, `no-referrer` présents (site en HTTP : pas de HSTS). |
| P2-13 à P2-15 | ✅ | 11 caractères, mot de passe = identifiant, même mot de passe : refusés avec message. |
| P2-03 à P2-06 en HTTPS | ✅ | Liaison 443 avec certificat du labo : HSTS présent ; autre nom refusé par IIS (404). |
| P1-34 | ⚠️ | Seuil du domaine à 0 : compte non verrouillé, mais `badPwdCount` = 5. **Avec un seuil de 5, le compte AD est verrouillé** par 5 essais dans Autoprint (B53). |
| P1-35 à P1-38 | ✅ | Rôle via groupe AD, retrait du groupe = refus « aucun droit », rôle manuel conservé, rôle admin ajouté puis retiré avec le groupe. |
| P1-40 à P1-46 | ✅ | Test AD sans ressaisie, refus d'un autre domaine sans mot de passe, `*` non interprété, filtre mal formé refusé, recherche des groupes. |
| P2-36 | ✅ | Clé agent depuis CLT-TEST : ni adresse IP, ni communauté SNMP, ni commentaire, ni numéro de série. |
| Client (Intune simulé) | ✅ | MSI silencieux sous SYSTEM avec `PRINTSERVER` et `APIKEY` : code 0, registre renseigné, HTTPS vers le serveur OK. |
| Journal MSI client | ⚠️ | Clé agent en clair dans `msiexec /l*v` (B54, partiellement corrigé). |
| `\\srv-print\print$` depuis CLT-TEST | ✅ / à finir | Refusé sans identifiants, accessible avec un compte AD (cas Entra ID hybride, B55). Connexion à l'imprimante à valider sur un vrai poste Intune (impossible depuis une session à distance). |

| SC-05 à SC-07, P2-35 | ✅ | Rotation avec transition (ancienne et nouvelle clés acceptées), révocation immédiate (anciennes clés refusées), 91 jours refusé, audit `AGENT_KEY_ROTATE` et `AGENT_KEY_REVEAL`. **La clé affichée par erreur le 08/11 est révoquée** ; CLT-TEST a reçu une clé neuve jamais affichée. |
| P3-08 à P3-13, NR-08, P2-40 | ✅ | Export sans secrets / protégé (`bkp:v1:`), mot de passe < 12 refusé, restauration refusée sans ou avec mauvais mot de passe sans rien modifier, restauration correcte : données, comptes locaux, rôle manuel AD, clé agent et mot de passe AD conservés. |
| P2-18 | ✅ (+ B57) | Fichier cohérent mais erroné : « Erreur pendant la restauration… (réf. 3894FF01…) ». Fichier mal formé : refus 400 avec le détail technique d'ASP.NET (B57). |
| P2-39, NR-09 | ✅ | Purge de l'audit ; export supervision avec jeton d'intégration (JSON), 401 sans jeton. |
| S8-01 à S8-03, S8-19, SC-01, SB-01, P3-07, P3-19 | ✅ | Service en Système local / automatique, pool en `ApplicationPoolIdentity`, `w3wp` sous `Autoprint`, `Spooler:Mode=Service`, JWT 30 min, base dans ProgramData, clé de chiffrement, pas de NETWORK SERVICE. |
| S8-12 | ✅ après correctif (B56) | Avant : aucune ligne par commande dans le journal. Après remplacement du service corrigé : « Commande … : OK ». |
| S8-13 à S8-15, SB-05 | ✅ | Pilote absent : « Pilote manquant » ; service arrêté : lectures OK, synchronisation refusée avec message clair, état « arrêté » au tableau de bord ; redémarré : synchronisation OK. |
| S8-17, S8-18 | ✅ | Tube refusé à un administrateur local (ni SYSTEM ni le pool) et depuis un autre poste. |
| P3-01 | ✅ | `AgentApiKey` et `AdServicePassword` en `enc:v1:` dans la base (SMTP non configuré). |

| P2-22 | ✅ (écran) | Pas de case « Retenir l'identifiant » ; identifiant vide. |
| SR-01, SR-02, P1-17, SC-08 (écran, `helpdesk`) | ✅ | Pas de colonne « Suppr. » pour Rôles ; « Administration » et « Système » grisées, le reste cochable ; bandeau « Enregistrer ce rôle ferme les sessions… ». |
| P1-13 (écran) | ✅ | Rôles proposés à `helpdesk` : Helpdesk, Lecteur, HD-Suppr (pas SuperAdmin). |
| P2-13, PA-15 (écran) | ✅ | « Le mot de passe doit faire au moins 12 caractères » avant envoi ; case « changement obligatoire » cochée par défaut. |
| P1-15 (écran) | ✅ | « Erreur Modification : Ce compte a « Administration » ou « Paramètres » : vous ne pouvez pas le modifier. » (B49). |
| P2-07 (pages de `helpdesk`) | ✅ | Aucune erreur CSP dans la console du navigateur. |

| NR-03, P2-07 (écran, `admin`) | ✅ | Toutes les pages (tableau de bord, imprimantes, lieux, marques, modèles, pilotes, profils et identifiants SNMP, rapports, paramètres, utilisateurs, audit, rôles) s'affichent ; aucune erreur CSP. |
| P2-33 (écran) | ✅ | Clé masquée (●●●●●●●●), pas d'interrupteur « Modification autorisée », bouton « Copier la clé » avec mention de l'audit. |
| P2-44 (écran) | ✅ | Avertissement « Sauvegarde : données sensibles » ; « Annuler » : aucune requête d'export. |
| P3-21, P3-22 (écran) | ✅ | TLS activé par défaut ; bandeau « Sans TLS… en clair » quand on le décoche (non enregistré). |
| P1-03 (écran) | ✅ | Onglet « Ce serveur » : plus de champs serveur / utilisateur / mot de passe, un seul bouton. |

| P3-04 à P3-06 | ✅ | SMTP (faux serveur du labo, STARTTLS + authentification) : mot de passe chiffré en base (`enc:v1:`) ; test avec mot de passe masqué = mot de passe enregistré utilisé ; autre serveur sans ressaisie refusé. |
| NR-06, NR-07, NR-10 (SQLite) | ✅ | « Envoyer maintenant » : e-mail avec un vrai PDF (37 Ko) / un CSV UTF-8 avec en-têtes ; planification hebdomadaire (heure, minute, seuil) conservée. |
| PA-17 à PA-21 | ✅ | Lien `https://srv-print.lab.autoprint.test/reset-password?token=…` ; même message et aucun e-mail pour un inconnu ou un compte AD ; 11 caractères refusé ; nouveau mot de passe OK ; audit `USER_PWD_RESET_REQUEST` / `USER_PWD_RESET` ; lien réutilisé refusé ; base : empreinte hexadécimale (64) ≠ jeton. Anti-spam : une nouvelle demande est ignorée pendant 5 min, même si l'envoi précédent a échoué. |

### À vérifier à l'écran

- ~~Compteurs à 0 de la synchronisation~~ : non utilisés par la console (seuls les messages sont affichés) : pas un problème.
- Message affiché par la console pour une sauvegarde mal formée (B57) : nécessite d'envoyer un fichier depuis le navigateur, à faire à la main.

### Anomalies

| # | Statut | Constat |
|---|---|---|
| B51 | Corrigé en 26.11.8.1 | Installation neuve inutilisable (droits du pool IIS). |
| B52 | Corrigé en 26.11.8.1 | Port du site non ouvert dans le pare-feu. |
| B53 | Corrigé en 26.11.8.2 | 5 mauvais mots de passe dans Autoprint verrouillaient le compte AD : comptes annuaire limités à 3 échecs, blocage 30 min. |
| B54 | Corrigé en 26.11.8.2 | Clé agent dans le journal d'installation du client. |
| B56 | Corrigé (prochaine livraison) | Aucune ligne par commande du service dans le journal Windows. |
| B58 | Mineur | Affichage : heures en UTC, dernière connexion perdue à la restauration, `admin` sans e-mail, bouton « LIER AD » sur SuperAdmin. |
| B57 | Corrigé en 26.11.8.2 | Message technique pour une sauvegarde mal formée. |
| B59 | Corrigé en 26.11.8.2 | Installation neuve impossible sur un SQL Server vierge (base inexistante, droits accordés avant la création du pool). |
| B60 | Corrigé en 26.11.8.2 | SQL Server : création d'un profil d'OID avec éléments en erreur 500 (colonne `DateCreation`). |
| B61 | Corrigé en 26.11.8.2 | Après chaque mise à jour, pool IIS laissé arrêté : console en erreur 503. |
| B62 | Fait en 26.11.8.2 | Installeur : champ Port pour SQL Server (au lieu de « serveur,1433 »). |
| B63 | Corrigé en 26.11.8.2 | Serveurs mis à jour : jetons de 120 min conservés au lieu de 30. |
| B64 | Fait en 26.11.8.2 | Texte de ResetPassword.bat (« admin123 »). |
| B65 | Corrigé en 26.11.8.2 | Compilation parallèle de la solution parfois en échec. |
| B66 | Corrigé en 26.11.8.2 | La synchronisation pouvait renommer puis supprimer la file d'une autre imprimante de même IP. |
| B67 | Corrigé en 26.11.8.2 | Spouleur bloqué : 60 s par opération et message technique. |
| B68 | Corrigé en 26.11.8.2 | DC injoignable : « Identifiants incorrects » et échec compté. |
| B69 | Corrigé en 26.11.8.2 | Plusieurs rôles du même nom (dont un faux « SuperAdmin »). |
| B70 | Mineur | Liste de débogage des pilotes en avertissement dans le journal. |
| B71 | Corrigé en 26.11.8.2 | Deux icônes Autoprint sur le bureau. |
| B72 | Corrigé en 26.11.8.2 | Clés de chiffrement perdues : restauration refusée (« mot de passe incorrect »), postes en erreur 500. |
| B73 | Corrigé en 26.11.8.2 | Mise à jour sans droit SQL : script non copiable, échec de la mise à jour. |
| B74 | Corrigé en 26.11.8.2 | Désinstallation : site IIS, pool et règle de pare-feu laissés en place. |
| B55 | Pas un bug | Accès à `print$` par Kerberos en Entra ID hybride ; installation d'imprimante à valider sur un poste réel. |
