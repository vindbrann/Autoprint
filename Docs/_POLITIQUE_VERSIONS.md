# 🔄 Politique de mise à jour des technologies

**Décidé le :** 04/11/2026
**Principe :** Autoprint doit toujours tourner sur des technologies **à jour**, pour profiter sans délai des correctifs de sécurité et ne pas accumuler de dette technique (une montée de version repoussée devient plus coûteuse et plus risquée à chaque version sautée).

---

## 1. Règles

| Type de mise à jour | Exemple | Délai | Comment |
|---|---|---|---|
| **Correctif de sécurité** (vulnérabilité publiée) | ASP.NET Core 10.0.x, ImageSharp | **Immédiat** : dans la semaine | Dependabot + workflow « Contrôles de sécurité » (bloquant). Publication d'une version corrective. |
| **Version corrective / mineure** | 10.0.13 → 10.0.14, Chart.js 4.4 → 4.6 | À **chaque version** d'Autoprint | Revue `dotnet list package --outdated` avant chaque publication. |
| **Version majeure** d'une bibliothèque | Radzen 8 → 12, WiX 6 → 7 | **Planifiée** : jamais plus d'une version majeure de retard | Chantier dédié (notes de version, adaptation du code, recette complète). |
| **Plateforme .NET** | .NET 10 LTS → .NET 12 LTS | **Versions LTS seulement** (décision du 11/11/2026) : migration dans les **6 mois** suivant la sortie de la LTS suivante, une fois stable (premiers correctifs publiés) | Les versions STS (.NET 11, 13…) sont sautées : projet maintenu sur le temps libre, pas de suivi de montée de version tous les ans. Toujours rester sur une version supportée ; ne jamais atteindre la fin de support. Dependabot ignore les paquets Microsoft.* / System.* des versions STS (`ignore` dans `.github/dependabot.yml`, plage 11.x ; ajouter 13.x le moment venu) : les correctifs 10.0.x restent proposés. |
| **Runtime installé sur les serveurs** (Hosting Bundle) | 10.0.10 → 10.0.14 | À **chaque version** d'Autoprint | Embarquer la dernière version dans l'installeur ; l'installeur doit mettre à jour un runtime trop ancien. |
| **Bibliothèques web embarquées** (`wwwroot/lib`) | Bootstrap, Chart.js | À **chaque version** d'Autoprint | Non vues par Dependabot : vérification manuelle (voir § 3). |

Exception possible : une mise à jour **bloquée** par une autre dépendance (ex. Microsoft.OpenApi 3 tant qu'ASP.NET Core l'interdit). Elle est alors notée ci-dessous avec la condition de déblocage, et revue à chaque version.

---

## 2. État au 04/11/2026, après la phase E (branche `feature/serveur-sans-localsystem`)

| Composant | Version actuelle | Dernière | État | Action |
|---|---|---|---|---|
| .NET (projets) | net10.0 | .NET 11 (STS, 10/11/2026) | ✅ supportée (LTS, jusqu'à nov. 2028) | **.NET 11 non retenu** (STS, support jusqu'en nov. 2028 comme .NET 10 : aucun gain de support). Prochaine migration : **.NET 12 LTS** (sortie prévue en nov. 2027), quand il sera stable, au plus tard mai 2028. |
| Paquets Microsoft.* / System.* | 10.0.15 | 10.0.15 | ✅ (11/11/2026, 16 CVE corrigées par la 10.0.15) | Suivi Dependabot. |
| **SDK et runtime du poste de build** | SDK 10.0.404 / runtime 10.0.15 | SDK 10.0.404 / runtime 10.0.15 | ✅ (11/11/2026, B44) | Le workflow GitHub utilise la dernière 10.0.x. |
| **Hosting Bundle embarqué dans l'installeur** | 10.0.15 | 10.0.15 | ✅ (11/11/2026, B79) | Fichier vérifié (empreinte SHA-512 publiée par Microsoft, signature Microsoft). La compilation échoue si le fichier ne correspond pas à `HostingBundleVersion` (B38). |
| **Détection du runtime par l'installeur** | version minimale = Hosting Bundle embarqué | — | ✅ phase E | Un runtime plus ancien est signalé « trop ancien » avec un bouton « Mettre à jour » (B39). |
| ~~PDFsharp~~ → **SkiaSharp** | 4.154.0 | 4.154.0 | ✅ phase E | PDFsharp 6.5 passé en AGPL-3.0 : remplacé par SkiaSharp (Microsoft, MIT). Police embarquée « Autoprint Sans » (Liberation Sans 2.1.5, OFL ; police stable, régénération décrite dans `Autoprint.Server/Resources/Fonts/README.md`). B42. |
| Radzen.Blazor | 12.2.0 | 12.2.0 | ✅ (11/11/2026, recette console en labo à faire) | Thème `material` conservé ; la refonte (B77) passera au thème « Standard ». |
| WiX Toolset (MSI) | 7.0.1 | 7.0.1 | ✅ (11/11/2026) | Contrat OSMF v1.1 accepté par le propriétaire du projet (`AcceptEula=wix7` dans les deux `.wixproj`) : redevance aux mainteneurs **seulement au-delà de 10 000 $ de revenus annuels** tirés des projets qui utilisent WiX ; à revoir si Autoprint devient source de revenus. WiX 6 était déjà sous ce contrat (v1.0, sans seuil). **Porte de sortie** sans réécriture : compiler WiX depuis ses sources (MS-RL), le contrat ne couvrant que les versions compilées par l'éditeur. Heat (supprimé en v7) n'était plus utilisé. Alternatives libres étudiées le 11/11 (Inno Setup, NSIS, wixl, WiX.Py, WiX 5) : non retenues (pas de MSI complet, ou version plus maintenue). |
| Microsoft.Data.SqlClient (installeur) | 6.1.3 | 7.1.1 | 🔴 majeure | Chantier dédié avec recette de l'installeur (backlog B12). |
| Microsoft.OpenApi | 2.13.0 | 3.11.0 | ⏸️ **bloquée** | ASP.NET Core 10 impose la 2.x ; la 3.x arrive avec ASP.NET Core 11 (STS, non retenu) : débloquée par la migration vers .NET 12. PR Dependabot n° 9 (installeur) et n° 17 (serveur) laissées ouvertes comme pense-bête. |
| SQLitePCLRaw | 2.1.14 | 3.x | ⏸️ **bloquée** | EF Core 10 impose la 2.x ; la 3.x arrive avec EF Core 11 (STS, non retenu) : débloquée par la migration vers .NET 12. PR Dependabot n° 12 laissée ouverte ; surveiller les correctifs de sécurité de SQLite publiés en 2.1.x. |
| Blazored.LocalStorage | — | — | ✅ phase E | Supprimé : remplacé par un service maison (`LocalStorageService`), même format de stockage (B41). |
| Bootstrap (embarqué) | 5.3.8 | 5.3.8 | ✅ phase E | Empreinte d'intégrité npm vérifiée. |
| Bootstrap Icons (embarqué) | 1.13.2 | 1.13.2 | ✅ phase E | Idem. |
| Chart.js (embarqué) | 4.6.1 | 4.6.1 | ✅ phase E | Idem. |
| SharpSnmpLib, MaterialDesign, Swagger UI | — | — | ✅ | — |

---

## 3. Processus

1. **En continu** — Dependabot (NuGet chaque semaine, GitHub Actions chaque mois) et workflow « Contrôles de sécurité » (à chaque push et chaque lundi) : toute vulnérabilité connue fait échouer la compilation.
2. **Avant chaque version d'Autoprint** (liste de contrôle) :
   - `dotnet list Autoprint.sln package --outdated` : appliquer toutes les mises à jour correctives et mineures ;
   - remplacer le **Hosting Bundle** embarqué par la dernière version de la branche .NET utilisée ;
   - mettre à jour les **bibliothèques web embarquées** (`Autoprint.Web/wwwroot/lib`) : télécharger la version voulue (registre npm), vérifier son empreinte d'intégrité, conserver le fichier de licence, puis **changer le `?v=` correspondant dans `wwwroot/index.html`** ;
   - pour le Hosting Bundle : télécharger depuis `builds.dotnet.microsoft.com` (lien et empreinte SHA-512 dans `release-metadata/10.0/releases.json`), vérifier l'empreinte et la signature, remplacer `Resources/dotnet-hosting-10.exe` et relever `HostingBundleVersion` dans `Autoprint.Installer.Server.UI.csproj` ;
   - **vérifier la licence** de chaque nouvelle version (cas de PDFsharp 6.5 : passage de MIT à AGPL) ;
   - mettre à jour le **SDK** du poste de build ;
   - relire la section 2 de ce document et la mettre à jour.
3. **Chaque trimestre** — revue des versions majeures en retard et du calendrier de support .NET ; planification des chantiers correspondants.
4. **À la sortie d'une nouvelle version de .NET** — LTS : évaluation dans le mois, migration dans les 6 mois une fois stable ; STS : non retenue (noter seulement les mises à jour qu'elle bloque, et ajouter sa plage de versions à la règle `ignore` de `.github/dependabot.yml`).
