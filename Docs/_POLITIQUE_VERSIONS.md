# 🔄 Politique de mise à jour des technologies

**Décidé le :** 04/11/2026
**Principe :** Autoprint doit toujours tourner sur des technologies **à jour**, pour profiter sans délai des correctifs de sécurité et ne pas accumuler de dette technique (une montée de version repoussée devient plus coûteuse et plus risquée à chaque version sautée).

---

## 1. Règles

| Type de mise à jour | Exemple | Délai | Comment |
|---|---|---|---|
| **Correctif de sécurité** (vulnérabilité publiée) | ASP.NET Core 10.0.x, ImageSharp | **Immédiat** : dans la semaine | Dependabot + workflow « Contrôles de sécurité » (bloquant). Publication d'une version corrective. |
| **Version corrective / mineure** | 10.0.13 → 10.0.14, PDFsharp 6.2 → 6.5 | À **chaque version** d'Autoprint | Revue `dotnet list package --outdated` avant chaque publication. |
| **Version majeure** d'une bibliothèque | Radzen 8 → 12, WiX 6 → 7 | **Planifiée** : jamais plus d'une version majeure de retard | Chantier dédié (notes de version, adaptation du code, recette complète). |
| **Plateforme .NET** | .NET 10 → .NET 11 / 12 | Dans les **6 mois** suivant la sortie d'une nouvelle version | Toujours rester sur une version supportée par Microsoft ; ne jamais atteindre la fin de support. |
| **Runtime installé sur les serveurs** (Hosting Bundle) | 10.0.10 → 10.0.14 | À **chaque version** d'Autoprint | Embarquer la dernière version dans l'installeur ; l'installeur doit mettre à jour un runtime trop ancien. |
| **Bibliothèques web embarquées** (`wwwroot/lib`) | Bootstrap, Chart.js | À **chaque version** d'Autoprint | Non vues par Dependabot : vérification manuelle (voir § 3). |

Exception possible : une mise à jour **bloquée** par une autre dépendance (ex. Microsoft.OpenApi 3 tant qu'ASP.NET Core l'interdit). Elle est alors notée ci-dessous avec la condition de déblocage, et revue à chaque version.

---

## 2. État au 04/11/2026 (branche `feature/serveur-sans-localsystem`)

| Composant | Version actuelle | Dernière | État | Action |
|---|---|---|---|---|
| .NET (projets) | net10.0 | — | ✅ supportée (LTS, jusqu'à nov. 2028) | Évaluer .NET 11 dès sa sortie (prévue en novembre 2026). |
| Paquets Microsoft.* / System.* | 10.0.14 | 10.0.14 | ✅ | Suivi Dependabot. |
| **SDK de compilation** (poste de build) | 10.0.100 | 10.0.1xx récent | ⚠️ | Mettre à jour le SDK du poste de build (le workflow GitHub utilise déjà la dernière 10.0.x). |
| **Hosting Bundle embarqué dans l'installeur** | **10.0.10** | 10.0.14 | ⚠️ **retard sur les correctifs du runtime** | Remplacer `Resources/dotnet-hosting-10.exe` à chaque version (fichier non versionné dans git). Voir backlog B38. |
| **Détection du runtime par l'installeur** | « tout 10.x convient » | — | ⚠️ un serveur existant n'est jamais mis à jour | Exiger une version minimale et proposer la mise à jour. Voir backlog B39. |
| PDFsharp | 6.2.4 | 6.5.0 | 🟡 mineure | À faire à la prochaine version (recontrôler un rapport PDF). |
| Radzen.Blazor | 8.3.8 | 12.1.11 | 🔴 4 majeures de retard | Chantier dédié + recette complète de la console (backlog B12). |
| WiX Toolset (MSI) | 6.0.2 | 7.0.1 | 🔴 majeure | Vérifier d'abord les conditions de licence de WiX 7 (backlog B12). |
| Microsoft.Data.SqlClient (installeur) | 6.1.3 | 7.1.1 | 🔴 majeure | Chantier dédié avec recette de l'installeur (backlog B12). |
| Microsoft.OpenApi | 2.13.0 | 3.11.0 | ⏸️ **bloquée** | Débloquée quand ASP.NET Core (Microsoft.AspNetCore.OpenApi) acceptera la 3.x (probablement .NET 11). |
| SQLitePCLRaw | 2.1.14 | 3.x | ⏸️ **bloquée** | Débloquée quand EF Core passera à SQLitePCLRaw 3. |
| Blazored.LocalStorage | 4.5.0 | 4.5.0 | 🟡 plus de nouvelle version | Projet tiers peu actif : prévoir son remplacement par un petit service maison (interop JavaScript), voir backlog B41. |
| Bootstrap (embarqué) | 5.3.0 | 5.3.8 | 🟡 | Mettre à jour `wwwroot/lib` (backlog B40). |
| Bootstrap Icons (embarqué) | 1.11.0 | 1.13.2 | 🟡 | Idem. |
| Chart.js (embarqué) | 4.4.1 | 4.6.1 | 🟡 | Idem (vérifier le graphique du tableau de bord). |
| SharpSnmpLib, MaterialDesign, Swagger UI, PDFsharp (dépendances) | — | — | ✅ | — |

---

## 3. Processus

1. **En continu** — Dependabot (NuGet chaque semaine, GitHub Actions chaque mois) et workflow « Contrôles de sécurité » (à chaque push et chaque lundi) : toute vulnérabilité connue fait échouer la compilation.
2. **Avant chaque version d'Autoprint** (liste de contrôle) :
   - `dotnet list Autoprint.sln package --outdated` : appliquer toutes les mises à jour correctives et mineures ;
   - remplacer le **Hosting Bundle** embarqué par la dernière version de la branche .NET utilisée ;
   - mettre à jour les **bibliothèques web embarquées** (`Autoprint.Web/wwwroot/lib`) : télécharger la version voulue, vérifier son empreinte, conserver le fichier de licence ;
   - mettre à jour le **SDK** du poste de build ;
   - relire la section 2 de ce document et la mettre à jour.
3. **Chaque trimestre** — revue des versions majeures en retard et du calendrier de support .NET ; planification des chantiers correspondants.
4. **À la sortie d'une nouvelle version de .NET** — évaluation dans le mois, migration dans les 6 mois.
