#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Crée (ou recrée) le labo de test Autoprint sous Hyper-V avec AutomatedLab.

.DESCRIPTION
    Labo « AutoprintLab », réseau privé 192.168.99.0/24, domaine lab.autoprint.test (NetBIOS LAB) :
      - SRV-AD     192.168.99.10  contrôleur de domaine + DNS, et fausse imprimante (capture des travaux sur le port 9100)
      - SRV-PRINT  192.168.99.20  serveur membre VIERGE : l'installeur Autoprint doit y installer lui-même IIS, le spouleur et .NET
      - CLT-TEST   192.168.99.50  poste Windows 11 hors domaine (optionnel, -SansClient pour ne pas le créer)

    Étapes : mise à jour d'AutomatedLab si besoin, suppression de l'ancien labo, saisie du mot de passe du compte LAB\LabAdmin
    (enregistré chiffré pour l'utilisateur courant, jamais en clair), installation, objets AD de test, instantané « Vierge ».

.PARAMETER Recreer
    Supprime sans autre question le labo AutoprintLab existant, ses VM et ses disques (y compris l'ancien dossier D:\LabVMs).

.PARAMETER AccorderAccesHyperV
    Ajoute le compte courant au groupe local « Administrateurs Hyper-V » (réglage de sécurité du poste), pour piloter les VM
    sans session administrateur. Prend effet à la prochaine ouverture de session Windows.

.EXAMPLE
    # PowerShell en administrateur
    .\Deploy-Lab.ps1 -Recreer -AccorderAccesHyperV
#>
[CmdletBinding()]
param(
    [switch]$Recreer,
    [switch]$SansClient,
    [switch]$AccorderAccesHyperV,
    [switch]$SansMiseAJour,
    [string]$VmPath = 'C:\AutomatedLab-VMs',
    [string]$AncienVmPath = 'D:\LabVMs'
)

$ErrorActionPreference = 'Stop'
$LabName      = 'AutoprintLab'
$DomainName   = 'lab.autoprint.test'
$AdminUser    = 'LabAdmin'
$OsServeur    = 'Windows Server 2022 Datacenter Evaluation (Desktop Experience)'
$OsClient     = 'Windows 11 Enterprise Evaluation'
$CredFolder   = Join-Path $env:USERPROFILE '.autoprint-lab'
$CredFile     = Join-Path $CredFolder 'labadmin.cred'
$ScriptFolder = $PSScriptRoot

function Step([string]$text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

# ---------------------------------------------------------------- 1. AutomatedLab à jour
Step '1. AutomatedLab'
$installed = (Get-Module -ListAvailable AutomatedLab | Sort-Object Version -Descending | Select-Object -First 1).Version
if (-not $installed) { throw "AutomatedLab n'est pas installé (Install-Module AutomatedLab -Scope AllUsers)." }
Write-Host "Version installée : $installed"
if (-not $SansMiseAJour) {
    $latest = (Find-Module AutomatedLab -Repository PSGallery).Version
    if ([version]$latest -gt [version]$installed) {
        Write-Host "Mise à jour vers $latest..." -ForegroundColor Yellow
        Update-Module AutomatedLab -RequiredVersion $latest -Force
        Write-Host "AutomatedLab $latest installé. Relance ce script dans une NOUVELLE fenêtre PowerShell (administrateur)." -ForegroundColor Green
        return
    }
}
Import-Module AutomatedLab -WarningAction SilentlyContinue
$labSources = Get-LabSourcesLocation
Write-Host "Sources du labo : $labSources"
foreach ($iso in 'WindowsServer2022.iso', 'Windows11.iso') {
    if (-not (Test-Path (Join-Path $labSources "ISOs\$iso"))) { throw "ISO manquante : $labSources\ISOs\$iso" }
}

# ---------------------------------------------------------------- 2. Accès Hyper-V (optionnel)
if ($AccorderAccesHyperV) {
    Step '2. Groupe « Administrateurs Hyper-V »'
    # Nom du groupe indépendant de la langue (SID S-1-5-32-578)
    $group = Get-LocalGroup -SID 'S-1-5-32-578'
    $me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    if (Get-LocalGroupMember -Group $group | Where-Object Name -eq $me) {
        Write-Host "$me est déjà membre de « $($group.Name) »."
    } else {
        Add-LocalGroupMember -Group $group -Member $me
        Write-Host "$me ajouté à « $($group.Name) ». Ferme puis rouvre ta session Windows pour que ce soit pris en compte." -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------- 3. Suppression de l'ancien labo
Step '3. Ancien labo'
$exists = (Get-Lab -List) -contains $LabName
if ($exists -or (Test-Path $AncienVmPath)) {
    if (-not $Recreer) {
        $answer = Read-Host "Le labo $LabName (ou le dossier $AncienVmPath) existe. Tout supprimer, VM et disques compris ? (oui/non)"
        if ($answer -ne 'oui') { Write-Host 'Abandon : rien n''a été supprimé.'; return }
    }
    if ($exists) {
        Write-Host "Suppression du labo $LabName (VM, disques, commutateur, entrées du fichier hosts)..."
        Remove-Lab -Name $LabName -Confirm:$false
    }
    foreach ($path in $AncienVmPath, $VmPath) {
        if (Test-Path $path) {
            $vhdInUse = Get-ChildItem $path -Recurse -Include *.vhdx, *.avhdx -ErrorAction SilentlyContinue |
                Where-Object { try { [IO.File]::Open($_.FullName, 'Open', 'Read', 'None').Dispose(); $false } catch { $true } }
            if ($vhdInUse) { throw "Des disques de $path sont encore utilisés : $($vhdInUse.FullName -join ', ')" }
            Write-Host "Suppression de $path..."
            Remove-Item $path -Recurse -Force
        }
    }
} else {
    Write-Host 'Aucun ancien labo.'
}

# ---------------------------------------------------------------- 4. Place sur le disque
$drive = (Split-Path $VmPath -Qualifier).TrimEnd(':')
$freeGb = [math]::Round((Get-PSDrive $drive).Free / 1GB)
$neededGb = if ($SansClient) { 50 } else { 80 }
Write-Host "`nEspace libre sur ${drive}: $freeGb Go (besoin estimé : $neededGb Go)"
if ($freeGb -lt $neededGb) { throw "Pas assez de place sur ${drive}: pour le labo." }

# ---------------------------------------------------------------- 5. Compte du labo
Step '5. Compte administrateur du labo'
Write-Host "Choisis le mot de passe de LAB\$AdminUser (administrateur du domaine et des VM du labo, 8 caractères min., majuscule, chiffre, symbole)."
Write-Host 'Ne réutilise PAS un vrai mot de passe : il est réservé à ce labo.' -ForegroundColor Yellow
do {
    $p1 = Read-Host 'Mot de passe' -AsSecureString
    $p2 = Read-Host 'Confirmation' -AsSecureString
    $plain1 = [Net.NetworkCredential]::new('', $p1).Password
    $plain2 = [Net.NetworkCredential]::new('', $p2).Password
    $ok = $plain1 -eq $plain2 -and $plain1.Length -ge 8 -and $plain1 -cmatch '[A-Z]' -and $plain1 -match '\d' -and $plain1 -match '[^a-zA-Z0-9]'
    if (-not $ok) { Write-Host 'Mots de passe différents ou trop simples, recommence.' -ForegroundColor Red }
} until ($ok)
$plain2 = $null
New-Item -ItemType Directory -Force $CredFolder | Out-Null
# Chiffré par DPAPI pour l'utilisateur Windows courant : lisible seulement par ce compte sur ce poste
[pscredential]::new("LAB\$AdminUser", $p1) | Export-Clixml $CredFile
Write-Host "Identifiant enregistré (chiffré) : $CredFile"

# ---------------------------------------------------------------- 6. Définition du labo
Step '6. Définition'
New-LabDefinition -Name $LabName -DefaultVirtualizationEngine HyperV -VmPath $VmPath
Add-LabVirtualNetworkDefinition -Name 'AutoprintNet' -AddressSpace 192.168.99.0/24
Set-LabInstallationCredential -Username $AdminUser -Password $plain1
Add-LabDomainDefinition -Name $DomainName -AdminUser $AdminUser -AdminPassword $plain1
$plain1 = $null

$PSDefaultParameterValues = @{
    'Add-LabMachineDefinition:Network'    = 'AutoprintNet'
    'Add-LabMachineDefinition:UserLocale' = 'fr-FR'
    'Add-LabMachineDefinition:TimeZone'   = 'Romance Standard Time'
    'Add-LabMachineDefinition:Processors' = 2
}

Add-LabMachineDefinition -Name SRV-AD -OperatingSystem $OsServeur -IpAddress 192.168.99.10 -DomainName $DomainName `
    -Roles (Get-LabMachineRoleDefinition -Role RootDC) -Memory 2GB -MinMemory 1GB -MaxMemory 3GB

# Volontairement sans rôle ni fonctionnalité : l'installeur Autoprint doit installer IIS, le spouleur et le runtime .NET
Add-LabMachineDefinition -Name SRV-PRINT -OperatingSystem $OsServeur -IpAddress 192.168.99.20 -DomainName $DomainName `
    -Memory 4GB -MinMemory 2GB -MaxMemory 6GB

if (-not $SansClient) {
    # Hors domaine (groupe de travail), comme le poste client de la procédure de test (§ 0.1)
    Add-LabMachineDefinition -Name CLT-TEST -OperatingSystem $OsClient -IpAddress 192.168.99.50 -DnsServer1 192.168.99.10 `
        -Memory 4GB -MinMemory 2GB -MaxMemory 6GB `
        -HypervProperties @{ EnableTpm = 'True'; EnableSecureBoot = 'On'; SecureBootTemplate = 'MicrosoftWindows' }
}

# ---------------------------------------------------------------- 7. Installation
Step '7. Installation (1 à 2 heures)'
Install-Lab

# ---------------------------------------------------------------- 8. Configuration de test
Step '8. Objets AD de test et fausse imprimante'
Invoke-LabCommand -ComputerName SRV-AD -ActivityName 'OU et groupes Autoprint' -ScriptBlock {
    $base = (Get-ADDomain).DistinguishedName
    if (-not (Get-ADOrganizationalUnit -Filter "Name -eq 'Autoprint'")) {
        New-ADOrganizationalUnit -Name 'Autoprint' -Path $base -ProtectedFromAccidentalDeletion $false
    }
    $ou = "OU=Autoprint,$base"
    foreach ($sub in 'Utilisateurs', 'Groupes', 'Comptes de service') {
        if (-not (Get-ADOrganizationalUnit -Filter "Name -eq '$sub'" -SearchBase $ou)) {
            New-ADOrganizationalUnit -Name $sub -Path $ou -ProtectedFromAccidentalDeletion $false
        }
    }
    # Noms attendus par Docs/_PROCEDURE_TESTS_SECURITE.md (§ 0.1)
    foreach ($g in 'GG_Autoprint_Admins', 'GG_Autoprint_Lecteurs', 'GG_Autoprint_Helpdesk') {
        if (-not (Get-ADGroup -Filter "Name -eq '$g'")) {
            New-ADGroup -Name $g -GroupScope Global -GroupCategory Security -Path "OU=Groupes,$ou"
        }
    }
}

Copy-LabFileItem -Path (Join-Path $ScriptFolder 'PrintCapture.ps1') -ComputerName SRV-AD -DestinationFolderPath 'C:\PrintCapture'
Invoke-LabCommand -ComputerName SRV-AD -ActivityName 'Fausse imprimante (port 9100)' -ScriptBlock {
    New-Item -ItemType Directory -Force 'C:\PrintCapture\Jobs' | Out-Null
    if (-not (Get-NetFirewallRule -Name 'Autoprint-PrintCapture-9100' -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name 'Autoprint-PrintCapture-9100' -DisplayName 'Autoprint labo : capture impression 9100' `
            -Direction Inbound -Protocol TCP -LocalPort 9100 -RemoteAddress 192.168.99.0/24 -Action Allow | Out-Null
    }
    $action  = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File C:\PrintCapture\PrintCapture.ps1'
    $trigger = New-ScheduledTaskTrigger -AtStartup
    $settings = New-ScheduledTaskSettingsSet -RestartCount 99 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName 'Autoprint-PrintCapture' -Action $action -Trigger $trigger -Settings $settings -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
    Start-ScheduledTask -TaskName 'Autoprint-PrintCapture'
}

# ---------------------------------------------------------------- 9. Instantané de référence
Step '9. Instantané « Vierge »'
Checkpoint-LabVM -All -SnapshotName 'Vierge'

Show-LabDeploymentSummary
Write-Host "`nLabo prêt. Identifiant : $CredFile ; console web future : http://srv-print.$DomainName" -ForegroundColor Green
