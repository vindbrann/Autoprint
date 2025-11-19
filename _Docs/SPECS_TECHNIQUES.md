Documentation Technique : Solution de Gestion Dynamique d'Impression
1. Présentation et Objectifs
1.1 Le Problème
Dans un environnement multi-sites, la gestion des imprimantes est complexe :
Les utilisateurs nomades peinent à trouver l'imprimante adéquate lorsqu'ils changent de bureau.
Les serveurs d'impression Windows classiques sont lourds à gérer et posent des problèmes de droits (GPO, Point & Print) sous Windows 11.
Le déploiement de masse de tous les pilotes via Intune est inefficace (poids, bande passante).
1.2 La Solution
Une architecture Client-Serveur hybride permettant :
Une gestion centralisée des ressources (Imprimantes, Pilotes, Lieux).
Une intelligence locale sur les postes clients pour détecter le réseau et adapter l'environnement d'impression.
Une installation sécurisée des pilotes sans intervention administrateur de l'utilisateur.

2. Fonctionnalités Détaillées
2.1 Serveur (Back-end & Administration)
Référentiel Central : Base de données relationnelle stockant les Marques, Modèles, Pilotes, Lieux (Nom, Code, Sous-réseaux) et Imprimantes (Nom, Code Inventaire, IP).
Gestion des Pilotes : Stockage des fichiers .inf, .cat, .cab avec calcul d'empreinte numérique (Hash) pour sécurité.
Interface Web d'Administration (Blazor WebAssembly) :
CRUD (Créer, Lire, Mettre à jour, Supprimer) sur toutes les entités.
Gestion des droits d'accès (ACL) par groupes (ex: Admin, Tech Local, Support).
Authentification Hybride : Support simultané des comptes locaux (pour administration de secours) et Active Directory. Délivrance de Tokens JWT (JSON Web Tokens) sécurisés pour les sessions stateless. 
Sécurité des mots de passe : Hachage cryptographique SHA-256 (UTF-8) avant stockage en base de données.
Scanner Réseau : Module capable de scanner un serveur d'impression existant pour importer les configurations.
Architecture UX "Secure-First" : Isolation totale de l'interface d'administration.
État Non-Connecté : Affichage exclusif de la mire de connexion (Layout minimaliste). Aucune fuite d'information sur la structure du menu.
État Connecté : Chargement dynamique du Layout complet avec Menu Latéral Fixe (Sticky Footer) affichant l'identité de l'utilisateur et la version.
Gestion de l'État Client : Utilisation d'un CustomAuthenticationStateProvider couplé au LocalStorage pour la persistance sécurisée de la session.
"Une interface dédiée permet aux administrateurs de créer des comptes locaux, de définir des rôles personnalisés via une matrice de droits (RBAC) et de configurer la liaison avec l'Active Directory sans toucher aux fichiers de configuration."

2.1.1 Module Administration Avancée
Configuration Dynamique : Gestion centralisée des paramètres serveur (Chemin de stockage des drivers avec migration automatique des fichiers existants en cas de changement de répertoire, Paramètres SMTP, Seuils d'alerte) via l'interface web, sans redémarrage du service.
Tableau de Bord (Dashboard) : Landing page affichant les KPI en temps réel (Nombre d'imprimantes actives, Top erreurs, Dernières connexions clients, État du stockage).
Reporting Automatisé : Moteur de tâches planifiées générant des rapports d'activité (PDF/HTML) envoyés par e-mail aux administrateurs (fréquence et contenue paramétrable).
Configuration Dynamique & Templates : Gestion centralisée des paramètres serveur via l'interface web.
Générateur de Noms Intelligent (Low-Code) :
Éditeur de gabarit dynamique avec prévisualisation.
Support des tokens étendus : {LIEU}, {LIEU_CODE}, {MARQUE}, {MODELE}, {IP}, {IP_LAST}, {IMP_CODE}.
Option "Même nom de partage" pour synchroniser le partage Windows avec le nom généré.
Fonction "Mise à jour en masse" (ApplyToAll) pour renommer rétroactivement le parc existant.
Gestion des Règles Conditionnelles (Mapping) : Le système supportera des tables de correspondance (Lookup Tables) permettant de transformer une valeur technique (ex: fin d'IP) en valeur métier (ex: Code Imprimante) sans écrire de code. Exemple : Si dernier octet IP = 19, alors Nom = "DIRECTEUR".
Mode "Staging" (File d'attente des modifications) :
Dissociation entre la Configuration (Base SQL) et l'État Système (Windows).
Toute modification (Ajout/Modif/Suppr) place l'objet en état "En Attente de Synchronisation".
Validation par Lots : Une interface dédiée permet de passer en revue les changements en attente avant de les appliquer réellement sur le serveur d'impression (WMI/PowerShell).
Gestion de la concurrence (Locking) pour éviter les conflits multi-administrateurs.
2.2 Client (Poste Utilisateur Windows 10/11)
Détection de Lieu : Analyse en temps réel du changement de sous-réseau IP et identification du lieu géographique associé.
Mappage Intelligent :
Proposition des imprimantes disponibles pour le lieu détecté.
Bascule automatique de l'imprimante par défaut (optionnel).
Installation "Zéro-Touche" : Installation des pilotes nécessaires à la demande, sans droits administrateurs requis pour l'utilisateur (contournement UAC via Service Windows).
Mode Hors-Ligne : Fonctionnement continu grâce à un cache local, avec synchronisation des changements (Delta) au retour de la connexion.
Interface Discrète : Application dans la zone de notification (Systray) avec popups non-intrusifs.

3. Architecture Technique et Choix Technologiques
3.1 Stack Serveur
Langage : C# / .NET 10 (ASP.NET Core Web API)..
Justification : Performance, robustesse, intégration native parfaite avec Windows Server et Active Directory.
Base de Données : SQL Server.
Justification : Standard en entreprise, intégrité référentielle forte, supporte de lourdes charges.
ORM : Entity Framework Core.
Justification : Facilite la manipulation des données et la maintenance du code.
Communication Temps Réel : SignalR (WebSockets).
Justification : Permet de "pousser" les mises à jour aux clients instantanément sans saturer le réseau par des requêtes répétitives.
Framework UI : Blazor WebAssembly (.NET 10) en mode Standalone. Architecture découplée (communique avec le serveur uniquement via API REST).
Configuration : Table ServerSettings (Key/Value) pour permettre la modification à chaud sans redémarrage du service (Hot-Reload des paramètres SMTP et Chemins).
3.2 Stack Client
L'architecture client est scindée en deux pour des raisons de sécurité (Privilege Separation) :
Service Windows :
Techno : .NET Worker Service.
Compte : LocalSystem.
Rôle : Télécharge, vérifie et installe les pilotes dans le Driver Store Windows. Exécute les tâches nécessitant des privilèges élevés.
Application UI :
Techno : WPF ou WinUI 3.
Compte : Utilisateur courant.
Rôle : Interface graphique, détection réseau, interaction utilisateur, mappage des imprimantes (une fois le pilote installé par le service).
3.3 Protocole de Communication
REST API (HTTPS) : Pour le téléchargement des binaires (pilotes) et la synchronisation initiale.
SignalR (WSS) : Pour les notifications d'événements (changement de config).
Format : JSON.
OpenAPI v3 (Swashbuckle v10)

4. Sécurité et Conformité
Cette section répond aux contraintes modernes de Windows 11 (PrintNightmare, durcissement RPC).
4.1 Gestion des Privilèges (Principe du moindre privilège)
L'utilisateur n'a jamais besoin d'être administrateur.
L'application utilisateur envoie une demande d'installation au Service Windows local via un canal sécurisé (Named Pipes ou gRPC local).
Le Service Windows intercepte la demande. Il ne l'exécute pas aveuglément.
4.2 Validation des Pilotes (Chaîne de confiance)
Le Service Windows effectue deux contrôles stricts avant toute installation :
Vérification du Hash (Intégrité) : Le fichier téléchargé est comparé à l'empreinte SHA-256 stockée en base de données serveur. Si un octet diffère, l'installation est rejetée.
Signature Numérique (Authenticité) : Vérification que le pilote est signé par une autorité de confiance (Microsoft WHQL ou Certificat Constructeur).
4.3 Contrôle d'Accès (ACL)
L'accès à l'API Serveur est protégé.
Les droits de modification (Ajout imprimante, Modif Lieu) sont basés sur l'appartenance aux groupes Active Directory.

5. Stratégie de Déploiement et Mise à Jour
5.1 Installation Initiale
Package .msi ou .intunewin déployé via Microsoft Intune (ou SCCM).
Installe le Service Windows et l'Application UI.
Enregistre l'application au démarrage de session.
5.2 Cycle de Vie (Mises à jour)
Mise à jour Config : Transparente et immédiate via SignalR/Sync.
Mise à jour Binaire (App Client) : Le client possède un module "Auto-Update" capable de télécharger une nouvelle version de lui-même depuis le serveur et de demander au Service Windows de l'appliquer.

6. Diagramme de Flux (Workflow) : Ajout d'une Imprimante
Détection : Le Client détecte le sous-réseau 192.168.10.0/24 -> Identifie le lieu "Siège - Étage 1".
Consultation : Le Client interroge son cache local : "Quelles imprimantes pour ce lieu ?".
Proposition : L'utilisateur clique sur "Installer Canon_Main_Hall".
Demande : L'UI demande au Service Windows : "Installe le pilote ID 54".
Téléchargement & Verif : Le Service télécharge le pilote, vérifie le Hash SHA256.
Installation Système : Le Service injecte le pilote dans Windows (Driver Store).
Mappage : Le Service confirme le succès. L'UI effectue la connexion finale (Add-Printer) dans la session utilisateur.

