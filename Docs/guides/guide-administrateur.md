# Guide administrateur

Ce guide parcourt la console Autoprint, menu par menu. Pour installer et démarrer, voir d'abord [Installer le serveur](installation-serveur.md) et [Premiers pas](premiers-pas.md).

La console s'ouvre dans un navigateur, à l'adresse de votre serveur (`https://autoprint.mondomaine.fr/`). On s'y connecte avec un compte Active Directory autorisé, ou avec le compte local de secours `admin`. Le thème clair ou sombre se change en bas du menu.

## Tableau de bord

- **État du serveur** : le spouleur Windows et les erreurs de synchronisation en cours.
- **Le parc en chiffres** : imprimantes, pilotes, lieux, imprimantes hors ligne, alertes actives.
- **Répartition par modèle** et **dernières activités** du journal d'audit.

## Gestion du parc

### Imprimantes

La liste se filtre et se trie sur chaque colonne. Ses réglages (colonnes, tri) sont retenus.

- **Ajouter**, ou un clic sur une ligne pour la modifier. Le détail des champs est dans [Premiers pas](premiers-pas.md#4-déclarer-limprimante).
- **Diagnostic** (bouton de la ligne) : un ping et une lecture SNMP immédiats. Il affiche l'état, les toners, les bacs, le compteur et les alertes. Une imprimante dont le modèle n'a pas de profil SNMP est lue avec les valeurs standard.
- **Synchroniser** : une création, une modification ou une suppression ne s'applique à Windows qu'à la synchronisation. Un aperçu indique ce qui va changer avant que vous validiez.
- **Audit serveur** : compare la console et le spouleur Windows, et signale les écarts, par exemple une imprimante supprimée à la main dans Windows.
- **Scanner le réseau** : découvre les imprimantes d'une plage d'adresses et propose de les ajouter.
- **Exporter en CSV**.
- **Archivage** : une imprimante injoignable depuis 30 jours est archivée automatiquement, pour ne plus encombrer la liste ni les rapports.

### Lieux, marques, modèles, pilotes

- **Lieux** : le nom, le code, le réseau principal et les réseaux secondaires. Un lieu **inactif** n'est plus proposé à la création d'imprimantes. Supprimer un lieu qui a encore des imprimantes dans Windows les marque « à supprimer » : synchronisez, puis supprimez le lieu.
- **Marques** et **Modèles** : deux marques du même nom, ou deux modèles du même nom pour une marque, sont refusés. Le modèle porte le pilote, le profil de lecture SNMP et éventuellement des identifiants SNMP communs.
- **Pilotes** : les pilotes installés sur le serveur. **Scanner le serveur** met la liste à jour après l'installation d'un nouveau pilote.

### Supervision SNMP

- **Profils SNMP** : pour un modèle qui n'expose pas les valeurs standard, un profil liste les OID à lire. L'assistant interroge une imprimante d'essai, lit ses valeurs, détecte les champs et propose des modèles d'OID courants.
- **Identifiants SNMP** : des profils réutilisables, avec une communauté v2c ou un compte SNMPv3 (authentification et chiffrement). Les secrets sont chiffrés en base.
- La fréquence et les plages horaires de la supervision se règlent dans **Paramètres > Supervision**.
- **Prévision** : à partir de la consommation observée, Autoprint estime le nombre de jours avant l'épuisement de chaque toner.

### Rapports et planification

Un rapport se crée en trois étapes :

1. son nom, le format (**PDF** ou **CSV**), les destinataires ;
2. le périmètre : tout le parc, ou des règles (par lieu, par modèle…) ;
3. le contenu (synthèse du parc, compteurs, toners, disponibilité, alertes, prévisions) et la fréquence (**quotidien**, **hebdomadaire** ou **mensuel**, à l'heure choisie).

**Envoyer maintenant** envoie le rapport tout de suite, pour vérifier son contenu. L'envoi passe par le serveur configuré dans **Paramètres > Messagerie (SMTP)**.

## Utilisateurs et droits

- **Utilisateurs** : des comptes locaux, ou des comptes de l'annuaire ajoutés à la main. Un compte local peut devoir changer son mot de passe à la prochaine connexion.
- **Rôles et droits** : chaque rôle est une liste de droits par domaine (imprimantes, lieux, rapports, paramètres…). Le droit **Administration** donne tous les droits. Un utilisateur ne peut pas accorder un droit qu'il n'a pas lui-même.
- **Active Directory** (**Paramètres > Active Directory**) : vous associez des groupes AD à des rôles. Un utilisateur du domaine se connecte avec son compte habituel et reçoit le rôle de son groupe. **Vérifier un compte** montre le résultat pour un utilisateur précis.
- **Protection contre les essais de mot de passe** : un compte local est bloqué 2 minutes après 5 échecs. Un compte de l'annuaire est bloqué 30 minutes après 3 échecs, pour ne pas verrouiller le compte Windows.
- **Mot de passe oublié** : un compte local qui a une adresse e-mail reçoit un lien valable 30 minutes.

## Paramètres

| Section | Contenu |
|---|---|
| **Nommage** | La règle de nommage des imprimantes et des partages, avec des variables cliquables (code du lieu, modèle…) et un aperçu. **Renommer N imprimantes** applique une nouvelle règle au parc existant. Les lettres accentuées sont conservées sans accent. |
| **Valeurs par défaut** | Les réglages proposés à la création, comme l'impression directe. Une action passe tout le parc en impression directe. |
| **Supervision** | Activation, fréquence et plage horaire de la supervision automatique. |
| **Scanner réseau** | Les réseaux à analyser, les jours d'analyse planifiée et le rapport par e-mail. |
| **Active Directory** | Le domaine, le compte de service, les groupes autorisés et leurs rôles. |
| **Messagerie (SMTP)** | Le serveur d'envoi, l'authentification, l'expéditeur, et un e-mail de test. |
| **Intégration monitoring** | Les jetons pour Zabbix, PRTG, Centreon… (voir plus bas) et les alertes exportées. |
| **Sécurité** | La clé d'API des postes clients, la politique des mots de passe locaux, la déconnexion après inactivité et les noms acceptés par le serveur. |
| **Maintenance** | La rétention du journal d'audit, la sauvegarde et la restauration. |

Un bandeau « Modifications non enregistrées » rappelle d'enregistrer avant de quitter une section.

### Clé d'API des postes

C'est la clé que présentent les clients Autoprint. **Générer une nouvelle clé** propose une période de transition, pendant laquelle l'ancienne clé reste acceptée, le temps de redéployer le MSI avec la nouvelle (voir [Déployer le client](deploiement-client.md)).

### Noms acceptés par le serveur

Le serveur ne répond qu'aux noms de cette liste, plus `localhost`. Un autre nom reçoit une erreur 400 : c'est une protection contre l'usurpation de l'en-tête `Host`.

- Le **premier** nom sert aux liens envoyés par e-mail.
- Une modification s'applique tout de suite et est inscrite au journal d'audit.
- On ne peut pas retirer le nom par lequel on est connecté.
- La liste n'est jamais reprise d'une sauvegarde.
- Pour changer l'adresse de la console sans couper les postes : [migration avec changement de nom](migration-changement-de-nom.md).

### Intégration avec un outil de supervision

1. **Paramètres > Intégration monitoring > Générer un jeton**.
2. Votre outil appelle `GET https://autoprint.mondomaine.fr/api/v1/monitoring/export` avec l'en-tête `X-Api-Token: <jeton>`.
3. Un jeton se révoque à tout moment.

## Sauvegarde et restauration

**Paramètres > Maintenance > Télécharger la sauvegarde** produit un fichier JSON avec toute la configuration : lieux, modèles, imprimantes, rôles, utilisateurs, paramètres.

- **Avec un mot de passe de protection**, la sauvegarde contient aussi les secrets (mots de passe AD et SMTP, clé d'API, identifiants SNMP), chiffrés avec ce mot de passe. C'est la seule façon de les retrouver sur un autre serveur. Rangez le fichier et son mot de passe séparément.
- **Sans mot de passe**, les secrets sont exclus et devront être ressaisis.
- **Restaurer** remplace la configuration actuelle par celle du fichier. Les noms acceptés par le serveur restent ceux du serveur.

Les secrets sont chiffrés en base avec les clés de `C:\ProgramData\Autoprint\Keys`, protégées par Windows pour ce serveur seulement. Sauvegardez ce dossier avec la base (`C:\ProgramData\Autoprint\Data` pour SQLite, ou la base SQL Server). Il ne peut pas être relu sur une autre machine : d'où la sauvegarde protégée par mot de passe.

## Journal d'audit

Chaque action d'administration y est inscrite : qui, quand, depuis quelle adresse IP, avec le détail avant et après la modification. Le journal se filtre par utilisateur, action, niveau et date, et s'exporte en CSV (l'export suit les filtres). Sa durée de conservation se règle dans **Paramètres > Maintenance**.
