# Labo de test Autoprint (Hyper-V, Windows en français)

Labo local pour dérouler `Docs/_PROCEDURE_TESTS_SECURITE.md` dans des conditions proches de la production : serveur Windows **français** membre d'un domaine, poste client **hors domaine** (comme un poste géré par Intune).

| VM | Adresse | Rôle |
|---|---|---|
| `SRV-AD` | 192.168.99.10 | Windows Server 2022 FR. Contrôleur du domaine `lab.autoprint.test` (NetBIOS `LAB`) et DNS. Fausse imprimante : capture des travaux reçus sur le port 9100 dans `C:\PrintCapture\Jobs`. Faux serveur SMTP (port 25, STARTTLS) : messages dans `C:\SmtpCapture\Mails`. |
| `SRV-PRINT` | 192.168.99.20 | Windows Server 2022 FR, membre du domaine, **vierge** : c'est l'installeur Autoprint qui doit y installer IIS, le spouleur et le runtime .NET. |
| `CLT-TEST` | 192.168.99.50 | Windows 11 Pro FR, groupe de travail. Le client s'y installe comme une application Win32 Intune (MSI silencieux, compte SYSTEM). |

Réseau : commutateur interne `AutoprintNet` (192.168.99.0/24), l'hôte est en 192.168.99.1. Pas d'accès Internet.
Objets AD : OU `Autoprint` (`Utilisateurs`, `Groupes`, `Comptes de service`), groupes `GG_Autoprint_Admins`, `GG_Autoprint_Lecteurs`, `GG_Autoprint_Helpdesk`. Les comptes de test sont créés au moment des tests.

> Pourquoi pas AutomatedLab : sur un hôte Windows récent (build 26300), son `bcdboot` laisse les images de base sans magasin de démarrage, et ses scripts supposent un Windows anglais. L'installation à la main depuis les ISO françaises évite les deux problèmes.

## Mise en place

1. **Accès Hyper-V** (une fois, PowerShell administrateur, puis fermer et rouvrir la session) :
   ```powershell
   Add-LocalGroupMember -Group (Get-LocalGroup -SID 'S-1-5-32-578') -Member "$env:USERDOMAIN\$env:USERNAME"
   ```
2. **Mot de passe du labo**, enregistré chiffré pour ton compte Windows (jamais en clair, ne pas réutiliser un vrai mot de passe) :
   ```powershell
   New-Item -ItemType Directory -Force "$env:USERPROFILE\.autoprint-lab" | Out-Null
   Get-Credential -UserName 'LAB\LabAdmin' -Message 'Mot de passe du labo' | Export-Clixml "$env:USERPROFILE\.autoprint-lab\labadmin.cred"
   ```
3. **VM** : génération 2, Secure Boot (modèle Microsoft Windows), TPM pour `CLT-TEST`, commutateur `AutoprintNet`, disques dans `D:\AutomatedLab-VMs` (déplacés de C le 11/11/2026 : place sur le disque système). ISO : `SERVER_2022_EVAL_x64FRE_fr-fr.iso` et Windows 11 FR (édition grand public : choisir **Professionnel**).
4. **Installation de Windows à la main** :
   - serveurs : « Datacenter Evaluation (expérience de bureau) », mot de passe Administrateur = mot de passe du labo ;
   - Windows 11 : à l'écran « il est temps de vous connecter à un réseau », Maj+F10 puis `start ms-cxh:localonly`, compte local `LabAdmin` avec le mot de passe du labo.
   S'arrêter à la mire de connexion ; ne rien configurer d'autre.
5. **Configuration** (session normale, sans élévation) : `.\Configure-Lab.ps1`. Environ 20 minutes ; instantanés `OS installé` (à faire avant) et `Labo configuré` (fait par le script).

## Utilisation

```powershell
$pw = (Import-Clixml "$env:USERPROFILE\.autoprint-lab\labadmin.cred").Password
$dom = [pscredential]::new('LAB\Administrateur', $pw)     # SRV-AD, SRV-PRINT
$clt = [pscredential]::new('.\LabAdmin', $pw)             # CLT-TEST
Invoke-Command -VMName SRV-PRINT -Credential $dom -ScriptBlock { hostname }        # PowerShell Direct
Copy-Item .\setup.exe -ToSession (New-PSSession -VMName SRV-PRINT -Credential $dom) -Destination C:\Install\
Restore-VMSnapshot -VMName SRV-PRINT -Name 'Labo configuré' -Confirm:$false        # retour à l'état de départ
```

E-mails de test : Autoprint › Paramètres › SMTP : `srv-ad.lab.autoprint.test`, port 25, TLS (STARTTLS) et identifiant au choix ; les messages arrivent dans `SRV-AD:C:\SmtpCapture\Mails` (`.eml`, journal `C:\SmtpCapture\capture.log` ; le mot de passe SMTP n'est jamais enregistré).

SQL Server (tests NR-01, NR-10, S8-04 à S8-06, SD-04) : `.\Install-LabSql.ps1` installe SQL Server 2025 Express sur SRV-PRINT (instance `SQLEXPRESS`, TCP 1433, mode mixte, identifiant `autoprint.sql` pour S8-06). Installeur à télécharger depuis download.microsoft.com (`SQLEXPR_x64_ENU.exe`) dans `%USERPROFILE%\.autoprint-lab\dl\sql2025`. Redémarrer SRV-PRINT avant d'installer Autoprint.

SNMP (tests SV3-*) : `.\Install-LabSnmp.ps1` installe snmpsim sur SRV-AD (Python portable : `%USERPROFILE%\.autoprint-lab\dl\pybundle.zip`, avec snmpsim, pysmi et cryptography). Imprimantes simulées : `192.168.99.10` en SNMPv3 authPriv SHA-256 / AES-128 (utilisateur `autoprint.v3`, mots de passe chiffrés dans `recette-secrets.xml`) et `192.168.99.11` en v2c (communauté `parc`). Données : `snmpsim\data`. `snmpsim\run_snmpsim.py` sert les requêtes v3 sans nom de contexte (comme une vraie imprimante).

Après une restauration d'instantané, réactiver « Interface de services d'invité » sur la VM (nécessaire à `Copy-VMFile`).

Impression de test : sur `SRV-PRINT`, port TCP/IP standard vers `192.168.99.10`, RAW, port 9100 ; les travaux arrivent dans `SRV-AD:C:\PrintCapture\Jobs` (journal `C:\PrintCapture\capture.log`).

Les versions d'évaluation expirent (180 jours pour Windows Server) : revenir à l'instantané ne prolonge pas la licence.
