# Plan de migration : serveur 1.1.2 vers la version 26.11 (B82)

Objectif : passer le serveur et les postes de la 1.1.2 à la version 26.11 sans couper la liaison entre les postes et le serveur.

Référence 1.1.2 utilisée pour l'analyse : commit `bb5a2d0` (installeur serveur 1.1.2.0, MSI client 1.1.3.0). Analyse du code faite le 11/11/2026.

---

## 1. Compatibilité

| Client | Serveur | Résultat |
|---|---|---|
| 1.1.x | 1.1.2 | situation actuelle |
| **1.1.x** | **26.11** | **compatible**, à une condition : le nom utilisé par les postes doit être autorisé par le serveur (voir 2.1) |
| 26.11 | 26.11 | compatible si le certificat HTTPS du serveur est approuvé par les postes (voir 2.2) |
| 26.11 | 1.1.2 | fonctionne aussi (mêmes routes), mais inutile : on ne le fait pas |

Ce qui ne change pas entre la 1.1.2 et la 26.11 (vérifié dans le code) :
- l'en-tête d'authentification `X-Agent-Secret` et la clé agent. Elle est chiffrée en base à la mise à jour, mais sa valeur reste la même. Le serveur lit encore une clé en clair héritée ;
- les routes `GET api/emplacements` et `GET api/imprimantes`, et le hub SignalR `/hubs/events`, sans authentification ;
- les champs utilisés par le client 1.1.x : lieux avec réseaux, codes et statut ; imprimantes avec nom, lieu, statut, modèle et pilote. Les énumérations sont toujours envoyées en nombres. Les nouveaux champs sont ignorés par l'ancien client ;
- la limite de requêtes, qui ne concerne que la connexion à la console : des centaines de postes derrière la même adresse ne sont pas bloqués ;
- le partage d'impression `\\serveur\imprimante` utilisé pour l'installation (même nom que `PRINTSERVER`) ;
- les préférences des utilisateurs (`Documents\Autoprint\user-settings.json`) : même format, les imprimantes par défaut par site sont conservées par le nouveau client.

**Conclusion : le serveur d'abord, puis les postes par vagues.** Pendant toute la transition, les deux clients cohabitent sans problème.

---

## 2. Points bloquants à vérifier avant de commencer

### 2.1 Nom du serveur utilisé par les postes (le plus important)
- En 1.1.2, le serveur accepte n'importe quel nom (`AllowedHosts = *`).
- À la mise à jour, l'installeur 26.11 demande le **nom public** et n'autorise plus que ce nom et `localhost`.
- Une requête adressée à un autre nom reçoit une **erreur 400**, ce qui coupe **tous** les postes d'un coup, anciens comme nouveaux.
- À vérifier : la valeur de `PRINTSERVER` dans `HKLM\SOFTWARE\Autoprint` sur les postes (paramètre de l'application Intune). Elle doit être **exactement** le nom saisi dans l'installeur, sans `https://`. Pas de nom court ni d'alias à la place du nom complet, pas d'adresse IP.
- Si les postes utilisent plusieurs noms (nom court et nom complet, ancien alias), l'installeur ne permet aujourd'hui d'en saisir qu'un seul. Deux options :
  - ajouter les autres noms à la main dans `C:\Program Files\Autoprint Server\appsettings.json` (`"AllowedHosts": "nom1;nom2;localhost"`), puis redémarrer le site ;
  - **ou** améliorer l'installeur pour accepter plusieurs noms séparés par `;` (petit changement, à décider).

### 2.2 Certificat HTTPS
- Le client 1.1.x **accepte tout certificat**, même auto-signé ou expiré. Le client 26.11 le **vérifie**, comme un navigateur.
- Avant de déployer le nouveau client, il faut donc que le certificat du site :
  - soit émis pour le nom de `PRINTSERVER` ;
  - soit valide (non expiré) ;
  - soit émis par une autorité approuvée par les postes : autorité publique, ou autorité interne déployée par Intune (profil de certificat approuvé).
- Test simple sur un poste, sans rien installer : ouvrir `https://<PRINTSERVER>/` dans Edge, sans alerte de sécurité. Le nouveau client affiche aussi « Certificat du serveur refusé » dans sa zone informatique (5 clics sur la version, puis « Tester la connexion »).
- Ce point ne concerne **pas** la mise à jour du serveur : les anciens postes continuent de fonctionner même avec un certificat non approuvé.

### 2.3 Adresse et port
- Si `PRINTSERVER` contient `http://` ou un port, le serveur 26.11 redirige vers HTTPS. Le client suit la redirection.
- À vérifier tout de même en labo avec la valeur réelle de production.

---

## 3. Déroulé

### Étape 0 : répétition en labo (obligatoire)
1. Instantané « Serveur en 1.1.2 » et client 1.1.x sur CLT-TEST, avec le **même** nom de serveur, le même type de certificat et la même valeur de `PRINTSERVER` qu'en production.
2. Dérouler les étapes 1 à 4 ci-dessous et cocher la section « Migration 1.1.2 » de la checklist de fin de campagne.

### Étape 1 : préparation (production)
- Relever : la valeur de `PRINTSERVER` sur les postes, le certificat du site (émetteur, nom, date d'expiration), le type de base (SQLite ou SQL Server) et le nombre de postes.
- Sauvegarde **complète** avant la mise à jour : instantané de la VM, plus une copie de la base. Pour SQLite : `C:\ProgramData\Autoprint\Data\Autoprint.db` ou l'ancien emplacement dans Program Files. Pour SQL Server : une sauvegarde `.bak`.
  - La mise à jour fait évoluer la base : **on ne revient pas en arrière sans cette sauvegarde**.
- Choisir un créneau calme : pendant la mise à jour, les postes gardent leurs imprimantes, mais ne peuvent pas en ajouter.

### Étape 2 : mise à jour du serveur
1. Lancer l'installeur 26.11. Au champ « Nom public », saisir **le nom utilisé par les postes** (2.1).
2. Après l'installation :
   - sauvegarder les clés de chiffrement (manuel serveur : clés DPAPI propres au serveur) ;
   - faire une sauvegarde protégée par mot de passe depuis la console.
3. Contrôles :
   - la console s'ouvre et les lieux et imprimantes sont là ;
   - sur un poste **1.1.x** : le badge « hors ligne » disparaît dans la minute (reconnexion automatique toutes les 30 s) ;
   - le lieu est détecté et une imprimante s'ajoute ;
   - une modification dans la console (renommer une imprimante, par exemple) arrive sur le poste.
4. Si les postes reçoivent une erreur 400 : le nom n'est pas autorisé. Corriger `AllowedHosts` (2.1) : c'est immédiat, sans réinstallation.

### Étape 3 : nouveau client par vagues Intune
1. **Prérequis :** certificat approuvé par les postes (2.2).
2. Nouvelle application Intune : MSI client 26.11, **remplaçant** (supersedence) l'application 1.1.x.
   - Le MSI a le même `UpgradeCode`, et Windows Installer remplace l'ancienne version (mise à jour majeure).
   - Même ligne de commande : `PRINTSERVER="<nom>" APIKEY="<clé>"`.
   - Si `APIKEY` n'est pas passée, la clé déjà présente dans le registre est relue et conservée.
3. Le MSI ferme le client en cours. Le nouveau client démarre à la prochaine ouverture de session (Active Setup), ou en le lançant depuis le menu Démarrer.
4. Vagues :
   - **pilote** : 5 à 10 postes, dont un en télétravail (VPN) et un sur un site avec plusieurs imprimantes ;
   - puis un groupe par site ou par service ;
   - puis le reste.
5. Contrôles sur le pilote :
   - le panneau s'ouvre ;
   - le site est détecté et l'imprimante par défaut de chaque site est retrouvée ;
   - ajout et retrait d'une imprimante fonctionnent ;
   - la zone informatique affiche « Connexion réussie » au test.

### Étape 4 : rotation de la clé agent (quand tous les postes sont en 26.11)
- La clé actuelle a été lisible en clair sur les postes et dans les journaux d'installation de la 1.1.x. On la change une fois la migration terminée.
- Console > Paramètres > Sécurité > zone sensible « Nouvelle clé d'API » : régler « Ancienne clé encore acceptée pendant » (30 jours par défaut), puis « Générer une nouvelle clé ». L'ancienne clé reste acceptée pendant cette période.
- Redéployer l'application Intune avec la nouvelle `APIKEY` (nouvelle révision, ou script qui met à jour la valeur dans le registre).
- À la fin de la transition, l'ancienne clé est refusée. Le serveur ne journalise pas les postes qui utilisent encore l'ancienne clé : s'appuyer sur l'état de déploiement Intune et choisir dès le départ une période assez longue (une nouvelle rotation ne prolonge pas l'ancienne clé : elle la remplace par la clé actuelle).

---

## 4. Retour arrière
- **Serveur** : restaurer l'instantané ou la sauvegarde de l'étape 1. Les postes 1.1.x n'ont rien à faire.
- **Postes** : en général inutile, le client 1.1.x fonctionnant avec le serveur 26.11. Si nécessaire : désinstaller le 26.11, puis réinstaller la 1.1.x (le MSI refuse le retour à une version plus ancienne par simple mise à jour).
- Les préférences des utilisateurs sont conservées dans les deux sens.

---

## 5. Points de vigilance mineurs
- **Pendant la mise à jour du serveur**, les postes 1.1.x affichent « Mode hors ligne » quelques minutes, puis se reconnectent seuls (contrôle toutes les 30 s).
- **Nettoyage de l'ancien client** :
  - l'onglet « Mes installations » du 1.1.x permettait de supprimer des imprimantes **locales** ;
  - le nouveau client ne gère plus que les imprimantes réseau et renvoie vers Windows pour le reste ;
  - à signaler aux équipes de support.
