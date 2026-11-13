# Premiers pas : de zéro à la première imprimante

Ce guide part d'un serveur fraîchement installé ([Installer le serveur](installation-serveur.md)) et va jusqu'à une imprimante proposée aux postes. Tout se fait dans la console.

Le principe : vous décrivez le parc dans la console (lieux, modèles, imprimantes), puis Autoprint crée lui-même les ports, les files et les partages sur le serveur d'impression Windows quand vous **synchronisez**.

## 1. Installer les pilotes sur le serveur

Autoprint utilise les pilotes déjà installés dans Windows ; il n'en installe pas.

1. Sur le serveur, installez le pilote de chaque modèle d'imprimante, avec l'outil du constructeur ou via **Gestion de l'impression > Pilotes**.
2. Dans la console, ouvrez **Pilotes** puis **Scanner le serveur** : la liste des pilotes installés se met à jour.

## 2. Déclarer un lieu

Un lieu est un site ou un bâtiment, avec ses réseaux. C'est grâce à eux que le client sait où se trouve le poste.

1. **Lieux > Nouveau lieu**.
2. **Nom du lieu** (« Siège ») et **Code** facultatif (`SIEGE`). Le code sert au nommage automatique des imprimantes.
3. **Réseau principal**, en notation CIDR : `10.12.0.0/16`. Les adresses des imprimantes de ce lieu commenceront par ce préfixe.
4. **Réseaux secondaires**, facultatifs : le Wi-Fi, le VPN… Tout poste dont l'adresse tombe dans l'un de ces réseaux est considéré comme présent sur ce site.
5. **Créer le lieu**.

> Un même réseau ne peut appartenir qu'à un seul lieu. Si deux bâtiments partagent un réseau mais pas leurs imprimantes, découpez le réseau en sous-réseaux.

## 3. Déclarer la marque et le modèle

1. **Marques > Nouvelle marque** : « Ricoh ».
2. **Modèles > Nouveau modèle** :
   - **Marque** et **Nom du modèle** : « IM C3000 » ;
   - **Pilote d'impression** : le pilote installé à l'étape 1 ;
   - **Profil de lecture** SNMP : laissez **Valeurs standard**, qui conviennent à la plupart des imprimantes réseau (toner, compteur de pages, état, numéro de série). Un profil sur mesure n'est utile que pour un modèle qui n'expose pas ces valeurs standard (voir **Profils SNMP**) ;
   - **Identifiants** SNMP : laissez **Ceux de chaque imprimante**, ou choisissez un profil d'**Identifiants SNMP** commun (communauté v2c, ou compte SNMPv3).

## 4. Déclarer l'imprimante

1. **Imprimantes > Ajouter**.
2. Choisissez le **Lieu**, puis la **Marque** et le **Modèle**. Le bouton d'enregistrement reste grisé tant que ces deux champs manquent.
3. **Adresse IP** : le préfixe du lieu est déjà rempli, complétez la fin.
4. **Nom affiché** et **Nom du partage** se remplissent selon la règle de nommage (**Paramètres > Nommage**). Vous pouvez les modifier, ou cliquer sur **Recalculer**.
5. **Emplacement** (« Bureau 204 ») et **Code inventaire** sont facultatifs. Ils aident les utilisateurs à trouver la bonne imprimante.
6. **Impression directe** : à activer pour une imprimante d'un site distant. Les postes impriment alors directement vers l'imprimante, sans passer par le serveur central ni charger le lien entre les sites.
7. **Supervision SNMP** : la communauté ou les identifiants, si le modèle ne les fournit pas.

L'imprimante apparaît « en attente » : elle n'existe pas encore dans Windows.

## 5. Synchroniser avec Windows

1. Cliquez sur **Synchroniser** : l'aperçu liste ce qui va être créé, modifié ou supprimé sur le serveur d'impression.
2. Validez. Autoprint crée le port TCP/IP, la file d'impression et le partage.

En cas d'échec (un pilote absent, par exemple), l'imprimante passe en erreur de synchronisation avec le message. Corrigez la cause, puis **Réessayer la synchronisation**.

## 6. Vérifier

- **Sur un poste du lieu** (client installé, voir [Déployer le client](deploiement-client.md)) : le panneau Autoprint propose l'imprimante dans **Ajouter sur ce site**.
- **Supervision** : après le premier passage de la supervision, ou tout de suite avec le bouton de diagnostic de la ligne, l'imprimante affiche son état, ses toners et son compteur de pages. Si rien ne remonte, vérifiez la communauté SNMP et que le port UDP 161 est ouvert entre le serveur et l'imprimante.

## Pour aller plus vite sur un grand parc

- **Imprimantes > Scanner le réseau** découvre les imprimantes d'une plage d'adresses ; **Paramètres > Scanner réseau** planifie cette analyse.
- **Paramètres > Valeurs par défaut** règle ce qui est proposé à chaque création, comme l'impression directe.

La suite : le [guide administrateur](guide-administrateur.md).
