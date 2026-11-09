<#
.SYNOPSIS
    Installe le simulateur d'imprimantes SNMP (snmpsim) sur SRV-AD, pour les tests SNMPv3 (SV3-*).

.DESCRIPTION
    Deux imprimantes simulées (données : tools\lab\snmpsim\data) :
      - 192.168.99.10:161 : SNMPv3 authPriv, utilisateur « autoprint.v3 », SHA-256 / AES-128 (numéro de série SIMV3-0001) ;
      - 192.168.99.11:161 (adresse ajoutée à SRV-AD) : v2c, communauté « parc » (numéro de série SIMV2-0002).
    Les deux répondent aussi en v2c « public ». Les mots de passe v3 sont générés, enregistrés chiffrés dans
    %USERPROFILE%\.autoprint-lab\recette-secrets.xml (clés snmp.v3.auth, snmp.v3.priv) et jamais affichés ;
    sur SRV-AD, ils sont dans C:\SnmpSim\v3.txt (lisible par SYSTEM et les administrateurs seulement).

    Prérequis sur l'hôte : bundle Python portable avec snmpsim, pysmi et cryptography
    (%USERPROFILE%\.autoprint-lab\dl\pybundle.zip : dossiers « python » et « snmpsim »).
    Tâche planifiée « Autoprint-SnmpSim » (SYSTEM, au démarrage) ; journal : C:\SnmpSim\snmpsim.log.
#>
param(
    [string]$VMName = 'SRV-AD',
    [string]$Bundle = "$env:USERPROFILE\.autoprint-lab\dl\pybundle.zip",
    [string]$V3User = 'autoprint.v3'
)
$ErrorActionPreference = 'Stop'
$dir = "$env:USERPROFILE\.autoprint-lab"
$secretsFile = Join-Path $dir 'recette-secrets.xml'
$secrets = if (Test-Path $secretsFile) { Import-Clixml $secretsFile } else { @{} }
function New-Pw {
    $alpha = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
    $b = New-Object byte[] 16; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    ConvertTo-SecureString ("Sn" + (-join ($b | ForEach-Object { $alpha[$_ % $alpha.Length] }))) -AsPlainText -Force
}
foreach ($k in 'snmp.v3.auth', 'snmp.v3.priv') { if (-not $secrets.ContainsKey($k)) { $secrets[$k] = New-Pw } }
$secrets | Export-Clixml $secretsFile

Get-VMIntegrationService -VMName $VMName | Where-Object { $_.Name -like 'Interface de services*' -or $_.Name -like 'Guest Service*' } | Enable-VMIntegrationService
Copy-VMFile -Name $VMName -SourcePath $Bundle -DestinationPath 'C:\Install\pybundle.zip' -FileSource Host -CreateFullPath -Force

$cred = [pscredential]::new('LAB\Administrateur', (Import-Clixml "$dir\labadmin.cred").Password)
Invoke-Command -VMName $VMName -Credential $cred -ArgumentList $V3User, $secrets['snmp.v3.auth'], $secrets['snmp.v3.priv'] {
    param($user, $auth, $priv)
    Unregister-ScheduledTask -TaskName 'Autoprint-SnmpSim' -Confirm:$false -ErrorAction SilentlyContinue
    Get-CimInstance Win32_Process -Filter "Name='python.exe'" | Where-Object CommandLine -like '*run_snmpsim*' | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    Expand-Archive C:\Install\pybundle.zip C:\SnmpSim -Force
    # snmpsim écrit ses clés v3 dans son journal : dossier réservé à SYSTEM et aux administrateurs
    icacls C:\SnmpSim /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null

    # Deuxième adresse pour l'imprimante v2c (jamais utilisée comme adresse source)
    $if = (Get-NetIPAddress -IPAddress 192.168.99.10).InterfaceIndex
    if (-not (Get-NetIPAddress -IPAddress 192.168.99.11 -ErrorAction SilentlyContinue)) {
        New-NetIPAddress -InterfaceIndex $if -IPAddress 192.168.99.11 -PrefixLength 24 -SkipAsSource $true | Out-Null
    }
    # Une adresse encore « Tentative » ne peut pas être écoutée : attendre qu'elle soit utilisable
    for ($i = 0; $i -lt 30 -and (Get-NetIPAddress -IPAddress 192.168.99.11).AddressState -ne 'Preferred'; $i++) { Start-Sleep 1 }
    netsh advfirewall firewall delete rule name="LABO : SNMP simulé 161" | Out-Null
    netsh advfirewall firewall add rule name="LABO : SNMP simulé 161" dir=in action=allow protocol=UDP localport=161 | Out-Null

    # Mots de passe v3 : fichier réservé à SYSTEM et aux administrateurs
    $keys = 'C:\SnmpSim\v3.txt'
    Set-Content $keys -Value @($user, [Net.NetworkCredential]::new('', $auth).Password, [Net.NetworkCredential]::new('', $priv).Password) -Encoding ASCII
    icacls $keys /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' | Out-Null

    @'
$env:HOMEPATH = '\SnmpSim'; $env:HOMEDRIVE = 'C:'
$k = Get-Content C:\SnmpSim\v3.txt
$a = @('-I', 'C:\SnmpSim\snmpsim\run_snmpsim.py', '--data-dir=C:\SnmpSim\snmpsim\data',
       '--agent-udpv4-endpoint=192.168.99.10:161', '--agent-udpv4-endpoint=192.168.99.11:161',
       "--v3-user=$($k[0])", "--v3-auth-key=$($k[1])", '--v3-auth-proto=SHA256', "--v3-priv-key=$($k[2])", '--v3-priv-proto=AES128',
       '--logging-method=file:C:\SnmpSim\snmpsim.log')
& C:\SnmpSim\python\python.exe @a
'@ | Set-Content C:\SnmpSim\Start-SnmpSim.ps1 -Encoding UTF8

    $act = New-ScheduledTaskAction -Execute powershell.exe -Argument '-NoProfile -ExecutionPolicy Bypass -File C:\SnmpSim\Start-SnmpSim.ps1'
    $trg = New-ScheduledTaskTrigger -AtStartup
    $set = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    Register-ScheduledTask -TaskName 'Autoprint-SnmpSim' -Action $act -Trigger $trg -Settings $set -User 'NT AUTHORITY\SYSTEM' -RunLevel Highest | Out-Null
    Start-ScheduledTask -TaskName 'Autoprint-SnmpSim'
    Start-Sleep 8
    Get-NetUDPEndpoint -LocalPort 161 -ErrorAction SilentlyContinue | Select-Object LocalAddress, LocalPort
}
Write-Host "Simulateur SNMP prêt : 192.168.99.10 (v3, utilisateur $V3User, SHA-256/AES-128) et 192.168.99.11 (v2c, communauté « parc »)."
