<#
.SYNOPSIS
    Installe SQL Server 2025 Express (instance SQLEXPRESS, TCP 1433, mode mixte) sur une VM du labo.

.DESCRIPTION
    Pour les tests SQL Server de la procédure (NR-01, NR-10, S8-04 à S8-06, SR-05).
    Le mot de passe « sa » et celui de l'identifiant SQL « autoprint.sql » (dbcreator, test S8-06) sont générés,
    enregistrés chiffrés dans %USERPROFILE%\.autoprint-lab\recette-secrets.xml (clés sql.sa, sql.autoprint) et jamais affichés.
    Administrateurs SQL : LAB\Administrateur et BUILTIN\Administrateurs.

.PARAMETER Setup
    SQLEXPR_x64_ENU.exe (téléchargé depuis download.microsoft.com, signature Microsoft vérifiée).
#>
param(
    [string]$VMName = 'SRV-PRINT',
    [string]$Setup = "$env:USERPROFILE\.autoprint-lab\dl\sql2025\SQLEXPR_x64_ENU.exe"
)
$ErrorActionPreference = 'Stop'
$dir = "$env:USERPROFILE\.autoprint-lab"
$secretsFile = Join-Path $dir 'recette-secrets.xml'
$secrets = if (Test-Path $secretsFile) { Import-Clixml $secretsFile } else { @{} }
function New-Pw {
    $alpha = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
    $b = New-Object byte[] 18; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    ConvertTo-SecureString ("Sq-" + (-join ($b | ForEach-Object { $alpha[$_ % $alpha.Length] })) + "!7") -AsPlainText -Force
}
foreach ($k in 'sql.sa', 'sql.autoprint') { if (-not $secrets.ContainsKey($k)) { $secrets[$k] = New-Pw } }
$secrets | Export-Clixml $secretsFile

if ((Get-AuthenticodeSignature $Setup).Status -ne 'Valid') { throw "Signature invalide : $Setup" }
$pw = (Import-Clixml "$dir\labadmin.cred").Password
$cred = [pscredential]::new('LAB\Administrateur', $pw)
$s = New-PSSession -VMName $VMName -Credential $cred
try {
    Invoke-Command $s { New-Item -ItemType Directory -Force C:\Install | Out-Null }
    Write-Host "Copie de l'installeur vers $VMName…"
    Copy-Item $Setup -ToSession $s -Destination C:\Install\SQLEXPR_x64_ENU.exe -Force

    Write-Host "Installation de SQL Server (5 à 10 min)…"
    $code = Invoke-Command $s -ArgumentList $secrets['sql.sa'] {
        param($sa)
        $plain = [Net.NetworkCredential]::new('', $sa).Password
        $p = Start-Process C:\Install\SQLEXPR_x64_ENU.exe -ArgumentList '/Q', '/x:C:\Install\SQL2025' -Wait -PassThru
        if ($p.ExitCode -ne 0) { return "extraction $($p.ExitCode)" }
        $admins = '"LAB\Administrateur" "BUILTIN\Administrateurs"'
        $setupArgs = "/Q /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS /SQLSYSADMINACCOUNTS=$admins " +
                "/SECURITYMODE=SQL /SAPWD=`"$plain`" /TCPENABLED=1 /UPDATEENABLED=0 /IACCEPTSQLSERVERLICENSETERMS"
        $p = Start-Process C:\Install\SQL2025\setup.exe -ArgumentList $setupArgs -Wait -PassThru
        return $p.ExitCode
    }
    if ("$code" -ne '0' -and "$code" -ne '3010') { throw "Installation SQL Server : code $code (journal : C:\Program Files\Microsoft SQL Server\170\Setup Bootstrap\Log)" }

    Write-Host "Port TCP 1433, pare-feu, identifiant autoprint.sql…"
    Invoke-Command $s -ArgumentList $secrets['sql.autoprint'] {
        param($apw)
        $inst = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL').SQLEXPRESS
        $tcp = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$inst\MSSQLServer\SuperSocketNetLib\Tcp\IPAll"
        Set-ItemProperty $tcp TcpDynamicPorts ''; Set-ItemProperty $tcp TcpPort '1433'
        Restart-Service 'MSSQL$SQLEXPRESS' -Force
        netsh advfirewall firewall delete rule name="LABO : SQL Server 1433" | Out-Null
        netsh advfirewall firewall add rule name="LABO : SQL Server 1433" dir=in action=allow protocol=TCP localport=1433 | Out-Null
        $plain = [Net.NetworkCredential]::new('', $apw).Password.Replace("'", "''")
        $sql = "IF SUSER_ID('autoprint.sql') IS NULL CREATE LOGIN [autoprint.sql] WITH PASSWORD = N'$plain', CHECK_POLICY = ON; ALTER SERVER ROLE dbcreator ADD MEMBER [autoprint.sql];"
        Add-Type -AssemblyName System.Data
        $c = New-Object Data.SqlClient.SqlConnection 'Server=localhost\SQLEXPRESS;Integrated Security=true;TrustServerCertificate=true'
        $c.Open(); $cmd = $c.CreateCommand(); $cmd.CommandText = $sql; [void]$cmd.ExecuteNonQuery()
        $cmd.CommandText = 'SELECT @@VERSION'; ($cmd.ExecuteScalar() -split "`n")[0]; $c.Close()
    }
} finally { Remove-PSSession $s }
Write-Host "SQL Server prêt sur $VMName : serveur « $VMName\SQLEXPRESS » (ou « $($VMName.ToLower()).lab.autoprint.test,1433 »)."
