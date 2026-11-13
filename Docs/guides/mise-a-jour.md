# Mettre à jour Autoprint

Une mise à jour se fait en deux temps : d'abord le serveur, puis les postes, à votre rythme. Un serveur à jour accepte les clients de la version précédente, et même ceux de la 1.1.x : rien ne presse côté postes.

## 1. Avant de commencer

1. Lisez le [CHANGELOG](../../CHANGELOG.md) de la nouvelle version.
2. **Sauvegardez** :
   - un instantané de la machine virtuelle, si le serveur en est une ;
   - la base : `C:\ProgramData\Autoprint\Data` pour SQLite (site arrêté dans IIS), ou une sauvegarde de la base SQL Server ;
   - le dossier des clés `C:\ProgramData\Autoprint\Keys` ;
   - une sauvegarde depuis la console, **Paramètres > Maintenance**, avec un mot de passe de protection.

La mise à jour fait évoluer la base : sans ces sauvegardes, pas de retour en arrière.

## 2. Mettre à jour le serveur

1. Lancez l'installeur de la nouvelle version en tant qu'administrateur. Il détecte l'installation existante et passe en mode mise à jour.
2. Suivez les étapes. La base, les réglages, les noms acceptés et les clés de chiffrement sont conservés.
3. À la fin, « Mise à jour terminée ».

Pendant la mise à jour, qui dure quelques minutes, la console est indisponible et les postes affichent « Serveur injoignable ». Ils se reconnectent seuls ensuite.

**Contrôles après la mise à jour :**

- la console s'ouvre et affiche la nouvelle version (**Paramètres**, ou le tableau de bord) ;
- **Imprimantes > Audit serveur** ne signale pas d'écart ;
- sur un poste, le panneau Autoprint affiche le site et ses imprimantes.

## 3. Mettre à jour les postes

Déployez le nouveau MSI client, de préférence par vagues : un groupe pilote, puis le reste. Avec Intune, créez une nouvelle application qui remplace l'ancienne ; le détail est dans [Déployer le client](deploiement-client.md#déployer-avec-intune). Les paramètres déjà présents sur les postes (`PRINTSERVER`, `SERVERURL`, `APIKEY`) sont conservés si vous ne les repassez pas.

## Depuis la version 1.1.x

La mise à jour directe d'un serveur 1.1.x vers 26.11 est prise en charge, en SQLite comme en SQL Server. La base est remise à niveau automatiquement, y compris pour les bases SQLite anciennes que les versions 1.1.1 et 1.1.2 ne savaient pas mettre à jour.

Points propres à ce passage :

- **Le certificat** : le client 1.1.x acceptait n'importe quel certificat, le nouveau client exige un certificat approuvé par les postes. Mettez-en un en place avant de déployer le nouveau client.
- **La clé d'API** est conservée, chiffrée en base : les postes 1.1.x continuent de fonctionner sans changement.
- **Si l'adresse de la console change** en même temps (nouveau nom DNS, nouveau domaine), suivez la [migration avec changement de nom](migration-changement-de-nom.md) : les deux noms cohabitent le temps de migrer tous les postes.
- **Après la migration de tous les postes**, générez une nouvelle clé d'API (**Paramètres > Sécurité**) avec une période de transition, puis redéployez le MSI avec cette clé : l'ancienne a pu être lue sur les postes 1.1.x.

## Revenir en arrière

- **Serveur** : restaurez l'instantané de la machine virtuelle. Sans instantané : désinstallez Autoprint, réinstallez l'ancienne version, puis remettez en place la base et le dossier des clés sauvegardés à l'étape 1.
- **Postes** : c'est rarement utile, puisque l'ancien client fonctionne avec le nouveau serveur. Sinon, désinstallez le nouveau client puis réinstallez l'ancien : le MSI refuse de revenir à une version plus ancienne par simple mise à jour.
