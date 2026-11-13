# Déployer le client sur les postes

Le client Autoprint est une petite application près de l'horloge de Windows. Elle détecte le site où se trouve le poste, à partir de son adresse IP, et propose les imprimantes de ce site.

Il s'installe avec un MSI unique, prévu pour Intune ou tout autre outil de déploiement. L'installation se fait pour l'ordinateur ; chaque utilisateur garde ses propres réglages.

## Ce qu'il faut

| Élément | Détail |
|---|---|
| Postes | Windows 10 ou 11, 64 bits. Aucun runtime à installer : le client embarque tout. Les postes n'ont pas besoin d'être dans le domaine. |
| Réseau | Accès HTTPS à la console Autoprint et accès aux partages du serveur d'impression (SMB, `\\serveur\imprimante`). |
| Certificat | Le certificat HTTPS de la console doit être approuvé par les postes. |
| Clé d'API | **Paramètres > Sécurité > Clé d'API des postes clients**, dans la console. |
| Lieux | Les lieux et leurs réseaux doivent être déclarés dans la console, sinon le poste ne se reconnaît sur aucun site (voir [Premiers pas](premiers-pas.md)). |

### Les pilotes d'impression sur les postes

Depuis les correctifs PrintNightmare, Windows ne laisse plus un utilisateur standard installer un pilote depuis le serveur d'impression. Le client Autoprint ajoute les imprimantes sans aucune fenêtre ni élévation de droits : **le pilote de chaque modèle doit donc déjà être présent sur le poste**. Pré-déployez-le avec Intune (paquet du constructeur), SCCM ou une GPO, avant ou en même temps que le client.

Sans le pilote, l'ajout échoue et le panneau l'indique en rouge sous l'imprimante.

Le MSI se trouve sur la page [Releases](https://github.com/vindbrann/Autoprint/releases) : `Autoprint-Client-<version>.msi`.

## Les paramètres du MSI

| Paramètre | Obligatoire | Rôle |
|---|---|---|
| `PRINTSERVER` | oui | Nom du serveur d'impression, tel qu'il apparaît dans les partages : `PRINTSERVER="srv-print"` donne `\\srv-print\imprimante`. |
| `SERVERURL` | non | Adresse de la console si elle diffère de `https://PRINTSERVER`, par exemple `SERVERURL="https://autoprint.mondomaine.fr"`. |
| `APIKEY` | oui | La clé d'API des postes. |

Les valeurs sont enregistrées dans `HKLM\SOFTWARE\Autoprint`. Lors d'une mise à jour, une valeur omise garde celle déjà présente sur le poste, et une valeur passée remplace l'ancienne : c'est ce qui permet de changer de clé ou d'adresse en redéployant.

## Déployer avec Intune

1. Créez une **application Windows (Win32)** à partir du MSI, après l'avoir empaqueté avec l'outil *Microsoft Win32 Content Prep Tool*, ou une **application métier** qui accepte le MSI directement.
2. **Commande d'installation** :
   ```
   msiexec /i Autoprint-Client-<version>.msi PRINTSERVER="srv-print" SERVERURL="https://autoprint.mondomaine.fr" APIKEY="<clé>" /qn
   ```
3. **Commande de désinstallation** : `msiexec /x {code produit} /qn`. Intune le renseigne pour un MSI.
4. **Comportement d'installation** : **Système**.
5. **Règle de détection** : le code produit MSI.
6. Affectez l'application à un groupe pilote de quelques postes, puis élargissez.

Après l'installation, le client démarre à la prochaine ouverture de session de chaque utilisateur, ou tout de suite depuis le menu Démarrer (**Autoprint**).

**Mise à jour** : créez une nouvelle application avec le nouveau MSI, qui **remplace** l'ancienne (supersedence). Le MSI ferme le client en cours, remplace la version précédente et garde les imprimantes et les préférences des utilisateurs.

## Installer à la main

Double-cliquez sur le MSI. Après la licence, l'écran **Paramètres de connexion** demande les trois mêmes valeurs : serveur d'impression, adresse de la console (facultative) et clé d'API.

## Vérifier sur un poste

1. Cliquez sur l'icône Autoprint près de l'horloge : le panneau affiche le site détecté et ses imprimantes.
2. Cliquez cinq fois sur le numéro de version, en bas des réglages : les informations serveur s'affichent. **Console et API** doit montrer l'adresse de la console, **Partages d'impression** le serveur d'impression. Cliquez sur **Tester la connexion** : le résultat attendu est « Connexion réussie ».

Si le panneau affiche « Serveur injoignable » ou « Hors des sites », voir [Dépannage](depannage.md).

## Désinstaller

Désinstallez l'application depuis Intune, ou depuis **Paramètres Windows > Applications**. La clé d'API est retirée du poste. Les imprimantes déjà ajoutées restent dans Windows.
