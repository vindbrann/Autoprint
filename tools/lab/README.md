# Labo de test Autoprint (Hyper-V, Windows en français)

Labo local pour dérouler `Docs/_PROCEDURE_TESTS_SECURITE.md` dans des conditions proches de la production : serveur Windows **français** membre d'un domaine, poste client **hors domaine** (comme un poste géré par Intune).

| VM | Adresse | Rôle |
|---|---|---|
| `SRV-AD` | 192.168.99.10 | Windows Server 2022 FR. Contrôleur du domaine `lab.autoprint.test` (NetBIOS `LAB`) et DNS. Fausse imprimante : capture des travaux reçus sur le port 9100 dans `C:\PrintCapture\Jobs`. |
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
3. **VM** : génération 2, Secure Boot (modèle Microsoft Windows), TPM pour `CLT-TEST`, commutateur `AutoprintNet`, disques dans `C:\AutomatedLab-VMs`. ISO : `SERVER_2022_EVAL_x64FRE_fr-fr.iso` et Windows 11 FR (édition grand public : choisir **Professionnel**).
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

Impression de test : sur `SRV-PRINT`, port TCP/IP standard vers `192.168.99.10`, RAW, port 9100 ; les travaux arrivent dans `SRV-AD:C:\PrintCapture\Jobs` (journal `C:\PrintCapture\capture.log`).

Les versions d'évaluation expirent (180 jours pour Windows Server) : revenir à l'instantané ne prolonge pas la licence.
