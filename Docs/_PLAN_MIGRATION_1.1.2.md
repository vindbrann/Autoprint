# Plan de migration : production 1.1.2 vers la version 26.11 (B82)

## Situation
- Serveur unique, **SQLite**, environ **600 postes** gérés par Intune.
- Le serveur ne change ni de machine ni de domaine AD :
  - les partages d'impression restent `\\anciennom\imprimante` ;
  - seule l'adresse **web** change.
- Aujourd'hui, l'adresse web est `https://autoprint.monanciendomaine.fr`, avec un certificat auto-signé. Le client 1.1.x accepte n'importe quel certificat.
- Demain, ce sera `https://autoprint.nouveaunom.fr`, avec le certificat wildcard acheté. Il est approuvé d'office par Windows.

Référence 1.1.2 utilisée pour l'analyse : commit `bb5a2d0` (installeur serveur 1.1.2.0, MSI client 1.1.3.0). Analyse du code faite le 11/11/2026.

## Ce qui rend la migration possible (26.11)
- **La clé agent est conservée** par la mise à jour : même valeur, seulement chiffrée en base. Les 600 postes n'ont rien à changer.
- **Le serveur accepte plusieurs noms.** L'installeur prend `nouveau;ancien`, puis la liste se gère dans la console (Paramètres > Sécurité > « Noms acceptés par le serveur »). Elle est prise en compte immédiatement, sans redémarrage.
- **Le client sépare deux adresses** :
  - `SERVERURL` : console, API et temps réel. Ce sera le nouveau nom ;
  - `PRINTSERVER` : partages d'impression. Il reste l'ancien nom ;
  - sans `SERVERURL`, le client se comporte comme avant (`https://PRINTSERVER`).
- **Le MSI client applique les valeurs passées par Intune**, même lors d'une mise à jour. Avant, la valeur trouvée dans le registre l'emportait, ce qui aurait gêné aussi la rotation de la clé.
- **Ce qui ne change pas pour un client 1.1.x** (vérifié dans le code) :
  - l'en-tête `X-Agent-Secret` ;
  - les routes `api/emplacements` et `api/imprimantes`, et le hub `/hubs/events`, sans authentification ;
  - les champs utilisés, les énumérations envoyées en nombres, aucune limite de requêtes pour les postes.

**Les clients 1.1.x et 26.11 cohabitent pendant toute la transition.**

---

## Étape 0 : répétition en labo (obligatoire)
- Instantané « Serveur en 1.1.2 », plus un client 1.1.x sur CLT-TEST.
- Deux noms web pour le serveur du labo :
  - un « ancien » nom avec un certificat auto-signé ;
  - un « nouveau » nom avec un certificat approuvé par le poste.
- Dérouler les étapes 1 à 5 et cocher la section « Migration 1.1.2 » de la checklist de fin de campagne.

## Étape 1 : préparer le nouveau nom (sans rien couper)
1. **DNS** : `autoprint.nouveaunom.fr` vers l'adresse du serveur. Les deux noms pointent alors vers la même machine.
2. **Certificat** : importer le wildcard (`.pfx`) dans le magasin « Ordinateur local > Personnel » du serveur.
3. **IIS**, site « Autoprint », deux liaisons HTTPS sur le même port :

   | Nom d'hôte | Certificat | SNI |
   |---|---|---|
   | `autoprint.monanciendomaine.fr` | auto-signé actuel (inchangé) | coché |
   | `autoprint.nouveaunom.fr` | wildcard | coché |

   Si la liaison actuelle n'a pas de nom d'hôte, lui donner l'ancien nom et cocher SNI, ou la laisser comme liaison par défaut.
4. **Test** : depuis un poste, `https://autoprint.nouveaunom.fr/` s'ouvre dans Edge sans alerte. Le serveur 1.1.2 répond déjà, puisqu'il accepte encore tous les noms.

## Étape 2 : sauvegarde
- Instantané de la VM.
- Copie de la base SQLite, serveur arrêté ou après « Arrêter » le site dans IIS :
  - `C:\ProgramData\Autoprint\Data\Autoprint.db`, ou l'ancien emplacement dans `C:\Program Files\Autoprint Server`.
- **On ne revient pas en arrière sans elle** : la mise à jour fait évoluer la base.

## Étape 3 : mise à jour du serveur en 26.11
1. Lancer l'installeur 26.11. Au champ « Nom public », saisir **les deux noms, le nouveau en premier** :
   `autoprint.nouveaunom.fr;autoprint.monanciendomaine.fr`
   Le premier nom sert aux liens envoyés par courriel (mot de passe oublié).
2. Après l'installation :
   - sauvegarder les clés de chiffrement (manuel serveur) ;
   - faire une sauvegarde protégée depuis la console.
3. Contrôles :
   - la console s'ouvre par **les deux** adresses ;
   - Paramètres > Sécurité affiche les deux noms ;
   - sur un poste **1.1.x**, le badge « hors ligne » disparaît dans la minute, le lieu est détecté et une imprimante s'ajoute ;
   - une modification faite dans la console arrive sur le poste.
4. Si les postes reçoivent une erreur 400, un nom manque : l'ajouter dans la console (Paramètres > Sécurité), en l'ouvrant par un nom accepté ou par `https://localhost` sur le serveur.

## Étape 4 : nouveau client par vagues Intune
1. Nouvelle application Intune : MSI client 26.11, qui **remplace** (supersedence) l'application 1.1.x. Même `UpgradeCode` : Windows Installer remplace l'ancienne version.
2. Ligne de commande :
   ```
   msiexec /i Autoprint.Client.Setup.msi PRINTSERVER="anciennom" SERVERURL="https://autoprint.nouveaunom.fr" APIKEY="<clé actuelle>" /qn
   ```
   - `PRINTSERVER` ne change pas : les imprimantes déjà installées sur les postes (`\\anciennom\...`) ne bougent pas.
   - Le client appelle la console par le nouveau nom, avec un certificat valide.
3. Le MSI ferme le client en cours. Le nouveau démarre à la prochaine ouverture de session (Active Setup), ou depuis le menu Démarrer.
4. Vagues :
   - **pilote** : 5 à 10 postes, dont un en télétravail (VPN) et un sur un grand site ;
   - puis par site ou par service ;
   - puis le reste.
5. Contrôles sur le pilote :
   - le panneau s'ouvre et le site est détecté ;
   - l'imprimante par défaut de chaque site est retrouvée ;
   - ajout et retrait d'une imprimante fonctionnent ;
   - informations serveur (5 clics sur la version) : « Console et API » affiche le nouveau nom, « Partages d'impression » `\\anciennom`, et « Tester la connexion » répond « Connexion réussie ».

## Étape 5 : fin de transition (tous les postes en 26.11)
1. **Retirer l'ancien nom** dans la console : Paramètres > Sécurité > « Noms acceptés par le serveur ».
   - Le nom par lequel on est connecté ne peut pas être retiré : ouvrir la console par le nouveau nom.
2. Supprimer la liaison IIS de l'ancien nom et son certificat auto-signé. Supprimer l'enregistrement DNS si l'ancien domaine disparaît.
3. **Changer la clé agent** : l'ancienne a été lisible sur les postes 1.1.x et dans leurs journaux d'installation.
   - Paramètres > Sécurité > « Nouvelle clé d'API » : choisir la transition (30 ou 90 jours), puis « Générer une nouvelle clé ».
   - Redéployer l'application Intune avec la nouvelle `APIKEY` (nouvelle révision). Le MSI applique la nouvelle valeur.
   - Le serveur ne journalise pas les postes qui utilisent encore l'ancienne clé : suivre l'état de déploiement Intune et choisir dès le départ une transition assez longue. Une nouvelle rotation ne prolonge pas l'ancienne clé.

---

## Retour arrière
- **Serveur** : restaurer l'instantané ou la copie de la base de l'étape 2. Les postes 1.1.x n'ont rien à faire.
- **Postes** : en général inutile, car le client 1.1.x fonctionne avec le serveur 26.11. Si nécessaire : désinstaller le 26.11, puis réinstaller la 1.1.x (le MSI refuse le retour en arrière par simple mise à jour).
- Les préférences des utilisateurs sont conservées dans les deux sens.

## Points de vigilance
- **Ne pas retirer l'ancien nom trop tôt.** Tant qu'un poste 1.1.x existe, il appelle l'ancien nom. Le retirer coupe ces postes (erreur 400), mais cela se rattrape en le remettant dans la console.
- **Restauration d'une sauvegarde** : la liste des noms acceptés n'est jamais reprise d'une sauvegarde. Elle reste celle du serveur, pour ne pas se couper de la console.
- **Pendant la mise à jour du serveur**, les postes 1.1.x affichent « Mode hors ligne » quelques minutes, puis se reconnectent seuls (contrôle toutes les 30 s).
- **Support** : le nouveau client ne gère plus les imprimantes locales. Il renvoie vers « Imprimantes et scanners » de Windows.
