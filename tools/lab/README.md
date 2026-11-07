# Labo de test Autoprint (Hyper-V + AutomatedLab)

Labo local et jetable pour dérouler `Docs/_PROCEDURE_TESTS_SECURITE.md` dans des conditions proches de la production : domaine Active Directory, serveur membre vierge, poste client.

| VM | Adresse | Rôle |
|---|---|---|
| `SRV-AD` | 192.168.99.10 | Contrôleur du domaine `lab.autoprint.test` (NetBIOS `LAB`) et DNS. Fausse imprimante : capture des travaux envoyés au port 9100 dans `C:\PrintCapture\Jobs`. |
| `SRV-PRINT` | 192.168.99.20 | Serveur membre **vierge** : c'est l'installeur Autoprint qui doit y installer IIS, le spouleur et le runtime .NET. |
| `CLT-TEST` | 192.168.99.50 | Poste Windows 11 **hors domaine** (client de bureau), comme dans la procédure de test. Compte local `CLT-TEST\LabAdmin`, même mot de passe. Optionnel : `-SansClient`. |

Réseau privé `AutoprintNet` (192.168.99.0/24), joignable depuis le poste hôte, sans accès Internet.
AutomatedLab ajoute les noms des VM au fichier `hosts` de l'hôte : la console web sera sur `http://srv-print.lab.autoprint.test`.

Objets AD créés : OU `Autoprint` (sous-OU `Utilisateurs`, `Groupes`, `Comptes de service`) et groupes `GG_Autoprint_Admins`, `GG_Autoprint_Lecteurs`, `GG_Autoprint_Helpdesk` (noms de la procédure de test, § 0.1). Le compte de service et les utilisateurs `ad.user1` / `ad.user2` sont créés au moment des tests.

## Prérequis

- Hyper-V activé, module AutomatedLab installé, sources dans `D:\LabSources` avec `ISOs\WindowsServer2022.iso` et `ISOs\Windows11.iso` (versions d'évaluation).
- Environ 80 Go libres sur le SSD (`C:\AutomatedLab-VMs`), 50 Go avec `-SansClient`. Mémoire : 6 à 10 Go selon l'usage (mémoire dynamique).

## Création

Dans un PowerShell **administrateur** :

```powershell
cd D:\code\Autoprint\Autoprint\tools\lab
.\Deploy-Lab.ps1 -Recreer -AccorderAccesHyperV
```

- Si AutomatedLab n'est pas à jour, le script le met à jour puis s'arrête : le relancer dans une nouvelle fenêtre administrateur.
- `-Recreer` supprime sans autre question l'ancien labo `AutoprintLab`, ses VM, ses disques et l'ancien dossier `D:\LabVMs`.
- `-AccorderAccesHyperV` ajoute ton compte au groupe local « Administrateurs Hyper-V » (prise en compte à la prochaine ouverture de session) : nécessaire pour piloter les VM et leurs instantanés sans session administrateur.
- Le script demande le mot de passe de `LAB\LabAdmin` (administrateur du domaine et des VM du labo). Il est enregistré **chiffré pour ton compte Windows** dans `%USERPROFILE%\.autoprint-lab\labadmin.cred` ; il n'est écrit en clair nulle part. Ne réutilise pas un vrai mot de passe.
- Durée : 1 à 2 heures. À la fin, chaque VM a un instantané `Vierge`.

## Utilisation

```powershell
$cred = Import-Clixml "$env:USERPROFILE\.autoprint-lab\labadmin.cred"
Invoke-Command -VMName SRV-PRINT -Credential $cred -ScriptBlock { hostname }     # commande dans une VM (PowerShell Direct)
Copy-Item .\setup.exe -ToSession (New-PSSession -VMName SRV-PRINT -Credential $cred) -Destination C:\Install\
Checkpoint-VM -Name SRV-PRINT -SnapshotName 'Avant test'                          # instantané
Restore-VMSnapshot -VMName SRV-PRINT -Name 'Vierge' -Confirm:$false               # retour à l'état vierge
```

Impression de test : sur `SRV-PRINT`, port TCP/IP standard vers `192.168.99.10`, RAW, port 9100 ; les travaux arrivent dans `SRV-AD:C:\PrintCapture\Jobs` (journal `C:\PrintCapture\capture.log`).

## Suppression

```powershell
Remove-Lab -Name AutoprintLab -Confirm:$false
```

Les versions d'évaluation expirent (180 jours pour Windows Server, 90 jours pour Windows 11) : recréer le labo plutôt que le prolonger.
