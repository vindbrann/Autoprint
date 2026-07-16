# 📝 Notes de Version - Autoprint V2 (Patch Notes)

Cette mise à jour apporte des améliorations majeures pour simplifier la gestion quotidienne de votre parc d'imprimantes, anticiper les pannes et renforcer la sécurité globale de vos installations.

---

## 🚀 Nouvelles Fonctionnalités (Supervision)

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
