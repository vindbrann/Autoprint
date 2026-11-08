# Résultats de la recette en labo

Recette de `Docs/_PROCEDURE_TESTS_SECURITE.md` sur le labo Hyper-V (`tools/lab/README.md`) : Windows Server 2022 **français**, domaine `lab.autoprint.test`, poste Windows 11 hors domaine.
Tests **[API]** joués par `tools/lab/Recette-Api.ps1` ; les autres à la main ou dans le navigateur.

## ▶️ Reprise de la recette (état au 08/11/2026, soir)

**État du labo** :
- SRV-PRINT : base existante 1.1.x mise à jour en 26.11.8.2 (SQLite). Instantanés : « Serveur en 1.1.2 (base existante corrigée) », « Avant installation 1.1.2 (SM) », « S8-05 + B62 validés (SQL distant SRV-AD) », « SQL Server 26.11.8.2 authentification SQL (S8-06) », « SQL Server 26.11.8.2 mis à jour (B61 validé) », « SQL Server 26.11.8.2 avant mise à jour B60 », « Autoprint 26.11.8.2 SQL Server (recette en cours) », « SQL Server 2025 installé (sans Autoprint) », « Recette SQLite 10-11 (avant SQL Server) » (installation SQLite 26.11.8.1 des tests précédents, HTTPS 443), plus les anciens (`OS installé`, `Labo configuré`, `Autoprint 26.11.8.1 installé`…).
- SRV-AD : SQL Server 2025 Express (`SQLEXPRESS`, port 1433, test S8-05 ; instantané « Avant SQL Server (S8-05) »), simulateur SNMP (tâche « Autoprint-SnmpSim », 192.168.99.10 v3 / .11 v2c), faux SMTP, fausse imprimante 9100.
- Après une restauration d'instantané : réactiver « Interface de services d'invité » (`Copy-VMFile`) ; l'installation SQL Server n'a ni HTTPS ni pare-feu « Partage de fichiers » (tests CLT-TEST des lots Clé et Serveur : en SQLite seulement).

**Pour reprendre** :
1. Hyper-V : démarrer `SRV-AD`, attendre ~1 min, puis `SRV-PRINT` et `CLT-TEST` (ou demander à Claude : « reprends la recette du labo »).
2. Console : `http://srv-print.lab.autoprint.test:8080` (installation SQL Server) ; `https://srv-print.lab.autoprint.test` sur l'instantané SQLite.
3. Tests API : `tools\lab\Recette-Api.ps1 -Lot <Lot>` (SQLite, HTTPS) ou `-BaseUrl http://srv-print.lab.autoprint.test:8080 -Profil sql` (SQL Server). Lots : `Comptes`, `Escalade`, `Sessions`, `Imprimantes`, `Web`, `AD`, `Cle`, `Donnees`, `Serveur`, `Email`, `Snmp`, `Limitation`.

**Fichiers locaux (hors dépôt, chiffrés pour le compte Windows, jamais affichés)** dans `%USERPROFILE%\.autoprint-lab\` :
`labadmin.cred`, `recette-secrets.xml` (comptes du labo et de l'installation SQLite ; `sql.sa`, `sql.autoprint`, `snmp.v3.auth`, `snmp.v3.priv`), `recette-secrets-sql.xml` (comptes Autoprint de l'installation SQL Server), `srv-print.cer`, `dl\` (SQL Server 2025 Express, Python portable avec snmpsim), `snmpsim-venv\`.

**Reste à tester** :
- À l'écran : SV3-05, SV3-07 (cycle de supervision, PDF), B57 ; P1-27, P1-28, P2-17, PA-03, PA-10.

**Décisions en attente** : B53 (verrouillage AD par Autoprint), B54 (fin de la correction du journal MSI client), livraison 26.11.8.2 (B54 partiel, B56, B59 à B64).

## Mise à jour 1.1.2 (base existante) → 26.11.8.2 — 08/11/2026

Base du serveur 1.1.0 de production, corrigée au schéma 1.1.2 (`Livraison-26.11.6.1\Correctif-base-1.1.0-vers-1.1.2`), placée sous une installation 1.1.2 en SQLite sur SRV-PRINT (l'installeur 1.1.2 seul échoue en SQLite sur une machine vierge : migrations écrites pour SQL Server, cf. B47), puis mise à jour par l'installeur 26.11.8.2. Vérifications faites **dans la VM** (la base contient des données sensibles) ; compte `admin` de la copie réinitialisé par `--reset-admin`.

| ID | Résultat | Remarque |
|---|---|---|
| SM-01 | ✅ | « Mise à jour terminée » ; base déplacée dans `C:\ProgramData\Autoprint\Data` ; journal : « Mise à niveau SQLite : 6 table(s) et 12 colonne(s) ajoutée(s) ». Pool en `ApplicationPoolIdentity`, démarré (B61) ; service AutoprintSpooler ; pare-feu 8080. |
| SM-02, NR-02 | ✅ | Imprimante, lieu avec son réseau, modèle, 2 comptes, 4 rôles, 3 liaisons AD, 19 lignes d'audit conservés ; toutes les pages en 200. |
| SM-03 | ✅ | Lieu et imprimante créés, imprimante existante modifiée. |
| SM-05 | ✅ | Rôle Infra (« Administration » en 1.1.x) : Découverte, Rapports, Profils et identifiants SNMP, archivage / désarchivage. |
| SM-06, SM-07 | ✅ | Rôle avec « Rôles - écriture » : ajout de Scan réseau à son rôle puis Découverte accessible après reconnexion ; « Administration » forcée : 403 avec message, rien modifié. |
| P3-02 | ✅ | `AdServicePassword` et `AgentApiKey`, en clair en 1.1.x, chiffrés (`enc:v1:`) après la mise à jour. |
| SI-07 | ✅ après correctif (B63) | Premier essai : `Jwt:ExpireMinutes` resté à 120 (valeur 1.1.x). Mise à jour refaite avec le build corrigé : 30 dans la configuration, jeton émis de 30 min. |
| B64 | ✅ après correctif | `ResetPassword.bat` annonçait « admin123 » ; texte corrigé (mot de passe temporaire aléatoire, affiché une fois), vérifié après la mise à jour. |
| SM-04, P1-39, P2-41, P3-14 | sans objet | 26.11.4.1 / 26.08.24.x jamais installées ailleurs (décision du 08/11). P1-39 : AD autre que celui du labo injoignable depuis le labo. |
| NR-05 (poste Intune réel) | ✅ (décision) | Validé par décision : un poste Intune réel n'apporterait rien de plus que le poste du labo (08/11). |
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
| SD-04, SD-01, SR-05, NR-10 (après mise à jour) | ✅ | DbInitializer a remis la valeur par défaut de `DateCreation` (B60, retirée avant la mise à jour) ; création de profil d'OID OK ; tous les lots rejoués après la mise à jour : 193 OK, aucun nouvel échec (P2-18 = B57). |## Version 26.11.8.1 (commit 7603b87), installation neuve SQLite — 08/11/2026
| S8-06 | ✅ | Installation neuve en **authentification SQL** (`autoprint.sql`, dbcreator) sur SQL Server vierge : base créée et possédée par `autoprint.sql`, **aucune connexion Windows ajoutée**, 14 migrations, console OK ; `appsettings.json` (mot de passe SQL en clair) lisible seulement par SYSTEM, Administrateurs et le pool (lecture). Lots rejoués (Comptes, Escalade, Sessions, Imprimantes, Données, Serveur, SNMPv3, E-mail, AD) : 167 OK ; seuls échecs : B57 (P2-18) et des effets d'ordre des lots du script, corrigés. |
| S8-05, B62 | ✅ | SQL Server **distant** (SQL Server 2025 Express sur SRV-AD, port 1433), authentification Windows : connexion `LAB\SRV-PRINT$` créée sur SRV-AD, `db_owner` sur la base, 14 migrations, console OK ; lots Comptes, Imprimantes, Données, SNMPv3 : 59 OK (échecs attendus : AD non configuré, B57). Port saisi d'abord avec la syntaxe SQL « serveur,1433 », jugée peu claire : champ **Port** ajouté (B62) et validé par une nouvelle installation. |

| ID | Résultat | Remarque |
|---|---|---|
| SI-01 à SI-04 | ✅ | Console joignable depuis le poste hôte ; droits du pool posés ; règle « Autoprint Server » 8080 ; aucune erreur au démarrage. En 26.11.6.2 : site inutilisable (B51) et port fermé (B52). |
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
| B53 | À décider | 5 mauvais mots de passe dans Autoprint verrouillent le compte AD (seuil de domaine à 5). |
| B54 | Partiellement corrigé | Clé agent dans le journal d'installation du client. |
| B56 | Corrigé (prochaine livraison) | Aucune ligne par commande du service dans le journal Windows. |
| B58 | Mineur | Affichage : heures en UTC, dernière connexion perdue à la restauration, `admin` sans e-mail, bouton « LIER AD » sur SuperAdmin. |
| B57 | Mineur | Message technique pour une sauvegarde mal formée. |
| B59 | Corrigé en 26.11.8.2 | Installation neuve impossible sur un SQL Server vierge (base inexistante, droits accordés avant la création du pool). |
| B60 | Corrigé en 26.11.8.2 | SQL Server : création d'un profil d'OID avec éléments en erreur 500 (colonne `DateCreation`). |
| B61 | Corrigé en 26.11.8.2 | Après chaque mise à jour, pool IIS laissé arrêté : console en erreur 503. |
| B62 | Fait en 26.11.8.2 | Installeur : champ Port pour SQL Server (au lieu de « serveur,1433 »). |
| B63 | Corrigé en 26.11.8.2 | Serveurs mis à jour : jetons de 120 min conservés au lieu de 30. |
| B64 | Fait en 26.11.8.2 | Texte de ResetPassword.bat (« admin123 »). |
| B55 | Pas un bug | Accès à `print$` par Kerberos en Entra ID hybride ; installation d'imprimante à valider sur un poste réel. |
