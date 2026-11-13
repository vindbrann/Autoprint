# Dépannage

## Côté postes

### Le panneau affiche « Serveur injoignable »

Le client n'arrive pas à joindre la console. Les imprimantes déjà installées restent utilisables.

1. Sur le poste, cliquez cinq fois sur le numéro de version (en bas des réglages), puis **Tester la connexion**. Le message indique la cause.
2. Vérifiez l'adresse affichée dans **Console et API** : c'est `SERVERURL`, ou `https://PRINTSERVER` sans `SERVERURL`. Elle doit s'ouvrir dans un navigateur du poste.
3. Si le test répond **« Certificat du serveur refusé »** : le certificat HTTPS de la console n'est pas approuvé par le poste. Il doit venir d'une autorité publique ou de votre autorité interne, et correspondre au nom utilisé. Un certificat auto-signé est refusé.
4. Si le serveur répond **400 « Nom d'hôte non autorisé »** : le nom utilisé par le poste n'est pas dans **Paramètres > Sécurité > Noms acceptés par le serveur**. Ajoutez-le.
5. Si le serveur répond **401** : la clé d'API du poste n'est plus valable, par exemple après une rotation sans redéploiement. Redéployez le MSI avec la clé actuelle.

### Le panneau affiche « Hors des sites »

L'adresse IP du poste n'appartient à aucun réseau déclaré. Vérifiez les réseaux du lieu dans **Lieux**, avec l'adresse du poste (`ipconfig`). Pensez aux réseaux Wi-Fi et VPN, à déclarer comme réseaux secondaires.

### Une imprimante ne s'ajoute pas

Le message s'affiche en rouge sous l'imprimante.

- Le poste doit pouvoir ouvrir `\\serveur-d-impression` (partages SMB).
- L'imprimante doit avoir été synchronisée : dans la console, elle ne doit pas être « en attente » ni en erreur de synchronisation.
- Le pilote du modèle doit déjà être présent sur le poste : Windows ne l'installe plus depuis le serveur pour un utilisateur standard. Voir [les pilotes sur les postes](deploiement-client.md#les-pilotes-dimpression-sur-les-postes).

## Côté console et serveur

### Erreur 400 « Nom d'hôte non autorisé » en ouvrant la console

Le nom tapé dans le navigateur n'est pas dans la liste des noms acceptés. Ouvrez la console sur le serveur lui-même par `https://localhost/`, puis ajoutez le nom dans **Paramètres > Sécurité**.

### Le navigateur force HTTPS alors que le serveur n'a pas encore de certificat

Ce cas arrive après la réinstallation d'un serveur sous un nom déjà utilisé en HTTPS : le navigateur a mémorisé la règle HSTS et refuse de rester en HTTP. Dans Edge ou Chrome, ouvrez `edge://net-internals/#hsts` (ou `chrome://net-internals/#hsts`), puis supprimez le nom dans **Delete domain security policies**. Le mieux reste d'activer HTTPS tout de suite (voir [Installer le serveur](installation-serveur.md#2-activer-https)).

### Mot de passe du compte `admin` perdu

Sur le serveur, en administrateur :

1. Pour une base SQLite, arrêtez le site **Autoprint** dans IIS.
2. Lancez `C:\Program Files\Autoprint Server\ResetPassword.bat`.
3. Le script affiche un mot de passe temporaire, une seule fois. Il devra être changé à la connexion.
4. Redémarrez le site.

### « Trop de tentatives pour ce compte »

Le compte est bloqué quelques minutes après plusieurs échecs : 2 minutes après 5 échecs pour un compte local, 30 minutes après 3 échecs pour un compte de l'annuaire. Attendez la fin du délai.

### Une imprimante n'apparaît pas dans Windows

Elle n'a pas encore été synchronisée, ou la synchronisation a échoué. Le tableau de bord compte les erreurs de synchronisation, et la ligne de l'imprimante donne le message. Cause la plus fréquente : un pilote absent du serveur. Installez-le, **Pilotes > Scanner le serveur**, puis **Réessayer la synchronisation**.

### La supervision ne remonte rien

- Le bouton de diagnostic de l'imprimante montre ce que répond l'imprimante, à l'instant.
- Vérifiez la communauté SNMP v2c, ou les identifiants SNMPv3, sur l'imprimante, le modèle ou le profil d'identifiants.
- Le port **UDP 161** doit être ouvert entre le serveur et l'imprimante.
- La supervision doit être active, dans la plage horaire, sous **Paramètres > Supervision**.

### Les e-mails ne partent pas

**Paramètres > Messagerie (SMTP) > Envoyer un e-mail de test** affiche l'erreur exacte : serveur, port, chiffrement ou authentification.

## Où trouver quoi

| Quoi | Où |
|---|---|
| Programme du serveur | `C:\Program Files\Autoprint Server` |
| Base SQLite | `C:\ProgramData\Autoprint\Data\Autoprint.db` |
| Clés de chiffrement | `C:\ProgramData\Autoprint\Keys` (à sauvegarder avec la base) |
| Licences | `C:\Program Files\Autoprint Server\Licenses` |
| Erreurs du serveur | Observateur d'événements, journal **Application** |
| Actions d'administration | Console, **Journal d'audit** |
| Client installé | `C:\Program Files\Autoprint Client` |
| Configuration d'un poste | Registre `HKLM\SOFTWARE\Autoprint` (`PRINTSERVER`, `SERVERURL`, `APIKEY`) |
| Données d'un utilisateur | `%LOCALAPPDATA%\Autoprint` |

Pour signaler un bug : ouvrez une *issue* sur [GitHub](https://github.com/vindbrann/Autoprint/issues). Pour une faille de sécurité, suivez [SECURITY.md](../../SECURITY.md).
