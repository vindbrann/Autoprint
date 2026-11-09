<#
.SYNOPSIS
    Fausse imprimante réseau du labo : écoute le port 9100 (RAW / JetDirect) et enregistre chaque travail reçu.

.DESCRIPTION
    Installée par Deploy-Lab.ps1 sur SRV-AD (tâche planifiée « Autoprint-PrintCapture », compte SYSTEM, au démarrage).
    Chaque connexion produit un fichier C:\PrintCapture\Jobs\<date>_<ip-source>.prn ; journal : C:\PrintCapture\capture.log.
    Pour les tests : créer sur SRV-PRINT une imprimante avec un port TCP/IP standard vers 192.168.99.10, protocole RAW, port 9100.
#>
param(
    [int]$Port = 9100,
    [string]$Folder = 'C:\PrintCapture'
)

$jobs = Join-Path $Folder 'Jobs'
$log  = Join-Path $Folder 'capture.log'
New-Item -ItemType Directory -Force $jobs | Out-Null

function Write-Log([string]$text) {
    Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss}  {1}" -f (Get-Date), $text) -Encoding UTF8
}

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Any, $Port)
$listener.Start()
Write-Log "Écoute sur le port $Port"

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        $remote = ($client.Client.RemoteEndPoint -as [Net.IPEndPoint]).Address.ToString()
        $file = Join-Path $jobs ("{0:yyyyMMdd-HHmmss-fff}_{1}.prn" -f (Get-Date), $remote)
        try {
            $client.ReceiveTimeout = 60000
            $stream = $client.GetStream()
            $out = [IO.File]::Create($file)
            try { $stream.CopyTo($out) } finally { $out.Dispose() }
            Write-Log ("Travail reçu de {0} : {1:N0} octets -> {2}" -f $remote, (Get-Item $file).Length, (Split-Path $file -Leaf))
        } catch {
            Write-Log "Erreur avec $remote : $($_.Exception.Message)"
        } finally {
            $client.Dispose()
        }
    }
} finally {
    $listener.Stop()
    Write-Log 'Arrêt'
}
