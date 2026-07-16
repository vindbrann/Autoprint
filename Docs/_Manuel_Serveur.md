# 🎛️ Guide de l'Administrateur : Autoprint Server

**Bienvenue sur la console d'administration Autoprint.**
Cette interface Web vous permet de piloter l'intégralité de votre parc d'impression, de configurer les déploiements automatiques pour les utilisateurs, d'activer la supervision de la santé du matériel et de programmer des rapports réguliers.

---

## 1. Accès et Connexion
L'administration est accessible via votre navigateur Web favori.

* **URL :** `https://votre-serveur-autoprint/`
* **Authentification :**
    * Utilisez votre **Compte Active Directory** habituel (AD).
    * *Ou* le compte local de secours (`admin`) si la liaison réseau AD est indisponible.

> **Sécurité :** Lors de votre première connexion, si le mot de passe est expiré ou défini par défaut, l'application vous imposera immédiatement de le remplacer par un mot de passe fort avant d'accéder aux menus.

---

## 2. Le Tableau de Bord & Supervision (Nouveauté V2)

Le tableau de bord centralise l'état global de vos services :
* **État du Spouleur Windows :** Vérifiez instantanément si le service spouleur tourne sur le serveur hôte.
* **Supervision en arrière-plan (Monitoring) :** Autoprint interroge périodiquement (Ping + SNMP) les imprimantes de votre parc (selon la plage horaire configurée). L'interface indique en temps réel le statut réseau (En ligne, Hors ligne, Alerte ou Panne).
* **Prédiction d'épuisement de toner :** Pour chaque imprimante supervisée par SNMP, un moteur d'analyse étudie la vitesse de consommation de l'encre et affiche une estimation du nombre de jours restants avant la panne sèche.
* **Archivage automatique :** Les imprimantes non détectées ou débranchées depuis plus de 30 jours sont automatiquement déplacées vers une section "Archives" pour ne pas encombrer vos rapports d'activité.

---

## 3. Ajouter une Imprimante (Workflow de base)

Autoprint utilise une logique structurée pour assurer la cohérence entre votre base de données et le spouleur d'impression. Suivez ces étapes :

### Étape A : Définir le Lieu
1. Allez dans **Gestion > Lieux**.
2. Créez un lieu (ex: "Bâtiment A - Étage 2").
3. Renseignez la plage IP (au format CIDR, ex: `192.168.10.0/24`). Les agents clients utiliseront cette plage pour savoir quelles imprimantes proposer à l'utilisateur selon sa localisation réseau.

### Étape B : Définir le Profil SNMP de la Marque (Si non-standard)
Si l'imprimante ne répond pas aux requêtes de niveau de toner génériques :
1. Allez dans **Gestion > Profils SNMP**.
2. Créez un profil pour la marque et le modèle en saisissant les identifiants d'objets (OIDs) spécifiques fournis par le constructeur, ou importez un profil existant au format JSON.

### Étape C : Vérifier le Pilote
Autoprint se base sur les pilotes installés sur le serveur Windows.
1. Allez dans **Gestion > Pilotes**.
2. Cliquez sur **Scanner** pour lister les pilotes du serveur.
3. Vérifiez que le pilote requis est bien marqué en vert (disponible).

### Étape D : Créer le Modèle
1. Allez dans **Gestion > Modèles**.
2. Créez la fiche (ex: "Canon iR-ADV C5535") et associez-la au pilote validé précédemment ainsi qu'au profil SNMP correspondant.

### Étape E : Déclarer l'Imprimante
1. Allez dans **Gestion > Imprimantes** et cliquez sur **Ajouter**.
2. Saisissez le nom de partage, l'adresse IP réseau, le lieu physique, le modèle et la communauté SNMP (généralement `public`).
3. **Option "Mode Filiale" (Direct Printing)** : Cochez cette case si l'imprimante est située sur un site distant. Les PC des utilisateurs imprimeront alors directement vers l'IP de l'imprimante sans saturer la liaison VPN vers le serveur d'impression central.

---

## 4. Diagnostic en Temps Réel & Staging

### Diagnostic à la demande
Dans la liste des imprimantes, un bouton **Diagnostic** (icône réseau/wifi) vous permet d'ouvrir une fenêtre d'interrogation immédiate. Autoprint effectue alors un ping en direct et interroge la machine par SNMP pour récupérer les niveaux de consommables et les codes d'erreur à l'instant T.

### La Synchronisation ("Staging")
Quand vous créez ou modifiez une imprimante dans la console, **l'action n'est pas répercutée immédiatement sur le serveur Windows**. Vos modifications sont placées dans un état "En Attente".
1. Un bandeau vous signale les changements en attente.
2. Cliquez sur **Synchroniser** pour afficher la prévisualisation :
    * 🟢 **Vert** : Création sur Windows (ports, partages, files d'attente).
    * 🟠 **Orange** : Modifications.
    * 🔴 **Rouge** : Suppressions du serveur d'impression.
3. Validez pour appliquer physiquement sur le spouleur Windows.

---

## 5. Rapports Planifiés par E-mail (Nouveauté V2)

Vous pouvez recevoir régulièrement des comptes-rendus de l'état de votre parc :
1. Allez dans **Supervision > Rapports & Planification**.
2. Cliquez sur **Planifier un Rapport**.
3. Choisissez le format (un document **PDF** propre à imprimer ou un fichier **CSV** importable dans Excel).
4. Saisissez les e-mails des destinataires et définissez la fréquence d'envoi (quotidien, hebdomadaire, mensuel).
5. L'application enverra les états, les compteurs de pages et les alertes de consommables automatiquement via le serveur de messagerie SMTP configuré.

---

## 6. Jetons d'Intégration API (Nouveauté V2)

Si vous souhaitez connecter Autoprint à votre outil de supervision interne (ex. GLPI, PRTG, Zabbix) :
1. Allez dans **Paramètres > Jetons d'Intégration**.
2. Générez un nouveau jeton d'accès sécurisé.
3. Utilisez ce jeton dans l'en-tête HTTP `X-Api-Token` de vos requêtes pour consommer les endpoints d'export de données d'Autoprint et lier vos systèmes.

---

## 7. Gestion des Rôles & Utilisateurs
Vous pouvez déléguer des tâches à des techniciens ou des gestionnaires de stocks sans leur donner l'accès administrateur complet :
* Allez dans **Administration > Utilisateurs**.
* Créez des comptes locaux ou importez des groupes depuis votre Active Directory.
* Attribuez-leur des rôles spécifiques. Grâce au système RBAC, un utilisateur peut être limité à la lecture seule ou restreint aux seules fonctions de gestion des rapports sans pouvoir modifier les imprimantes système.

---

## 8. Sauvegarde, Restauration & Dépannage

### Journal d'Audit
Toutes les actions d'administration sont loguées dans **Système > Journal d'Audit** (qui a fait quoi, quand et depuis quelle IP, avec le comparatif avant/après modif).

### Audit Écart Serveur
Le bouton **Audit Serveur** compare la base de données avec le Spouleur Windows réel et vous alerte en cas d'écart (ex: si une imprimante a été supprimée à la main directement dans la console d'impression Windows).

### Restauration en cas de désastre
Vous pouvez exporter l'intégralité des métadonnées du parc au format JSON depuis l'onglet **Paramètres > Maintenance**. Ce fichier permettra de restaurer la configuration complète de l'application sur un nouveau serveur en cas de panne matérielle majeure.