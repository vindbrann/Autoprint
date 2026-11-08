<#
.SYNOPSIS
    Faux serveur SMTP du labo : accepte tous les messages (STARTTLS et AUTH LOGIN/PLAIN acceptés) et les enregistre en .eml.

.DESCRIPTION
    Installé par Configure-Lab.ps1 sur SRV-AD (tâche planifiée « Autoprint-SmtpCapture », compte SYSTEM, au démarrage).
    Messages : C:\SmtpCapture\Mails\<date>_<ip>.eml ; journal : C:\SmtpCapture\capture.log (expéditeur, destinataires,
    identifiant AUTH, TLS oui/non ; jamais le mot de passe). Certificat TLS : magasin LocalMachine\My, sujet CN=<CertName>.
    Pour les tests : Autoprint › Paramètres › SMTP : hôte srv-ad.lab.autoprint.test, port 25, TLS au choix.
#>
param(
    [int]$Port = 25,
    [string]$Folder = 'C:\SmtpCapture',
    [string]$CertName = 'srv-ad.lab.autoprint.test'
)

$mails = Join-Path $Folder 'Mails'
$log = Join-Path $Folder 'capture.log'
New-Item -ItemType Directory -Force $mails | Out-Null
function Write-Log([string]$t) { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss}  {1}" -f (Get-Date), $t) -Encoding UTF8 }

$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object Subject -eq "CN=$CertName" | Select-Object -First 1
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Any, $Port)
$listener.Start()
Write-Log "Écoute sur le port $Port (STARTTLS : $([bool]$cert))"

while ($true) {
    $client = $listener.AcceptTcpClient()
    $remote = ($client.Client.RemoteEndPoint -as [Net.IPEndPoint]).Address.ToString()
    $stream = $client.GetStream(); $stream.ReadTimeout = 60000
    $tls = $false; $authUser = ''; $from = ''; $rcpt = New-Object System.Collections.Generic.List[string]
    $enc = New-Object System.Text.UTF8Encoding($false)
    $reader = New-Object IO.StreamReader($stream, $enc); $writer = New-Object IO.StreamWriter($stream, $enc); $writer.NewLine = "`r`n"; $writer.AutoFlush = $true
    function Send([string]$s) { $writer.WriteLine($s) }
    try {
        Send "220 srv-ad.lab.autoprint.test ESMTP Autoprint-Labo"
        while ($true) {
            $line = $reader.ReadLine(); if ($null -eq $line) { break }
            $cmd = $line.Split(' ')[0].ToUpperInvariant()
            switch ($cmd) {
                { $_ -in 'EHLO', 'HELO' } {
                    Send '250-srv-ad.lab.autoprint.test'
                    if ($cert -and -not $tls) { Send '250-STARTTLS' }
                    Send '250-AUTH LOGIN PLAIN'
                    Send '250 8BITMIME'
                }
                'STARTTLS' {
                    if (-not $cert) { Send '454 TLS indisponible'; break }
                    Send '220 Prêt pour TLS'
                    $ssl = New-Object Net.Security.SslStream($stream, $false)
                    $ssl.AuthenticateAsServer($cert, $false, [Security.Authentication.SslProtocols]::Tls12, $false)
                    $tls = $true
                    $reader = New-Object IO.StreamReader($ssl, $enc); $writer = New-Object IO.StreamWriter($ssl, $enc); $writer.NewLine = "`r`n"; $writer.AutoFlush = $true
                }
                'AUTH' {
                    $parts = $line.Split(' ')
                    if ($parts[1] -eq 'PLAIN') {
                        $b64 = if ($parts.Count -gt 2) { $parts[2] } else { Send '334 '; $reader.ReadLine() }
                        $authUser = ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64)) -split "`0")[1]
                    } else {
                        # AUTH LOGIN <identifiant en base64> (réponse initiale, client .NET) ou AUTH LOGIN seul
                        $u64 = if ($parts.Count -gt 2) { $parts[2] } else { Send '334 VXNlcm5hbWU6'; $reader.ReadLine() }
                        $authUser = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($u64))
                        Send '334 UGFzc3dvcmQ6'; $null = $reader.ReadLine()   # mot de passe lu puis ignoré, jamais enregistré
                    }
                    Send '235 Authentification acceptée'
                }
                'MAIL' { $from = $line.Substring(10).Trim(); Send '250 OK' }
                'RCPT' { $rcpt.Add($line.Substring(8).Trim()); Send '250 OK' }
                'DATA' {
                    Send '354 Fin par <CRLF>.<CRLF>'
                    $sb = New-Object Text.StringBuilder
                    while (($l = $reader.ReadLine()) -ne '.') { if ($null -eq $l) { break }; [void]$sb.AppendLine($(if ($l.StartsWith('..')) { $l.Substring(1) } else { $l })) }
                    $file = Join-Path $mails ("{0:yyyyMMdd-HHmmss-fff}_{1}.eml" -f (Get-Date), $remote)
                    [IO.File]::WriteAllText($file, $sb.ToString(), $enc)
                    Write-Log ("Message de {0} : de {1} à {2} ; TLS={3} ; AUTH={4} ; {5:N0} octets -> {6}" -f $remote, $from, ($rcpt -join ','), $tls, $authUser, $sb.Length, (Split-Path $file -Leaf))
                    $rcpt.Clear(); Send '250 Message reçu'
                }
                'RSET' { $rcpt.Clear(); Send '250 OK' }
                'NOOP' { Send '250 OK' }
                'QUIT' { Send '221 Au revoir'; break }
                default { Send '502 Commande non gérée' }
            }
            if ($cmd -eq 'QUIT') { break }
        }
    } catch {
        Write-Log "Erreur avec $remote : $($_.Exception.Message)"
    } finally {
        $client.Dispose()
    }
}
