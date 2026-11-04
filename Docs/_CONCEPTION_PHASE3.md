# 📐 Conception — Phase 3 : sortie de `LocalSystem` (#8) et SNMPv3 (#14)

**Statut :** à valider avant développement · **Date :** 04/11/2026
Référence : `_AUDIT_SECURITE_2026-11.md`, points #8 et #14.

Ces deux points ne sont pas des correctifs mais des évolutions : ils changent l'installation (#8) ou ajoutent une fonctionnalité (#14). Ce document décrit ce qui serait fait, ce que cela change pour l'exploitation, et les décisions à prendre.

---

## 1. Sortir le site IIS de `LocalSystem` (#8)

### 1.1 Situation actuelle

Le pool IIS « Autoprint » tourne en `LocalSystem`. Tout le serveur web (API, console, tâches planifiées) a donc les droits maximum sur la machine, et s'authentifie sur le réseau avec le **compte machine** du serveur. Une seule faille dans le code ou dans une dépendance donne le contrôle total du serveur d'impression.

`LocalSystem` n'est réellement nécessaire que pour **les opérations d'administration du spouleur** :

| Opération | Où | Droit requis |
|---|---|---|
| Créer un port TCP/IP | `CreerPortTcp` | Administrateur |
| Créer / modifier / renommer / supprimer une file | `CreerImprimante`, `ModifierImprimante`, `RenommerImprimante`, `SupprimerImprimante` | Administrateur (`PRINTER_ACCESS_ADMINISTER`) |
| Activer le mode direct (registre du pilote + attributs) | `SetDirectPrintingMode` | Administrateur |
| Lire les files, ports, pilotes, état du spouleur | `GetServerSnapshotAsync`, `ScanLocalPrintersAsync`, `GetInstalledDriversAsync`, `VerifierModeDirect`, tableau de bord | Utilisateur standard suffisant |

Le reste (base SQL, SNMP, SMTP, LDAP avec compte de service, rapports PDF) n'a besoin d'aucun privilège.

### 1.2 Architecture proposée

```
 Navigateurs / postes ──HTTPS──▶  IIS, pool « Autoprint »                     Service Windows « AutoprintSpooler »
                                  identité : ApplicationPoolIdentity           compte : LocalSystem
                                  (aucun droit d'administration)               (seul composant privilégié)
                                  API, console, SNMP, SMTP, LDAP, rapports ──▶ tube nommé local, ACL limitée à
                                                                                l'identité du pool « IIS AppPool\\Autoprint »
                                                                                5 commandes typées, paramètres validés
```

* **Site web** : pool en `ApplicationPoolIdentity` (`IIS AppPool\Autoprint`). Plus aucun privilège local. Sur le réseau, il s'authentifie avec le compte machine uniquement pour SQL Server en authentification Windows (voir 1.4).
* **Service `AutoprintSpooler`** (nouveau projet, sur le **serveur** uniquement, à ne pas confondre avec l'ancien service client supprimé) :
  * tube nommé local `\\.\pipe\AutoprintSpooler`, **refuse toute connexion réseau** et n'accepte que l'identité du pool (vérification de l'ACL **et** du SID de l'appelant) ;
  * 5 commandes fixes : `CreatePort(ip)`, `CreatePrinter(nom, pilote, ip, partage, direct)`, `UpdatePrinter(…)`, `RenamePrinter(ancien, nouveau)`, `DeletePrinter(nom)` ;
  * chaque paramètre est revalidé côté service (IP valide, nom conforme aux règles du spouleur, pilote **déjà installé**) : même si le site web était compromis, le service ne peut que gérer des files d'impression, jamais exécuter une commande ou installer un pilote arbitraire ;
  * code réutilisé : les fonctions Win32 / WMI de `WindowsPrintSpoolerService` y sont déplacées telles quelles (avec l'échappement WQL de la phase 1).
* Côté site, `WindowsPrintSpoolerService` garde les lectures et délègue les 5 écritures au service via un client du tube.

### 1.3 Ce que cela change

| Domaine | Avant | Après |
|---|---|---|
| Faille dans le site web | Contrôle total du serveur (SYSTEM) | Accès limité au compte du pool ; au pire, création / suppression de files d'impression via le service |
| Installation | MSI serveur + IIS | MSI serveur + IIS + **installation d'un service Windows** (le MSI s'en charge) |
| SQL Server en authentification Windows | Droits accordés à `NT AUTHORITY\SYSTEM` | Droits accordés à `IIS AppPool\Autoprint` (SQL local) ou au **compte machine** `DOMAINE\SERVEUR$` (SQL distant) : l'installeur doit le gérer |
| Clés de chiffrement (phase 3) | SYSTEM | Droits à donner à `IIS AppPool\Autoprint` (l'installeur le fait) |
| Mise à jour d'un serveur existant | — | L'installeur bascule l'identité du pool, installe le service, recalcule les droits SQL et NTFS |
| Dépannage | Un seul processus | Deux composants : si le service est arrêté, la synchronisation échoue avec un message explicite (« service AutoprintSpooler arrêté ») |

### 1.4 Décisions à prendre

1. **Identité du pool** : `ApplicationPoolIdentity` (recommandé, rien à gérer) ou un **gMSA** du domaine (si vous voulez maîtriser l'identité réseau, par ex. pour SQL distant) ?
2. **SQL Server distant en authentification Windows** : le cas existe-t-il chez vos clients ? (Cela impose de donner les droits SQL au compte machine du serveur Autoprint.)
3. **Calendrier** : l'évolution touche l'installeur, l'installation et la mise à jour ; elle mérite une **version dédiée** (ex. 26.11) testée séparément.

**Estimation :** nouveau projet service + client du tube + adaptation de l'installeur (identité du pool, service, SQL, NTFS) : environ 2 à 3 jours de développement, plus la recette d'installation et de mise à jour.

---

## 2. SNMPv3 (#14)

### 2.1 Situation actuelle

La supervision des imprimantes utilise SNMP v1 / v2c : la « communauté » circule **en clair** sur le réseau (souvent `public`), et elle est stockée en clair en base, par imprimante. La bibliothèque utilisée (`Lextm.SharpSnmpLib`) prend déjà en charge SNMPv3.

### 2.2 Proposition

* Nouvelle option « Version SNMP : v3 » dans la fiche imprimante, à côté de v1 / v2c (qui restent disponibles).
* Champs v3 : utilisateur, niveau de sécurité (`authPriv` recommandé, `authNoPriv`), protocole d'authentification (SHA-256 / SHA-1), mot de passe d'authentification, protocole de chiffrement (AES-128 / AES-256), mot de passe de chiffrement.
* **Profils d'identifiants réutilisables** (« Identifiants SNMP ») plutôt que des mots de passe saisis imprimante par imprimante : on sélectionne un profil dans la fiche. Les mots de passe sont chiffrés en base comme les autres secrets (phase 3).
* Les 12 appels SNMP du code passent par une seule fonction qui choisit v1 / v2c / v3 selon l'imprimante.
* Au passage : **les communautés v1 / v2c sont chiffrées en base** elles aussi.

### 2.3 Ce que cela change

* Rien pour les imprimantes existantes (elles restent en v1 / v2c).
* Pour passer une imprimante en v3, il faut d'abord configurer l'utilisateur SNMPv3 **sur l'imprimante** (serveur web embarqué du fabricant), puis dans Autoprint.
* Le scan réseau de découverte reste en v1 / v2c (il interroge des adresses inconnues).

### 2.4 Décision (04/11/2026)

* **Profils d'identifiants SNMP** réutilisables (communauté v1 / v2c ou identifiants v3), secrets chiffrés en base.
* Un profil est rattaché au **modèle d'imprimante** : toutes les imprimantes du modèle l'utilisent par défaut.
* Chaque **imprimante** peut choisir un autre profil (cas particuliers).
* Sans profil (installations existantes), les champs « communauté / version » actuels de l'imprimante continuent de s'appliquer.
* Réalisation : phase B, branche `feature/serveur-sans-localsystem`.

### 2.5 Questions initiales

1. Vos parcs d'imprimantes supportent-ils SNMPv3 (la plupart des modèles professionnels récents, oui) et est-ce un besoin réel des clients ?
2. Profils d'identifiants partagés (recommandé) ou identifiants par imprimante ?
3. **Test** : il faudra au moins une imprimante (ou un simulateur SNMP) configurée en v3 pour la recette : je ne peux pas le vérifier ici.

**Estimation :** modèle + écran + profils + chiffrement + adaptation des appels SNMP : environ 1,5 à 2 jours, plus la recette sur matériel réel.
