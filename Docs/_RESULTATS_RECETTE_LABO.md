# Résultats de la recette en labo

Recette de `Docs/_PROCEDURE_TESTS_SECURITE.md` sur le labo Hyper-V (`tools/lab/README.md`) : Windows Server 2022 **français**, domaine `lab.autoprint.test`, poste Windows 11 hors domaine.
Tests **[API]** joués par `tools/lab/Recette-Api.ps1` ; les autres à la main ou dans le navigateur.

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
| `\srv-print\print$` depuis CLT-TEST | ⚠️ | Inaccessible sans identifiants (poste hors domaine) : installation d'imprimantes par le client à confirmer (B55). |

### À vérifier à l'écran

- Synchronisation du spouleur : le compte rendu de l'API a des compteurs à 0 (`totalTraites`, `added`, `updated`, `deleted`) alors que les messages listent les opérations faites. Vérifier ce qu'affiche la console.

### Anomalies

| # | Statut | Constat |
|---|---|---|
| B51 | Corrigé en 26.11.8.1 | Installation neuve inutilisable (droits du pool IIS). |
| B52 | Corrigé en 26.11.8.1 | Port du site non ouvert dans le pare-feu. |
| B53 | À décider | 5 mauvais mots de passe dans Autoprint verrouillent le compte AD (seuil de domaine à 5). |
| B54 | Partiellement corrigé | Clé agent dans le journal d'installation du client. |
| B55 | À confirmer | Accès à `print$` depuis un poste hors domaine. |
