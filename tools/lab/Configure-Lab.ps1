<#
.SYNOPSIS
    Configure le labo Autoprint après une installation MANUELLE de Windows en français dans 3 VM Hyper-V.

.DESCRIPTION
    Prérequis (voir README.md) : VM SRV-AD, SRV-PRINT (Windows Server 2022 FR) et CLT-TEST (Windows 11 Pro FR) installées
    à la main, branchées sur le commutateur interne AutoprintNet (hôte 192.168.99.1), arrêtées sur la mire de connexion ;
    compte Administrateur (serveurs) / LabAdmin (Windows 11) avec le mot de passe du labo, enregistré chiffré dans
    %USERPROFILE%\.autoprint-lab\labadmin.cred ; compte courant membre de « Administrateurs Hyper-V ».

    Tout passe par PowerShell Direct (aucune configuration réseau dans les VM n'est nécessaire au départ).
    Rejouable : chaque étape vérifie d'abord si elle est déjà faite.

      SRV-AD     192.168.99.10  contrôleur du domaine lab.autoprint.test (NetBIOS LAB), DNS, fausse imprimante (port 9100)
      SRV-PRINT  192.168.99.20  serveur membre du domaine, VIERGE (l'installeur Autoprint installe IIS, le spouleur, .NET)
      CLT-TEST   192.168.99.50  poste hors domaine (comme un poste géré par Intune)
#>
[CmdletBinding()]
param(
    [string]$CredFile = (Join-Path $env:USERPROFILE '.autoprint-lab\labadmin.cred'),
    [switch]$SansInstantane
)

$ErrorActionPreference = 'Stop'
$Domain  = 'lab.autoprint.test'
$NetBios = 'LAB'
$Prefix  = '192.168.99'
$DcIp    = "$Prefix.10"

$labPassword = (Import-Clixml $CredFile).Password
$credSrvLocal = [pscredential]::new('.\Administrateur', $labPassword)
$credClient   = [pscredential]::new('.\LabAdmin', $labPassword)
$credDomain   = [pscredential]::new("$NetBios\Administrateur", $labPassword)

function Step([string]$text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

# Exécute un bloc dans la VM ; renvoie $null si la VM ne répond pas (redémarrage en cours)
function Invoke-InVM([string]$VM, [pscredential]$Cred, [scriptblock]$Script, [object[]]$Arguments = @()) {
    Invoke-Command -VMName $VM -Credential $Cred -ScriptBlock $Script -ArgumentList $Arguments -ErrorAction Stop
}

# Attend que la VM réponde avec l'un des identifiants donnés et que le test renvoie $true
function Wait-VM([string]$VM, [pscredential[]]$Creds, [scriptblock]$Test = { $true }, [int]$TimeoutMin = 20) {
    $limit = (Get-Date).AddMinutes($TimeoutMin)
    Start-Sleep 15
    while ((Get-Date) -lt $limit) {
        foreach ($c in $Creds) {
            try { if (Invoke-InVM $VM $c $Test) { return $c } } catch { }
        }
        Start-Sleep 15
    }
    throw "$VM ne répond pas après $TimeoutMin minutes"
}

# Adresse IP fixe, DNS, nom, fuseau horaire ; redémarre si le nom change
function Set-VMBase([string]$VM, [pscredential]$Cred, [string]$Ip, [string]$Dns) {
    $renamed = Invoke-InVM $VM $Cred {
        param($vmName, $ip, $dns)
        $nic = Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Select-Object -First 1
        if (-not (Get-NetIPAddress -InterfaceIndex $nic.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object IPAddress -eq $ip)) {
            Set-NetIPInterface -InterfaceIndex $nic.ifIndex -Dhcp Disabled
            Get-NetIPAddress -InterfaceIndex $nic.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Remove-NetIPAddress -Confirm:$false
            New-NetIPAddress -InterfaceIndex $nic.ifIndex -IPAddress $ip -PrefixLength 24 | Out-Null
        }
        Set-DnsClientServerAddress -InterfaceIndex $nic.ifIndex -ServerAddresses $dns
        Set-TimeZone -Id 'Romance Standard Time'
        # Ping autorisé dans le labo (diagnostic)
        Get-NetFirewallRule -Name 'FPS-ICMP4-ERQ-In*' -ErrorAction SilentlyContinue | Enable-NetFirewallRule
        if ($env:COMPUTERNAME -ne $vmName) { Rename-Computer -NewName $vmName -Force; return $true }
        return $false
    } @($VM, $Ip, $Dns)
    if ($renamed) {
        Write-Host "  $VM renommé, redémarrage..."
        Restart-VM -Name $VM -Force -Wait -For Heartbeat
    }
}

# ---------------------------------------------------------------- 0. VM démarrées
Step '0. Démarrage des VM'
foreach ($vm in 'SRV-AD', 'SRV-PRINT', 'CLT-TEST') {
    if ((Get-VM $vm).State -ne 'Running') { Start-VM $vm }
    # Le DVD d'installation n'est plus utile : le disque dur démarre en premier
    $hdd = Get-VMHardDiskDrive -VMName $vm | Select-Object -First 1
    try { Set-VMFirmware -VMName $vm -FirstBootDevice $hdd } catch { Write-Warning "$vm : ordre de démarrage inchangé ($($_.Exception.Message))" }
    Get-VMDvdDrive -VMName $vm | Set-VMDvdDrive -Path $null
}

# ---------------------------------------------------------------- 1. SRV-AD : contrôleur de domaine
Step '1. SRV-AD : contrôleur du domaine'
$isDc = $false
try { $isDc = Invoke-InVM SRV-AD $credDomain { (Get-CimInstance Win32_ComputerSystem).DomainRole -ge 4 } } catch { }
if (-not $isDc) {
    $c = Wait-VM SRV-AD @($credSrvLocal)
    Set-VMBase SRV-AD $c $DcIp '127.0.0.1'
    $c = Wait-VM SRV-AD @($credSrvLocal) { $env:COMPUTERNAME -eq 'SRV-AD' }
    Write-Host '  Installation du rôle AD DS et création de la forêt (redémarrage automatique)...'
    Invoke-InVM SRV-AD $c {
        param($domain, $netbios, $pw)
        Install-WindowsFeature AD-Domain-Services -IncludeManagementTools | Out-Null
        Install-ADDSForest -DomainName $domain -DomainNetbiosName $netbios -SafeModeAdministratorPassword $pw `
            -InstallDns -Force -NoRebootOnCompletion | Out-Null
        Restart-Computer -Force
    } @($Domain, $NetBios, $labPassword)
}
Write-Host '  Attente des services d''annuaire...'
Wait-VM SRV-AD @($credDomain) { try { (Get-ADDomain).DNSRoot } catch { $false } } -TimeoutMin 30 | Out-Null
Write-Host "  SRV-AD : contrôleur de $Domain opérationnel" -ForegroundColor Green

# ---------------------------------------------------------------- 2. SRV-PRINT : membre du domaine
Step '2. SRV-PRINT : membre du domaine'
$joined = $false
try { $joined = Invoke-InVM SRV-PRINT $credDomain { (Get-CimInstance Win32_ComputerSystem).Domain } } catch { }
if ($joined -ne $Domain) {
    $c = Wait-VM SRV-PRINT @($credSrvLocal)
    Set-VMBase SRV-PRINT $c "$Prefix.20" $DcIp
    $c = Wait-VM SRV-PRINT @($credSrvLocal) { $env:COMPUTERNAME -eq 'SRV-PRINT' }
    Invoke-InVM SRV-PRINT $c {
        param($domain, $cred)
        Add-Computer -DomainName $domain -Credential $cred -Force
        Restart-Computer -Force
    } @($Domain, $credDomain)
    Wait-VM SRV-PRINT @($credDomain) { (Get-CimInstance Win32_ComputerSystem).Domain } | Out-Null
}
Write-Host "  SRV-PRINT : membre de $Domain" -ForegroundColor Green

# ---------------------------------------------------------------- 3. CLT-TEST : hors domaine
Step '3. CLT-TEST : poste hors domaine'
$c = Wait-VM CLT-TEST @($credClient)
Set-VMBase CLT-TEST $c "$Prefix.50" $DcIp
Wait-VM CLT-TEST @($credClient) { $env:COMPUTERNAME -eq 'CLT-TEST' } | Out-Null
Write-Host '  CLT-TEST : groupe de travail, DNS du labo' -ForegroundColor Green

# ---------------------------------------------------------------- 4. Objets AD de test
Step '4. Objets AD de test'
Invoke-InVM SRV-AD $credDomain {
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
    Get-ADGroup -Filter "Name -like 'GG_Autoprint_*'" | ForEach-Object { "  groupe $($_.Name)" }
}

# ---------------------------------------------------------------- 5. Fausse imprimante sur SRV-AD
Step '5. Fausse imprimante (SRV-AD, port 9100)'
$captureScript = Get-Content (Join-Path $PSScriptRoot 'PrintCapture.ps1') -Raw
Invoke-InVM SRV-AD $credDomain {
    param($content)
    New-Item -ItemType Directory -Force 'C:\PrintCapture\Jobs' | Out-Null
    Set-Content -Path 'C:\PrintCapture\PrintCapture.ps1' -Value $content -Encoding UTF8
    if (-not (Get-NetFirewallRule -Name 'Autoprint-PrintCapture-9100' -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name 'Autoprint-PrintCapture-9100' -DisplayName 'Autoprint labo : capture impression 9100' `
            -Direction Inbound -Protocol TCP -LocalPort 9100 -RemoteAddress 192.168.99.0/24 -Action Allow | Out-Null
    }
    $action   = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File C:\PrintCapture\PrintCapture.ps1'
    $trigger  = New-ScheduledTaskTrigger -AtStartup
    $settings = New-ScheduledTaskSettingsSet -RestartCount 99 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName 'Autoprint-PrintCapture' -Action $action -Trigger $trigger -Settings $settings `
        -User 'NT AUTHORITY\SYSTEM' -RunLevel Highest -Force | Out-Null
    Start-ScheduledTask -TaskName 'Autoprint-PrintCapture'
    Start-Sleep 3
    "  écoute 9100 : " + [bool](Get-NetTCPConnection -LocalPort 9100 -State Listen -ErrorAction SilentlyContinue)
} @($captureScript)

# ---------------------------------------------------------------- 5 bis. Pilotes de test sur SRV-PRINT
Step '5 bis. Pilotes d''impression de test (SRV-PRINT)'
Invoke-InVM SRV-PRINT $credDomain {
    foreach ($d in 'Generic / Text Only', 'Microsoft PCL6 Class Driver') {
        if (-not (Get-PrinterDriver -Name $d -ErrorAction SilentlyContinue)) { Add-PrinterDriver -Name $d }
        "  pilote : $d"
    }
}

# ---------------------------------------------------------------- 6. Contrôles
Step '6. Contrôles'
Invoke-InVM SRV-PRINT $credDomain {
    param($dc)
    "  SRV-PRINT -> DNS srv-ad : " + [bool](Resolve-DnsName "srv-ad.lab.autoprint.test" -ErrorAction SilentlyContinue)
    "  SRV-PRINT -> port 9100 SRV-AD : " + (Test-NetConnection $dc -Port 9100 -WarningAction SilentlyContinue).TcpTestSucceeded
} @($DcIp)
Invoke-InVM CLT-TEST $credClient {
    "  CLT-TEST -> DNS srv-print : " + [bool](Resolve-DnsName "srv-print.lab.autoprint.test" -ErrorAction SilentlyContinue)
    "  CLT-TEST domaine/groupe de travail : " + (Get-CimInstance Win32_ComputerSystem).Workgroup
}

# ---------------------------------------------------------------- 7. Instantané
if (-not $SansInstantane) {
    Step '7. Instantané « Labo configuré »'
    Checkpoint-VM -Name SRV-AD, SRV-PRINT, CLT-TEST -SnapshotName 'Labo configuré'
}
Write-Host "`nLabo configuré." -ForegroundColor Green
