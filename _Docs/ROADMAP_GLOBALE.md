🗺️ Roadmap du Projet : Gestionnaire d'Impression Centralisé
🗓️ Phase 1 : Le Backend (API & Données)
Objectif : Avoir un serveur capable de stocker, organiser et délivrer les configurations.
1.1 Initialisation
[x] Créer la Solution Visual Studio (Autoprint).
[x] Créer le projet Web API (Autoprint.Server).
[x] Configurer le Git (Source Control).
1.2 Base de Données (SQL & EF Core)
[x] Créer les classes de Modèles (Entités) :
[x] Marque, Modele
[x] Pilote (avec champs Checksum/Hash)
[x] Emplacement (avec champ CIDR)
[x] Imprimante (Relations + Flags)
[x] Configurer le DbContext Entity Framework.
[x] Configurer la chaîne de connexion (appsettings.json).
[x] Créer la première Migration (Add-Migration Initial).
[x] Mettre à jour la base de données (Update-Database).
1.3 API de Base (CRUD)
[x] Créer les Contrôleurs API pour chaque entité (GET, POST, PUT, DELETE).
[x] Tester les endpoints via Swagger (ajouter une marque, un lieu, une imprimante).
1.4 Gestion des Fichiers (Pilotes)
[x] Créer un service de stockage (pour sauvegarder les fichiers .inf/.zip sur le disque serveur).
[x] Créer un endpoint d'Upload de fichier.
[x] Implémenter le calcul automatique du Hash SHA256 à l'upload (Sécurité).
1.5 Gestion Système (Spouleur Windows Serveur) Objectif : Que l'API puisse créer/supprimer réellement des partages Windows.
[x] Créer un Service PrintSpoolerService (Interface IPrintSpoolerService).
[x] Implémenter la création de port TCP/IP (via WMI/CIM).
[x] Implémenter la création de l'imprimante partagée (via System.Management ou PowerShell).
[x] Connecter ce service au ImprimantesController (Quand on fait un POST, on crée aussi l'objet Windows).
1.6 Logs et Audit (Traçabilité)
[x] Créer la table AuditLogs (Id, Date, User, Action, Details).
[x] Créer la table SystemErrors (pour les remontées de bugs clients).
[x] Implémenter un service de Log global dans l'API (Serilog ou NLog).


1.7 Administration Serveur & Configuration
[x] Créer la table ServerSettings (Clé, Valeur, Type, Description) pour stocker la config (SMTP, Chemins) en BDD.
[x] Créer le SettingsController pour lire/écrire ces configurations.
[x] Créer le Service IEmailService (Configuration SMTP + Envoi de mail HTML).
[x] Créer le Contrôleur DashboardController (Endpoints pour récupérer les stats : count printers, count errors, etc.).

🗓️ Phase 2 : Interface d'Administration (Web UI)
Objectif : Permettre aux techniciens de remplir la base de données sans passer par des requêtes SQL.
2.1 Structure Web (Blazor ou React/Angular selon choix)
[x] Initialiser le projet Frontend (dans la même solution).
[x] Connecter le Frontend à l'API.
[x] Initialiser le projet Frontend (Migration vers .NET 10 effectuée).
2.2 Pages de Gestion & Staging (Workflow Validé)
[x] Page "Gestion des Lieux" (CRUD).
[x] Page "Gestion des Modèles & Marques" (CRUD).
[x] Refonte "Gestion des Imprimantes" :
[x] Remplacer les champs ID par des listes déroulantes (Select).
[x] Implémenter le champ Status (Synchronisé / En Attente).
[x] Système de Naming (Template) :
[x] Créer la logique de remplacement de tokens ({LIEU}, {IP}) en C#.
[x] Intégrer la proposition automatique dans le formulaire d'ajout.
[ ] Module de Synchronisation (Le bouton "Appliquer") :
[ ] Créer le composant visuel "Résumé des changements" (Liste des diffs).
[ ] Connecter le bouton "Appliquer" au Service Windows (via l'API).
2.3 Module d'Importation
[ ] Créer une page "Découverte Réseau".
[ ] Implémenter le scan SNMP ou WMI (pour lister les imprimantes d'un serveur existant).
[ ] Créer l'assistant d'importation (Convertir une imprimante détectée en objet BDD).

2.4 Dashboard & Configuration
[ ] Dashboard : Stats globales (KPI).
[x] Page "Paramètres Avancés" :
[x] Éditeur de Template de nommage (avec zone de test "Live Preview").
[x] Configuration SMTP et Dossiers.
2.5 Sécurité & Authentification
[x] Configurer l'authentification Hybride (Locale + Active Directory) sur le Serveur. 
[x] Créer le système de Rôles et Permissions (RBAC) en base de données. 
[x] Protéger les routes de l'API (JWT Token). 
[x] Implémentation côté Client : CustomAuthStateProvider, LocalStorage et Intercepteur HTTP.
[x] Création du Layout "Secure-First" (Interface totalement masquée si non connecté).
[x] Page de Login et Menu Utilisateur (Sticky Footer).
[ ] Implémentation des politiques d'autorisation (Policies)
2.6 Administration des Accès (UI & Gestion)
[ ] Backend : Créer UsersController et RolesController (API pour gérer les comptes).
[ ] Page "Gestion des Utilisateurs" :
 [ ] Liste des utilisateurs (Locaux et AD).
 [ ] Bouton "Ajouter un utilisateur local".
 [ ] Fonction "Réinitialiser le mot de passe".
 [ ] Assignation des utilisateurs aux groupes.
[ ] Page "Gestion des Rôles & Permissions" :
 [ ] Créateur de Groupes (ex: "Techniciens", "Support").
 [ ] Matrice de droits (Cocher les permissions : "Voir Imprimantes", "Supprimer Lieux"...).
[ ] Module Connecteur AD :
 [ ] Onglet dans "Paramètres" pour configurer le LDAP (Domaine, Serveur, Compte de service).
 [ ] Interface de Mapping : "Groupe AD 'Admins'" = "Rôle 'SuperAdmin'".

🗓️ Phase 3 : Le Client Utilisateur - Partie "Intelligente"
Objectif : L'application sait "où elle est" et "ce qui est disponible", mais n'installe rien.
3.1 Base du Client (WPF/WinUI)
[ ] Créer le projet Desktop Client.
[ ] Mettre en place l'icône dans la barre des tâches (TrayIcon).
[ ] Créer la fenêtre de popup (Toast notification).
3.2 Logique Réseau
[ ] Créer le service de détection d'IP locale.
[ ] Implémenter la logique de comparaison CIDR (ex: Mon IP est dans 192.168.1.0/24 ?).
[ ] Connecter le client à l'API (Mode "Pull" pour commencer).
[ ] Afficher dans l'interface : "Lieu détecté : X".
3.3 Persistance (Mode Hors-Ligne)
[ ] Mettre en place SQLite (ou fichier JSON chiffré) localement.
[ ] Sauvegarder la config reçue du serveur.
[ ] Tester : Couper le réseau, lancer l'appli, vérifier que les données s'affichent.

🗓️ Phase 4 : Le Client Utilisateur - Partie "Système" (Windows Service)
Objectif : Le cœur de la sécurité. Installer les pilotes sans droits admin.
4.1 Le Worker Service
[ ] Créer le projet "Worker Service".
[ ] Configurer pour tourner en LocalSystem.
[ ] Créer l'installateur pour enregistrer le service sur Windows.
4.2 Communication Inter-Processus (IPC)
[ ] Mettre en place gRPC ou Named Pipes.
[ ] Définir le contrat d'interface (Ex: InstallDriver(string url, string hash)).
[ ] Connecter l'UI (Client) au Service.
4.3 Moteur d'Installation (Le plus complexe)
[ ] Implémenter le téléchargement du pilote dans un dossier temporaire sécurisé.
[ ] Implémenter la vérification du Hash (Hash téléchargé == Hash BDD).
[ ] Coder l'appel système PnPUtil ou API Windows pour injecter le pilote.
[ ] Coder la commande Add-Printer (PowerShell ou WMI).
4.4 Système de Mise à Jour (Auto-Update)
[ ] Créer l'API de versioning (Le serveur dit "La version 1.2 est dispo").
[ ] Implémenter la logique de mise à jour dans le Service Windows (Télécharger -> Stop -> Replace -> Start).
[ ] Implémenter la remontée d'erreurs client vers le serveur (API Logs).


🗓️ Phase 5 : Synchronisation & Temps Réel
Objectif : Rendre l'expérience fluide et instantanée.
5.1 SignalR (WebSockets)
[ ] Configurer le Hub SignalR sur le Serveur.
[ ] Connecter le Client au Hub.
[ ] Tester le Push : Modifier une imprimante sur le Web Admin -> Voir le popup sur le client.
5.2 Logique Delta
[ ] Modifier l'API pour accepter un paramètre lastSyncDate.
[ ] Coder la logique serveur : "Retourne uniquement les objets modifiés après cette date".
[ ] Coder la logique client : "Fusionner les nouveautés avec mon cache local".
5.3 Reporting & Tâches Planifiées
[ ] Implémenter un BackgroundService (HostedService) pour les tâches récurrentes.
[ ] Créer la logique de génération de rapport (ex: "Résumé de la semaine").
[ ] Connecter le planificateur au service d'envoi d'email.



🗓️ Phase 6 : Packaging & Déploiement
Objectif : Rendre le tout installable en entreprise.
6.1 Packaging
[ ] Créer un projet d'installation (WiX Toolset ou Visual Studio Installer).
[ ] Packager le Service + l'UI + les dépendances.
[ ] Vérifier que l'installateur configure bien le démarrage automatique.
6.2 Tests Finaux
[ ] Test "Nouveau PC" (Installation propre).
[ ] Test "Mise à jour" (Déploiement d'une nouvelle version).
[ ] Test "PrintNightmare" (Vérifier qu'aucune fenêtre UAC/Admin n'apparaît).
