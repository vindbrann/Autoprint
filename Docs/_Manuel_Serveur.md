# 🎛️ Guide de l'Administrateur : Autoprint Server

**Bienvenue sur la console d'administration Autoprint.**
Cette interface Web vous permet de piloter l'intégralité de votre parc d'impression, de gérer les déploiements et de surveiller la santé du service, sans avoir à vous connecter en bureau à distance sur le serveur Windows.

---

## 1. Accès et Connexion
L'administration est accessible via votre navigateur Web favori.

* **URL :** `https://votre-serveur-autoprint/`
* **Authentification :**
    * Utilisez votre **Compte Active Directory** habituel.
    * *Ou* un compte local de secours (ex: `admin`) si le réseau est indisponible.

> **Sécurité :** Si votre mot de passe est expiré ou temporaire, l'application vous demandera immédiatement de le changer avant de vous laisser accéder aux menus.

---

## 2. Le Tableau de Bord (Dashboard)
Dès la connexion, vous avez une vue d'ensemble de la santé du système:

* **État du Service :** Vérifiez en un coup d'œil si le **Spouleur Windows** tourne correctement sur le serveur.
* **Santé du Parc :** Visualisez le nombre d'imprimantes en erreur de synchronisation.
* **Timeline :** Les dernières actions effectuées par vos collègues (Ajout, Suppression, Modification) sont listées ici.

---

## 3. Ajouter une Imprimante (Workflow)
Autoprint utilise une logique structurée pour éviter le chaos. Suivez ces étapes :

### Étape A : Définir le Lieu 
1.  Allez dans **Gestion > Lieux**.
2.  Créez un lieu (ex: "Bâtiment A - Étage 2").
3.  **Important :** Renseignez la plage IP (CIDR) du lieu (ex: `192.168.10.0/24`). C'est grâce à cela que les clients sauront quelles imprimantes afficher !

### Étape B : Vérifier le Pilote 
Autoprint ne permet pas d'importer n'importe quel fichier. Il se base sur ce qui est installé sur le serveur Windows.
1.  Allez dans **Gestion > Pilotes**.
2.  Cliquez sur **"Scanner"** pour rafraîchir la liste des pilotes présents sur le serveur.
3.  Vérifiez que le pilote nécessaire est bien listé (Vert).

### Étape C : Créer le Modèle 
1.  Allez dans **Gestion > Modèles**.
2.  Associez une **Marque** et un **Nom** (ex: "Canon iR-ADV C5535").
3.  Sélectionnez le **Pilote** validé à l'étape précédente.

### Étape D : Créer l'Imprimante 
1.  Allez dans **Gestion > Imprimantes**.
2.  Cliquez sur **Ajouter**.
3.  Remplissez les champs (Nom, IP, Lieu, Modèle).
4.  **Option "Mode Filiale" (Direct Printing)**: Cochez cette case si l'imprimante est sur un site distant. Cela permettra aux PC d'imprimer directement vers l'IP de l'imprimante sans passer par le serveur (économie de bande passante).

---

## 4. La Synchronisation ("Staging")
**Attention, concept clé !**
Quand vous créez ou modifiez une imprimante dans l'interface, **rien ne change immédiatement sur Windows**. Vos modifications sont mises "En Attente".

1.  Une fois vos modifications terminées, un bandeau ou un indicateur vous signalera des changements en attente.
2.  Cliquez sur le bouton **"Synchroniser"** (ou "Appliquer").
3.  Une fenêtre de **Prévisualisation** s'ouvre:
    * 🟢 **Vert :** Ce qui va être créé.
    * 🟠 **Orange :** Ce qui va être modifié.
    * 🔴 **Rouge :** Ce qui va être supprimé du serveur Windows.
4.  Validez pour appliquer réellement les changements sur le Spouleur d'impression.

> *C'est à ce moment précis que les utilisateurs recevront la mise à jour sur leurs postes.* 

---

## 5. Gestion des Utilisateurs & Droits
Vous pouvez déléguer l'administration à des techniciens locaux sans leur donner tous les droits.

* Allez dans **Administration > Utilisateurs**.
* Vous pouvez importer des comptes depuis l'**Active Directory**.
* Assignez des **Rôles** précis (ex: un technicien peut avoir le droit de "Voir" et "Scanner", mais pas de "Supprimer" une imprimante).

---

## 6. Outils de Dépannage

### Le Journal d'Audit 
Une erreur de manipulation ? Une imprimante a disparu ?
* Allez dans **Système > Journal d'Audit**.
* Vous verrez *qui* a fait *quoi* et *quand*.
* Cliquez sur une ligne pour voir le détail "Avant / Après" de la modification.

### Audit Serveur 
Si vous soupçonnez que le serveur Windows n'est pas aligné avec l'interface Web :
* Dans la page Imprimantes, utilisez le bouton **"Audit Serveur"**.
* L'outil va comparer la base de données avec la réalité du terrain et vous signaler les incohérences (ex: une imprimante supprimée manuellement par un admin système).

---
*En cas de problème technique majeur, consultez les logs techniques dans le dossier d'installation.*