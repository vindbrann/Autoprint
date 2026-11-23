# Journal des versions

Les versions suivent le format `AA.M.J.N` (année, mois, jour, numéro de la journée). Le détail technique de chaque correction est dans le [backlog](Docs/projet/BACKLOG_AMELIORATIONS.md).

## Non publié

### Nouveautés

- **Consommables : des états au lieu de pourcentages inventés** (#49) : une imprimante qui ne communique pas son niveau (« il en reste ») affichait 100 %. Le diagnostic affiche maintenant OK, Bas, Vide ou Inconnu, et le niveau seulement quand l'imprimante le donne.
- **Toner bas détecté par l'indicateur d'erreurs standard** (#49) : sur une imprimante monochrome, le toner passe à « Bas » ou « Vide » quand l'imprimante le signale, même sans niveau. Sur une couleur, une alerte l'indique.
- **Alertes avec gravité** (#49) : en plus de l'écran de l'imprimante, le diagnostic lit les alertes standard (bourrage, capot ouvert, consommable absent…) avec leur gravité. Le statut de supervision (Avertissement, Critique) s'appuie dessus.
- **Section Maintenance** (#49) : unité de fusion, photoconducteur et kits ont leur jauge, au lieu de passer dans les alertes ou d'être mélangés aux toners.
- **Prévision affichée** (#49) : le nombre de jours restants estimé s'affiche sous la jauge, quand il y a assez de relevés.
- **Détection automatique des consommables et des bacs** (#50) : une imprimante dont le modèle n'a pas de profil SNMP affiche maintenant tous ses consommables, éléments de maintenance et bacs, avec leurs noms, couleurs et types lus directement dans l'imprimante, au lieu du seul premier toner. Les bacs de récupération sont reconnus (« % libre », « Presque plein », « Plein »). Compatible SNMPv1, v2c et v3 ; en v1, un élément absent ne bloque plus les autres.
- **Supervision plus rapide sur les grands parcs** (#50) : jusqu'à 8 imprimantes interrogées en même temps, et la description de chaque imprimante est mémorisée (relue après un redémarrage ou toutes les 24 h).
- **Seuil « bas » réglable** (#49) : **Paramètres > Supervision**, 20 % par défaut (au lieu de 10 % codé en dur), pour avoir le temps de commander.

### Corrections

- **Diagnostic SNMP en thème sombre** (#46) : les messages de l'imprimante sont de nouveau lisibles, la carte des alertes suit le thème, et la jauge du toner noir se distingue du fond. Le pourcentage s'affiche à côté de la jauge, lisible quelle que soit la couleur.
- **Libellés SNMP accentués** (#46) : les textes envoyés par l'imprimante dans une page de code DOS ou Windows sont décodés correctement (« Unité de fusion » au lieu de « Unit� De Fusion »), y compris dans les profils déjà enregistrés.
- **Uptime** : « 1 heure », « 1 jour » au singulier.
- **Noms proposés par l'auto-découverte** (#46) : l'unité de fusion ou le kit de maintenance ne prennent plus le préfixe « Toner ». Les bacs portent leur nom et leur format (« Tray 1 – A4 ») au lieu du code brut `iso-a4-white`. Dans le diagnostic, les bacs déjà enregistrés avec ce code s'affichent « Bac 1 – A4 ».

## 26.11.22.1 — 22/11/2026

### Nouveautés

- **Connexion SNMP dans le profil** (#40) : le profil SNMP définit maintenant tout ce qu'il faut pour lire un modèle : la version (v1, v2c ou v3), la communauté ou l'identifiant SNMPv3, et les OID. Une imprimante peut avoir sa propre connexion (**Connexion propre à cette imprimante**). L'identifiant SNMP du modèle disparaît.
- **Diagnostic plus accessible** (#43) : une icône devant le nom de chaque imprimante, au lieu d'un bouton en bout de ligne.
- **Fiche imprimante** (#42) : le bandeau affiche le nom et le nom du partage, sans `\\serveur\`, et suit la règle « Même nom pour le partage ».

### Corrections

- **Audit serveur** (#41) : il ne signale plus toutes les imprimantes « Introuvable » en mode Service. Il lit les files Windows comme la synchronisation, et s'arrête sans rien modifier s'il ne peut pas les lire. Plusieurs imprimantes sur la même adresse IP ne sont plus signalées « Nom incorrect ».

### À savoir pour la mise à jour

- Une imprimante dont la communauté n'est pas « public », ou réglée autrement qu'en v2c, garde sa propre connexion. Les autres prennent celle du profil SNMP de leur modèle.
- Un identifiant SNMPv3 choisi sur un **modèle** n'est pas repris : choisissez-le dans le profil SNMP du modèle.

## 26.11.13.1 — 13/11/2026

La plus grosse version depuis la 1.1 : nouvelle interface, nouveau client, sécurité revue de fond en comble. La mise à jour directe depuis 1.1.x est prise en charge (voir [Mettre à jour](Docs/guides/mise-a-jour.md)).

### Nouveautés

- **Nouvelle charte graphique** sur la console, le client, les installeurs et les rapports PDF : thème clair et sombre, police IBM Plex, nouveau logo.
- **Client entièrement repensé** : un seul panneau près de l'horloge. On y trouve ses imprimantes, l'imprimante par défaut par site, l'ajout et le retrait sans fenêtre Windows, la recherche sans accents, et l'impression sur un autre site.
- **Console** :
  - filtre et tri sur toutes les colonnes de tous les tableaux ;
  - fenêtres de saisie refaites (imprimante, lieu, modèle, rôle, profil SNMP, rapport en trois étapes) ;
  - paramètres regroupés par section ;
  - règle de nommage avec aperçu en direct et renommage du parc.
- **SNMPv3** et **profils d'identifiants SNMP** réutilisables. Une imprimante sans profil SNMP est lue avec les valeurs standard.
- **Changement de l'adresse de la console sans couper les postes** : le serveur accepte plusieurs noms, gérés dans la console. Le client sépare l'adresse de la console (`SERVERURL`) du serveur d'impression (`PRINTSERVER`).
- **Déconnexion après inactivité**, réglable.
- **Rapports PDF** plus légers, générés avec SkiaSharp (licence MIT) à la place de PDFsharp, passé sous licence AGPL.

### Sécurité

- Le site web tourne sans privilège, sous l'identité de son pool IIS. Les cinq opérations d'administration du spouleur passent par un petit service dédié (`AutoprintSpooler`), joignable seulement par le site. L'ancien service local des postes est supprimé.
- Les secrets (mots de passe AD et SMTP, clé d'API, identifiants SNMP) sont chiffrés en base. La sauvegarde peut les inclure, protégés par un mot de passe.
- Le serveur n'accepte que les noms autorisés : protection contre l'usurpation de l'en-tête `Host`.
- Les mots de passe locaux font au moins 12 caractères. Un compte est bloqué temporairement après plusieurs échecs, et le nombre de connexions par adresse est limité.
- La clé d'API change avec une période de transition, et le MSI applique la nouvelle valeur lors d'une mise à jour.
- Le client vérifie le certificat HTTPS du serveur.

### Corrections notables

- La mise à jour des serveurs SQLite installés en 1.1.x fonctionne à nouveau.
- Les marques, modèles et lieux en double sont refusés.
- Un refus du serveur s'affiche dans la console au lieu d'un faux « Succès ».
- La synchronisation ne peut plus toucher la file Windows d'une autre imprimante.
- La sauvegarde et la restauration conservent le réseau principal des lieux et les champs vides.
- Toutes les actions du journal d'audit ont un libellé : plus de « CODE NON TRADUIT » pour les jetons, rapports et profils SNMP.

### Technique

- .NET 10.0.15 (LTS), Radzen 13, SqlClient 7, WiX 7, SkiaSharp 4.156.
- MSI serveur allégé (de 72 à 39 Mo).
- Licence clarifiée : MIT, titulaire unique, composants tiers listés dans `THIRD-PARTY-NOTICES.txt`.

## 26.08.24.3 et versions antérieures

- **26.08.24.3** : client à fenêtre unique avec onglets, exécutable autonome, thème sombre.
- **26.08.24.2** : droit « Scan réseau », secrets masqués dans la console, protection contre l'injection de formules dans les exports CSV, sauvegarde complète.
- **26.08.24.1** : profils SNMP dynamiques (toners, bacs, compteurs, numéros de série, alertes), détection automatique des OID.
- **1.1.0** : première version publique.

Le détail de ces versions est dans les [anciennes notes de version](Docs/archives/PATCH_NOTES_V2.md).
