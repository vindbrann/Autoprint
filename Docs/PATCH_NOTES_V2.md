# 📝 Notes de Version - Autoprint V2 (Patch Notes)

Cette mise à jour apporte des améliorations majeures pour simplifier la gestion quotidienne de votre parc d'imprimantes, anticiper les pannes et renforcer la sécurité globale de vos installations.

---

## 🌟 Nouveautés - Version 1.1.15

### 🖨️ 1. Refonte complète des Profils SNMP Dynamiques
* **Gestion multi-consommables personnalisée** : Fin de la limitation aux 4 toners rigides. Chaque profil d'imprimante gère désormais une liste dynamique et illimitée d'OIDs :
  * **Toners & Encres** : Prise en charge des imprimantes monochromes (N&B), couleur 4 toners, ou machines spécifiques (toners photo, vernis) avec choix des codes couleurs visuels.
  * **Bacs à papier (Trays)** : Suivi en temps réel des niveaux de papier de chaque bac avec calcul automatique exact (pourcentage ou nombre de feuilles selon la capacité maximale RFC 3805).
  * **Compteurs de pages** : Relevé des compteurs totaux et spécifiques.
  * **Numéros de série** : Lecture directe de l'identifiant matériel de la machine.
  * **Consoles LCD & Alertes** : Récupération des messages de l'écran de l'imprimante (porte ouverte, bourrage, code panne).
  * **Consommables de maintenance** : Suivi des bacs de récupération de toner usagé, tambours (*drums*) et unités de fusion (*fusers*).
* **⚡ Auto-détection des OIDs (Scan intelligent)** : Scannez en direct une imprimante de test pour découvrir et configurer automatiquement tous ses toners, bacs et compteurs physiques en un clic.
* **🧪 Test d'OID individuel en direct** : Bouton permettant de tester instantanément la réponse d'un OID sur une adresse IP avant d'enregistrer le champ.
* **Modèles prédéfinis RFC 3805** : Boutons d'insertion rapide des OIDs standards du marché.

### 📊 2. Évolutions majeures des Rapports d'Activité
* **Modularité totale du contenu** : Vous composez votre rapport à la carte en activant uniquement les éléments souhaités :
  * 📄 *Compteur de pages individuel*
  * 🧪 *Niveaux de consommables (Toners)*
  * 📦 *Niveaux des bacs de papier*
  * ⚡ *Disponibilité réseau (Ping)*
  * ⚠️ *Pannes et alertes matérielles*
  * ⏳ *Prévisions intelligentes d'épuisement*
* **🔢 Totalisation globale des impressions** : Option permettant de calculer et d'afficher le volume cumulé total de pages imprimées sur l'ensemble de votre parc (dans l'encart d'en-tête du PDF et en ligne de total final dans l'export Excel/CSV).
* **📈 Synthèse globale du parc** : Statistiques globales en tête de document (total imprimantes suivies, réparties en ligne / hors ligne, alertes actives).
* **🏷️ Intégration du Numéro de Série** : Colonne dédiée dans les exports PDF et CSV pour identifier formellement chaque matériel.
* **🎯 Filtrage par Profil SNMP** : Possibilité de cibler la génération d'un rapport selon le type de profil SNMP configuré.
* **✨ Rendu PDF haute fidélité** : Suppression des caractères d'emojis incompatibles avec les polices système, éliminant tout carré noir de substitution pour un document 100 % propre et professionnel.

### 🔧 3. Améliorations Matérielles & Base de Données
* **Auto-remplissage des Numéros de Série** : Lorsqu'un diagnostic ou un rapport interroge une imprimante en SNMP, son numéro de série matériel est désormais automatiquement enregistré et mis à jour en base de données.
* **Persistance Modèle ↔ Profil SNMP** : Correction et fiabilisation de l'association entre les modèles d'imprimantes et leurs profils SNMP.
* **Migrations et Initialisations automatiques** : Détection et création transparente des nouvelles colonnes et tables (SQLite et SQL Server) lors du déploiement via l'installateur, sans interruption ni intervention manuelle.

---

## 🚀 Nouvelles Fonctionnalités Initiales (Supervision)

*   **Diagnostic en temps réel** : Testez instantanément si une imprimante est en ligne sur le réseau (Ping) et récupérez son modèle ainsi que ses niveaux de toner actuels d'un simple clic.
*   **Supervision automatique** : L'application surveille désormais seule et en tâche de fond l'état de santé de toutes vos imprimantes à intervalle régulier, sans action manuelle requise.
*   **Prédiction intelligente de fin de toner** : L'application analyse l'historique d'utilisation de vos imprimantes pour estimer précisément le nombre de jours restants avant qu'un toner ne soit vide.
*   **Gestion des modèles spécifiques (Marques)** : Ajout de la possibilité de créer des configurations personnalisées (profils de communication) pour les imprimantes moins standards, avec option d'import/export de ces réglages.
*   **Nettoyage automatique du parc (Archivage)** : Les imprimantes qui ne répondent plus ou qui ont été débranchées depuis plus de 30 jours sont automatiquement archivées pour garder votre écran de contrôle propre et lisible.
*   **Rapports automatiques par e-mail** : Planifiez l'envoi automatique de rapports réguliers (ex: tous les lundis matin) contenant les compteurs, les niveaux de consommables et les alertes d'épuisement, directement dans votre boîte mail au format PDF ou Excel (CSV).
*   **Connexion simplifiée pour outils tiers** : Possibilité de générer des jetons d'accès pour connecter Autoprint de façon sécurisée à d'autres logiciels de supervision de votre entreprise.

---

## 🛡️ Corrections et Sécurité

*   **Sécurisation du service sur les ordinateurs** : Correction d'une faille critique qui aurait pu permettre à un utilisateur malveillant local d'obtenir le contrôle total de l'ordinateur (accès administrateur complet).
*   **Chiffrement des communications réseau** : Correction d'une faille qui aurait permis à un pirate présent sur le même réseau d'intercepter des clés d'accès ou d'injecter de fausses informations d'impression.
*   **Protection lors de l'installation d'imprimantes** : Correction d'une faille qui aurait permis à un attaquant d'exécuter des commandes malveillantes sur le poste de l'utilisateur lors de l'installation d'une imprimante réseau.
*   **Protection de l'annuaire d'entreprise (Active Directory)** : Correction d'une vulnérabilité qui aurait pu perturber ou contourner les requêtes d'authentification et de recherche d'utilisateurs.
*   **Renforcement de la sécurité des mots de passe** : Remplacement de l'ancien système de stockage des mots de passe locaux par une méthode de cryptage moderne et robuste, qui aurait empêché un pirate de deviner ou déchiffrer rapidement les mots de passe même s'il accédait aux données du serveur.
*   **Sécurisation de l'accès administrateur** : Correction d'une faille de configuration qui aurait permis à une personne malveillante de s'identifier comme administrateur global en exploitant une clé de sécurité par défaut non modifiée.
*   **Amélioration de la validation des mots de passe (Gestion des utilisateurs)** : Ajout d'une validation locale (6 caractères minimum) à la création et modification des utilisateurs, et refonte de la gestion des erreurs d'API pour afficher les messages de validation de manière lisible dans un bandeau d'alerte de la modal au lieu d'une popup de notification JSON brute.
*   **Correction des autorisations sur les Profils SNMP** : Correction d'une anomalie qui bloquait à tort les utilisateurs ayant des droits SNMP avec le message « Vous n'avez pas l'autorisation de gérer la configuration serveur ». La page vérifie désormais la permission `SNMP_PROFILE_READ` et s'affiche de manière dynamique (champs et boutons d'édition ou de suppression grisés ou masqués) selon les droits d'écriture et suppression.
*   **Mise à niveau de sécurité des dépendances** : Résolution de plusieurs vulnérabilités de sécurité sur les packages NuGet (`Microsoft.OpenApi`, `SixLabors.ImageSharp`, `SQLitePCLRaw`) et correction d'avertissements de nullité du compilateur C# pour assainir et fiabiliser la compilation du projet.
