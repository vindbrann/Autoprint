#  Autoprint

**Autoprint** est une solution centralisée de gestion et de déploiement d'imprimantes pour environnements Windows, conçue sur la plateforme **.NET 10**.

Son objectif est de simplifier l'administration du parc d'impression tout en garantissant une sécurité maximale sur les postes clients grâce à une architecture à séparation de privilèges.

-----

##  Architecture Globale

La solution repose sur une architecture distribuée Client/Serveur sécurisée :

```mermaid
graph TD
    subgraph SERVEUR [" SERVEUR (Back-Office)"]
        API[API REST .NET 10]
        Admin[Admin UI (Blazor WASM)]
        DB[(SQL Server)]
        API <--> DB
        Admin <--> API
    end

    subgraph CLIENT [" POSTE CLIENT (Windows)"]
        Service[Service Windows (LocalSystem)]
        UI[App WPF (Session Utilisateur)]
        
        UI -- "IPC (Named Pipes)" --> Service
    end

    Service -- "SignalR + HTTPS" --> API
```

-----

##  Composants du Système

### 1\. Le Serveur (Cerveau)

Le point central de la solution qui orchestre les configurations.

  * **API Core (.NET 10)** : Expose les endpoints REST pour les clients et l'administration. Gère l'authentification et la logique métier.
  * **Interface d'Administration (Blazor WASM + Radzen)** : Permet aux techniciens de :
      * Définir les files d'impression.
      * Associer des imprimantes à des groupes ou des postes.
      * Monitorer l'état du parc en temps réel via **SignalR**.

### 2\. Le Client (Agent)

Installé sur les postes utilisateurs, il est conçu selon le principe de **Moindre Privilège**. Il est scindé en deux processus distincts :

#### A. Le Service Windows (`Autoprint.Service`)

  * **Rôle** : L'ouvrier qualifié.
  * **Contexte** : S'exécute en arrière-plan sous le compte **LocalSystem**.
  * **Responsabilités** :
      * Installation / Suppression des pilotes d'impression (nécessite des droits Admin).
      * Modification du Spooler Windows.
      * Communication sécurisée avec l'API Serveur.

#### B. L'Interface Utilisateur (`Autoprint.Client.UI`)

  * **Rôle** : La fenêtre de dialogue.
  * **Contexte** : S'exécute dans la **session de l'utilisateur** standard (WPF).
  * **Responsabilités** :
      * Afficher les notifications (ex: "Nouvelle imprimante installée").
      * Permettre à l'utilisateur de choisir ses préférences (imprimante par défaut).
      * **Sécurité** : Cette interface n'a *aucun* droit administratif. Elle envoie des commandes au Service via un canal local sécurisé.

-----

##  Flux de Communication

1.  **Serveur vers Service (Réseau)** :

      * Le Serveur notifie le Service d'un changement via **SignalR** (WebSockets).
      * Le Service télécharge la configuration via l'API REST sécurisée.

2.  **Service vers UI (Local)** :

      * Le Service et l'UI communiquent via **Named Pipes** (IPC).
      * Protocole JSON strict pour éviter les injections.

-----

##  Stack Technique

  * **Framework** : .NET 10 (Dernière version LTS/Preview).
  * **Base de données** : Microsoft SQL Server + Entity Framework Core.
  * **Backend** : ASP.NET Core Web API.
  * **Frontend Admin** : Blazor WebAssembly.
  * **Desktop** : WPF (Windows Presentation Foundation) & Windows Services.
  * **Communication** : System.IO.Pipes, SignalR.

-----
