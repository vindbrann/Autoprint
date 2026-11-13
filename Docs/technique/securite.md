# Modèle de sécurité

Cette page décrit comment Autoprint se protège, dans sa version actuelle. L'historique des audits et des correctifs se trouve dans `Docs/archives/`. Pour signaler une faille : [SECURITY.md](../../SECURITY.md).

## Serveur

### Privilèges

- Le site web tourne sous l'identité de son pool IIS (`ApplicationPoolIdentity`), sans droit d'administration.
- Les opérations sur le spouleur Windows sont confiées au service **`AutoprintSpooler`**, seul composant en `LocalSystem`. Il écoute un tube nommé local :
  - ACL limitée à l'identité du pool et à SYSTEM ;
  - aucun accès réseau ;
  - messages de taille bornée ;
  - cinq commandes possibles, traitées une à la fois ;
  - chaque commande est inscrite dans le journal Windows.
- Les droits NTFS sont restreints : `appsettings.json` est lisible seulement par SYSTEM, les administrateurs et le pool. Les utilisateurs standard n'ont aucun droit sur `Data` ni `Keys`.

### Transport et en-têtes

- HTTPS obligatoire pour les postes, avec redirection HTTP vers HTTPS et HSTS.
- **Noms acceptés** : le serveur ne répond qu'aux noms de sa liste, plus `localhost` ; tout autre en-tête `Host` reçoit une erreur 400. La liste est gérée dans la console, et n'est jamais reprise d'une sauvegarde.
- Sont envoyés : `Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`.
- Derrière un proxy inverse, les en-têtes `X-Forwarded-*` ne sont pris en compte que depuis les proxys déclarés (`ReverseProxy:KnownProxies`). Sinon, on pourrait se forger une adresse IP pour contourner les limites.

### Authentification de la console

- **Comptes locaux** : mot de passe haché en PBKDF2 salé, 12 caractères minimum et différent de l'identifiant. Le changement peut être imposé à la prochaine connexion.
- **Comptes Active Directory** : vérification par LDAP. Les saisies sont échappées contre l'injection de filtre, et le rôle découle du groupe AD.
- **Limites** :
  - 10 tentatives de connexion par minute et par adresse IP ;
  - un compte local est bloqué 2 minutes après 5 échecs ;
  - un compte AD est bloqué 30 minutes après 3 échecs, pour ne pas atteindre le seuil de verrouillage du domaine.
- **Sessions** :
  - jeton JWT de 30 minutes, renouvelé tant que la console est utilisée ;
  - déconnexion après une inactivité réglable ;
  - un changement de mot de passe ou la désactivation du compte invalide les sessions ouvertes (empreinte de sécurité) ;
  - la clé de signature JWT par défaut est refusée en production.
- **Mot de passe oublié** : lien valable 30 minutes, à usage unique. Seule une empreinte du jeton est stockée en base. La réponse est toujours la même, qu'un compte existe ou non.

### Autorisations

- Contrôle par droits (RBAC) sur chaque API : lecture, écriture et suppression par domaine, plus des droits spécifiques (scan réseau, paramètres, archivage…).
- Le droit **Administration** donne tous les droits. Un utilisateur ne peut pas accorder un droit qu'il n'a pas.
- Toutes les actions d'administration sont inscrites au journal d'audit : auteur, date, adresse IP, valeurs avant et après.

### Secrets

- Les mots de passe AD et SMTP, la clé d'API des postes et les communautés et identifiants SNMP sont **chiffrés en base**, avec ASP.NET Core Data Protection. Les clés sont dans `C:\ProgramData\Autoprint\Keys`, protégées par DPAPI pour la machine.
- Les secrets ne sont jamais renvoyés au navigateur : la console affiche un masque.
- La sauvegarde n'inclut les secrets que si un mot de passe de protection est donné ; ils sont alors chiffrés avec ce mot de passe.

### Données exportées

- Exports CSV : les caractères qui déclenchent une formule dans un tableur (`=`, `+`, `-`, `@`) sont neutralisés.

## Postes clients

- Le client tourne dans la session de l'utilisateur, sans service ni élévation de droits.
- Il ajoute et retire des connexions d'imprimantes réseau avec les API Windows, sans interface ni appel à `printui`. Les noms sont validés, et les imprimantes locales ne sont pas touchées.
- Il vérifie le certificat HTTPS du serveur.
- Il s'authentifie avec la **clé d'API des postes** (en-tête `X-Agent-Secret`). La clé se change depuis la console avec une période de transition. Le MSI l'écrit dans le registre par une action masquée, pour qu'elle n'apparaisse pas dans les journaux d'installation, et l'efface à la désinstallation.

## Intégrations

- Les outils de supervision lisent `GET /api/v1/monitoring/export` avec un **jeton d'intégration** (en-tête `X-Api-Token`), révocable, qui ne donne accès qu'à cet export.

## Chaîne de développement

- **CodeQL** analyse le code à chaque modification.
- **Dependabot** propose les mises à jour des dépendances. La politique de mise à jour est dans [POLITIQUE_VERSIONS.md](../projet/POLITIQUE_VERSIONS.md).
- Le workflow **Contrôles de sécurité** fait échouer la compilation si une dépendance a une vulnérabilité connue.
- Une alerte n'est jamais masquée : on corrige la cause.
