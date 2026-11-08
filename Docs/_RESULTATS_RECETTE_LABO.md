# Résultats de la recette en labo

Recette de `Docs/_PROCEDURE_TESTS_SECURITE.md` sur le labo Hyper-V (`tools/lab/README.md`) : Windows Server 2022 **français**, domaine `lab.autoprint.test`, poste Windows 11 hors domaine.
Tests **[API]** joués par `tools/lab/Recette-Api.ps1` ; les autres à la main ou dans le navigateur.

## ▶️ Reprise de la recette (état au 08/11/2026, fin de soirée)

**État du labo** : instantané « Fin de session 08-11 (recette en cours) » sur chacune des 3 VM ; SRV-PRINT : aussi « Avant tests sauvegarde 09-11 ». Le service AutoprintSpooler de SRV-PRINT est la version corrigée B56 (original : `C:\Windows\Temp\AutoprintSpooler.exe.orig`).
Autres instantanés : `OS installé` (Windows nu), `Labo configuré` (domaine, réseau, avant Autoprint), `Autoprint 26.11.8.1 installé` (SRV-PRINT seul, juste après l'installation, avant les tests).

**Pour reprendre** :
1. Hyper-V : démarrer `SRV-AD`, attendre ~1 min, puis `SRV-PRINT` et `CLT-TEST` (ou demander à Claude : « reprends la recette du labo »).
2. Console : `https://srv-print.lab.autoprint.test` (certificat auto-signé du labo : non reconnu par le navigateur de l'hôte ; reconnu sur CLT-TEST). En HTTP, `:8080` redirige vers HTTPS.
3. Tests API : `tools\lab\Recette-Api.ps1 -Lot <Lot>` (session normale). Lots : `Comptes`, `Escalade`, `Sessions`, `Imprimantes`, `Web`, `AD`, `Limitation`.

**Fichiers locaux (hors dépôt, chiffrés pour le compte Windows, jamais affichés)** dans `%USERPROFILE%\.autoprint-lab\` :
`labadmin.cred` (Administrateur des VM / `LAB\Administrateur`, `CLT-TEST\LabAdmin`), `recette-secrets.xml` (mots de passe de `admin`, `helpdesk`, `lecteur`, `svc.autoprint`, `ad.user1`, `ad.user2`…), `srv-print.cer` (certificat du labo, accepté par empreinte par le script).

**Configuré à la main pendant la recette (pas encore dans `Configure-Lab.ps1`)** : liaison HTTPS 443 sur le site Autoprint avec un certificat auto-signé `srv-print.lab.autoprint.test`, règle de pare-feu « LABO : Autoprint HTTPS 443 », certificat ajouté aux autorités racine de CLT-TEST ; client Autoprint 26.11.8.1 (MSI corrigé B54) installé sur CLT-TEST sous SYSTEM ; dans Autoprint : rôles `Helpdesk`, `Lecteur`, `HD-Suppr`, comptes `helpdesk`, `lecteur`, `hd.cree`, `ad.user2` (rôle Lecteur manuel), liaisons `GG_Autoprint_Lecteurs` → Lecteur et `GG_Autoprint_Admins` → SuperAdmin, AD configuré (`svc.autoprint`), lieu `Labo` (192.168.99.0/24), imprimante `Labo-Texte` → fausse imprimante 192.168.99.10:9100. Dans l'AD : `ad.user1` n'est plus dans `GG_Autoprint_Lecteurs` (retiré par P1-36).

**À faire en premier** : ~~changer la clé agent~~ fait le 08/11 (SC-07).

**Ancienne consigne** :
- **Changer la clé agent** (Paramètres › Sécurité) : la clé du labo a été affichée par erreur dans une sortie de commande pendant la vérification de B54. Puis mettre à jour `HKLM\SOFTWARE\Autoprint\APIKEY` sur CLT-TEST (tests SC de changement de clé).

**Reste à tester** :
- Non-régression : NR-03 (toutes les pages), NR-05 (client : lieu détecté, imprimantes listées et installées : **sur un poste Intune réel**, cf. B55), NR-06/NR-07 (rapports PDF/CSV : nécessite un serveur SMTP de test), NR-08 (sauvegarde/restauration), NR-09 (export supervision), NR-01/NR-10 (SQL Server : non installé dans le labo).
- Phase 1 : P1-03, P1-05, P1-12, P1-13 (écran), P1-27, P1-28, P1-39 (mise à jour d'un serveur existant).
- Phase 2 : P2-01, P2-02, P2-05, P2-07 à P2-12, P2-16 à P2-22, P2-27 à P2-35, P2-37 à P2-44.
- Phase 3 et suivantes : sections 4 à 5.10 de la procédure (secrets chiffrés, sauvegarde des secrets, droits NTFS, SMTP, phases A à E, SNMPv3, mises à jour depuis 1.1.x, SM-*, SR-01 à SR-03, SR-05), désinstallation S8-20.
- À l'écran : compteurs de la synchronisation du spouleur (voir plus bas).

**Décisions en attente** : B53 (verrouillage AD par Autoprint), B54 (fin de la correction du journal MSI client), puis une livraison 26.11.8.x avec le MSI client corrigé (la livraison `Livraison-26.11.8.1` contient encore l'ancien MSI client).

## Version 26.11.8.1 (commit 7603b87), installation neuve SQLite — 08/11/2026

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

### À vérifier à l'écran

- ~~Compteurs à 0 de la synchronisation~~ : non utilisés par la console (seuls les messages sont affichés) : pas un problème.
- Message affiché par la console pour une sauvegarde mal formée (B57).

### Anomalies

| # | Statut | Constat |
|---|---|---|
| B51 | Corrigé en 26.11.8.1 | Installation neuve inutilisable (droits du pool IIS). |
| B52 | Corrigé en 26.11.8.1 | Port du site non ouvert dans le pare-feu. |
| B53 | À décider | 5 mauvais mots de passe dans Autoprint verrouillent le compte AD (seuil de domaine à 5). |
| B54 | Partiellement corrigé | Clé agent dans le journal d'installation du client. |
| B56 | Corrigé (prochaine livraison) | Aucune ligne par commande du service dans le journal Windows. |
| B57 | Mineur | Message technique pour une sauvegarde mal formée. |
| B55 | Pas un bug | Accès à `print$` par Kerberos en Entra ID hybride ; installation d'imprimante à valider sur un poste réel. |
