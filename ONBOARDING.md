# ONBOARDING - Synthèse du Projet Autoprint

## 1. État des Lieux
Ce document résume ma compréhension du projet après analyse des fichiers et du code existant.

### Documentation vs Réalité
- **Phase 1 (Backend)** : Terminée et fonctionnelle.
- **Phase 2 (Interface Admin)** : Bien avancée.
- **Divergences** :
  - Le fichier `SUIVI_PROJET.md` est vide.
  - La `ROADMAP_GLOBALE.md` n'est pas à jour (indique "Administration des Accès" comme non fait, alors que le code existe).
  - Le code est la source de vérité.

## 2. Stack Technique
- **Backend** : .NET 10 (ASP.NET Core Web API)
- **Frontend** : Blazor WebAssembly (.NET 10)
- **Base de Données** : SQL Server avec Entity Framework Core
- **Communication** : API REST + SignalR (prévu)
- **Sécurité** : JWT, Hachage SHA-256, RBAC (Rôles/Permissions)

## 3. Structure du Projet
- **`Autoprint.Server`** : API, Contrôleurs, Accès Données, Logique métier.
- **`Autoprint.Web`** : Interface utilisateur Blazor (Pages, Composants, Services).
- **`Autoprint.Shared`** : Modèles de données (Entités), DTOs partagés.
- **`_Docs`** : Documentation technique et roadmap.

## 4. Analyse des Fonctionnalités
- **Administration des Accès (Utilisateurs/Rôles)** :
  - **État** : Implémenté (`UsersController`, `RolesController`, `Users.razor`, `Roles.razor`).
  - **Point d'attention** : Duplication des DTOs détectée entre `Autoprint.Server` et `Autoprint.Shared`. Un refactoring sera nécessaire pour nettoyer `UsersController.cs`.
- **Module d'Importation (Découverte Réseau)** :
  - **État** : Non commencé. Aucune trace de code (SNMP, WMI, Wizard).
  - **Priorité** : C'est la prochaine tâche majeure à développer.

## 5. Prochaine Tâche Prioritaire
Basé sur l'analyse, la priorité est le **Module d'Importation** (Phase 2.3 de la roadmap).
Cependant, une **harmonisation du code existant** (nettoyage DTOs Users) serait bénéfique avant d'attaquer le gros morceau.
