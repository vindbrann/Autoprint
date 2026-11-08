<#
.SYNOPSIS
    Tests [API] de Docs/_PROCEDURE_TESTS_SECURITE.md, joués contre la console du labo (tools/lab).

.DESCRIPTION
    Affiche « OK » / « ÉCHEC » par identifiant de test de la procédure. Les mots de passe de test (admin après le premier
    changement, helpdesk, lecteur...) sont générés aléatoirement et enregistrés chiffrés pour le compte Windows courant
    dans %USERPROFILE%\.autoprint-lab\recette-secrets.xml ; ils ne sont jamais affichés.
    Le mot de passe d'origine admin123 (base neuve) vient de DbInitializer.

    Lots : Comptes (P1-23 à P1-26 + préparation des rôles et comptes), Escalade (P1-06, P1-14 à P1-22, SR-03, SR-04),
           Limitation (P1-29 à P1-33 ; bloque les connexions depuis ce poste environ 2 minutes).
#>
param(
    [string]$BaseUrl = 'https://srv-print.lab.autoprint.test',
    [ValidateSet('Comptes', 'Escalade', 'Sessions', 'Imprimantes', 'Web', 'AD', 'Limitation', 'Tout')][string[]]$Lot = 'Tout'
)

$ErrorActionPreference = 'Stop'

# Certificat auto-signé du labo (créé par la mise en place HTTPS, copié dans %USERPROFILE%\.autoprint-lab\srv-print.cer) :
# accepté par empreinte pour ce script uniquement, sans l'ajouter au magasin de confiance du poste
$LabCertFile = Join-Path $env:USERPROFILE '.autoprint-lab\srv-print.cer'
if ((Test-Path $LabCertFile) -and -not ('AutoprintLab.CertPin' -as [type])) {
    Add-Type -TypeDefinition @'
namespace AutoprintLab {
    public static class CertPin {
        public static string Thumbprint;
        public static bool Check(object s, System.Security.Cryptography.X509Certificates.X509Certificate c,
                                 System.Security.Cryptography.X509Certificates.X509Chain ch, System.Net.Security.SslPolicyErrors e) {
            if (e == System.Net.Security.SslPolicyErrors.None) return true;
            return c != null && new System.Security.Cryptography.X509Certificates.X509Certificate2(c).Thumbprint == Thumbprint;
        }
    }
}
'@
    [AutoprintLab.CertPin]::Thumbprint = (New-Object Security.Cryptography.X509Certificates.X509Certificate2 $LabCertFile).Thumbprint
    [Net.ServicePointManager]::ServerCertificateValidationCallback = [Net.Security.RemoteCertificateValidationCallback] [AutoprintLab.CertPin].GetMethod('Check').CreateDelegate([Net.Security.RemoteCertificateValidationCallback])
}
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$SecretsFile = Join-Path $env:USERPROFILE '.autoprint-lab\recette-secrets.xml'
$script:Score = @{ OK = 0; KO = 0 }
$script:Logins = New-Object System.Collections.Generic.List[datetime]

# ------------------------------------------------------------------ secrets
$script:Secrets = if (Test-Path $SecretsFile) { Import-Clixml $SecretsFile } else { @{} }
function Save-Secrets { $script:Secrets | Export-Clixml $SecretsFile }
function New-TestPassword {
    $alpha = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
    $bytes = New-Object byte[] 18; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $body = -join ($bytes | ForEach-Object { $alpha[$_ % $alpha.Length] })
    return "Rq-$body!7"
}
function Get-Plain([string]$user) {
    if (-not $script:Secrets.ContainsKey($user)) { return $null }
    return [Net.NetworkCredential]::new('', $script:Secrets[$user]).Password
}
function Set-Plain([string]$user, [string]$pw) { $script:Secrets[$user] = ConvertTo-SecureString $pw -AsPlainText -Force; Save-Secrets }

# ------------------------------------------------------------------ HTTP
function Invoke-Api([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null) {
    $req = [Net.HttpWebRequest]::Create("$BaseUrl$Path")
    $req.Method = $Method; $req.Timeout = 120000; $req.ContentType = 'application/json'
    if ($Token) { $req.Headers['Authorization'] = "Bearer $Token" }
    if ($null -ne $Body) {
        # Une chaîne est envoyée telle quelle (JSON déjà formé, ex. liste d'un seul élément que ConvertTo-Json aplatirait)
        $json = if ($Body -is [string]) { $Body } else { ConvertTo-Json -InputObject $Body -Depth 8 -Compress }
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $s = $req.GetRequestStream(); $s.Write($bytes, 0, $bytes.Length); $s.Close()
    } elseif ($Method -in 'POST', 'PUT') { $req.ContentLength = 0 }
    try { $resp = $req.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response; if (-not $resp) { throw } }
    $reader = New-Object IO.StreamReader($resp.GetResponseStream(), [Text.Encoding]::UTF8)
    $text = $reader.ReadToEnd(); $code = [int]$resp.StatusCode; $resp.Close()
    $json = $null; try { if ($text) { $json = $text | ConvertFrom-Json } } catch { }
    return [pscustomobject]@{ Code = $code; Text = $text; Json = $json }
}

# Limite du serveur : 10 connexions par minute et par adresse IP (P1-32)
function Wait-LoginBudget {
    $now = Get-Date
    $recent = @($script:Logins | Where-Object { $_ -gt $now.AddSeconds(-62) })
    if ($recent.Count -ge 8) {
        $wait = [int](62 - ($now - $recent[0]).TotalSeconds) + 1
        if ($wait -gt 0) { Write-Host "    (pause $wait s : limite de connexions par minute)" -ForegroundColor DarkGray; Start-Sleep $wait }
    }
    $script:Logins.Add((Get-Date))
}
function Invoke-Login([string]$user, [string]$pw) { Wait-LoginBudget; Invoke-Api POST '/api/auth/login' @{ username = $user; password = $pw } }
function Get-Token([string]$user) {
    $r = Invoke-Login $user (Get-Plain $user)
    if ($r.Code -ne 200) { throw "Connexion $user impossible ($($r.Code)) : $($r.Text)" }
    return $r.Json.token
}

function Check([string]$id, [string]$label, $actual, $expected, [string]$detail = '') {
    $ok = if ($expected -is [scriptblock]) { [bool](& $expected $actual) } else { $actual -eq $expected }
    if ($ok) { $script:Score.OK++; Write-Host ("OK     {0,-7} {1}  -> {2}" -f $id, $label, $actual) -ForegroundColor Green }
    else { $script:Score.KO++; Write-Host ("ÉCHEC  {0,-7} {1}  -> {2} (attendu {3})" -f $id, $label, $actual, $expected) -ForegroundColor Red }
    if ($detail -and (-not $ok -or $actual -ge 400)) { Write-Host "         $($detail.Substring(0, [Math]::Min(220, $detail.Length)))" -ForegroundColor DarkGray }
}

# ------------------------------------------------------------------ lot Comptes
function Lot-Comptes {
    Write-Host "`n--- Lot Comptes : premier login admin, rôles et comptes de recette ---" -ForegroundColor Cyan
    if (-not (Get-Plain 'admin')) {
        $r = Invoke-Login 'admin' 'admin123'
        Check 'P1-23' 'admin / admin123 sur base neuve : connexion, changement imposé' $r.Code 200 $r.Text
        $t0 = $r.Json.token
        $u = Invoke-Api GET '/api/users' -Token $t0
        Check 'P1-24' 'avant changement : GET /api/users' $u.Code 403 $u.Text
        $i = Invoke-Api GET '/api/imprimantes' -Token $t0
        Check 'P1-24' 'avant changement : GET /api/imprimantes' $i.Code 403 $i.Text
        $p = Invoke-Api GET '/api/users/profile' -Token $t0
        Check 'P1-25' 'avant changement : GET /api/users/profile' $p.Code 200
        $new = New-TestPassword
        $c = Invoke-Api PUT '/api/users/change-password' @{ currentPassword = 'admin123'; newPassword = $new; confirmPassword = $new } $t0
        Check 'P1-26' 'changement du mot de passe admin' $c.Code { param($x) $x -in 200, 204 } $c.Text
        if ($c.Code -in 200, 204) { Set-Plain 'admin' $new }
        Start-Sleep 31   # révocation vérifiée au plus tard 30 s après (cache du serveur)
        $old = Invoke-Api GET '/api/users/profile' -Token $t0
        Check 'P1-26' 'ancien jeton refusé 31 s après le changement' $old.Code 401
    }
    $ta = Get-Token 'admin'
    $u = Invoke-Api GET '/api/users' -Token $ta
    Check 'P1-26' 'nouveau mot de passe : accès complet (GET /api/users)' $u.Code 200

    $perms = @{}; (Invoke-Api GET '/api/roles/permissions' -Token $ta).Json | ForEach-Object { $perms[$_.code] = $_.id }
    $roles = @{}; (Invoke-Api GET '/api/roles' -Token $ta).Json | ForEach-Object { $roles[$_.name] = $_.id }
    foreach ($def in @(@{ n = 'Helpdesk'; p = 'USER_READ', 'USER_WRITE', 'ROLE_READ', 'ROLE_WRITE' }, @{ n = 'Lecteur'; p = @('PRINTER_READ') })) {
        if (-not $roles.ContainsKey($def.n)) {
            $r = Invoke-Api POST '/api/roles' @{ name = $def.n; description = 'Recette'; permissionIds = @($def.p | ForEach-Object { $perms[$_] }) } $ta
            Check 'prep' "création du rôle $($def.n)" $r.Code 200 $r.Text
        }
    }
    $roles = @{}; (Invoke-Api GET '/api/roles' -Token $ta).Json | ForEach-Object { $roles[$_.name] = $_.id }
    $users = @{}; (Invoke-Api GET '/api/users' -Token $ta).Json | ForEach-Object { $users[$_.username] = $_.id }
    foreach ($def in @(@{ u = 'helpdesk'; r = 'Helpdesk' }, @{ u = 'lecteur'; r = 'Lecteur' })) {
        if (-not $users.ContainsKey($def.u)) {
            $pw = New-TestPassword
            $r = Invoke-Api POST '/api/users' @{ username = $def.u; password = $pw; displayName = $def.u; email = "$($def.u)@lab.autoprint.test"; roleId = $roles[$def.r]; forceChangePassword = $false } $ta
            Check 'prep' "création du compte $($def.u)" $r.Code { param($x) $x -in 200, 201 } $r.Text
            if ($r.Code -in 200, 201) { Set-Plain $def.u $pw }
        }
    }
}

# ------------------------------------------------------------------ lot Escalade
function Lot-Escalade {
    Write-Host "`n--- Lot Escalade de privilèges (2.4, B50) ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'; $th = Get-Token 'helpdesk'; $tl = Get-Token 'lecteur'
    $perms = @{}; (Invoke-Api GET '/api/roles/permissions' -Token $ta).Json | ForEach-Object { $perms[$_.code] = $_.id }
    $roles = @{}; (Invoke-Api GET '/api/roles' -Token $ta).Json | ForEach-Object { $roles[$_.name] = $_.id }
    $users = @{}; (Invoke-Api GET '/api/users' -Token $ta).Json | ForEach-Object { $users[$_.username] = $_.id }
    $super = $roles['SuperAdmin']

    $r = Invoke-Api POST '/api/Import/Scan/Printers' @{} $tl
    Check 'P1-06' 'lecteur : scan du spouleur' $r.Code 403

    $pw = New-TestPassword
    $r = Invoke-Api POST '/api/users' @{ username = 'hd.cree'; password = $pw; displayName = 'Créé par helpdesk'; email = 'hd.cree@lab.autoprint.test'; roleId = $roles['Helpdesk']; forceChangePassword = $true } $th
    Check 'P1-14' 'helpdesk crée un compte Helpdesk' $r.Code { param($x) $x -in 200, 201 } $r.Text

    $body = @{ displayName = 'Pirate'; email = 'admin@lab.autoprint.test'; isActive = $true; roleId = $super; forceChangePassword = $false; newPassword = (New-TestPassword) }
    $r = Invoke-Api PUT "/api/users/$($users['admin'])" $body $th
    Check 'P1-15' 'helpdesk modifie le compte admin' $r.Code 403 $r.Text

    $pw2 = New-TestPassword
    $r = Invoke-Api POST '/api/users' @{ username = 'admin2'; password = $pw2; displayName = 'Admin 2'; email = 'admin2@lab.autoprint.test'; roleId = $super; forceChangePassword = $false } $ta
    if ($r.Code -in 200, 201) { Set-Plain 'admin2' $pw2 }
    $users = @{}; (Invoke-Api GET '/api/users' -Token $ta).Json | ForEach-Object { $users[$_.username] = $_.id }
    $r = Invoke-Api DELETE "/api/users/$($users['admin2'])" -Token $th
    Check 'P1-16' 'helpdesk supprime un compte SuperAdmin (admin2)' $r.Code 403 $r.Text

    $hd = (Invoke-Api GET "/api/roles/$($roles['Helpdesk'])" -Token $ta).Json
    $hd.permissionIds = @($hd.permissionIds) + $perms['SETTINGS_MANAGE']
    $r = Invoke-Api PUT "/api/roles/$($roles['Helpdesk'])" $hd $th
    Check 'P1-17' 'helpdesk ajoute Paramètres à Helpdesk' $r.Code 403 $r.Text

    $sa = (Invoke-Api GET "/api/roles/$super" -Token $ta).Json
    $r = Invoke-Api PUT "/api/roles/$super" $sa $th
    Check 'P1-18' 'helpdesk modifie SuperAdmin' $r.Code 403 $r.Text
    $r = Invoke-Api DELETE "/api/roles/$super" -Token $th
    Check 'P1-18' 'helpdesk supprime SuperAdmin (refus)' $r.Code { param($x) $x -in 400, 403 } $r.Text

    $r = Invoke-Api POST '/api/roles' @{ name = 'HD-Suppr'; description = 'Recette'; permissionIds = @($perms['PRINTER_DELETE']) } $th
    Check 'P1-19' 'helpdesk crée un rôle avec PRINTER_DELETE' $r.Code 200 $r.Text
    $r = Invoke-Api POST '/api/roles' @{ name = 'HD-Admin'; description = 'Recette'; permissionIds = @($perms['ADMIN_ACCESS']) } $th
    Check 'P1-19' 'helpdesk crée un rôle avec Administration' $r.Code 403 $r.Text

    $r = Invoke-Api POST '/api/roles/admappings' @{ adIdentifier = 'GG_Autoprint_Lecteurs'; mappingType = 0; roleId = $super } $th
    Check 'P1-20' 'helpdesk lie un groupe AD à SuperAdmin' $r.Code 403 $r.Text

    $r = Invoke-Api POST '/api/users' @{ username = 'hd.pirate'; password = (New-TestPassword); displayName = 'Pirate'; email = 'pirate@lab.autoprint.test'; roleId = $super; forceChangePassword = $false } $th
    Check 'P1-21' 'helpdesk crée un compte SuperAdmin (roleId SuperAdmin)' $r.Code 403 $r.Text

    # P1-22 : admin peut tout faire (sur des objets jetables)
    $r = Invoke-Api PUT "/api/users/$($users['admin2'])" @{ displayName = 'Admin 2 modifié'; email = 'admin2@lab.autoprint.test'; isActive = $true; roleId = $super; forceChangePassword = $false } $ta
    Check 'P1-22' 'admin modifie un compte SuperAdmin' $r.Code { param($x) $x -in 200, 204 } $r.Text
    $hd = (Invoke-Api GET "/api/roles/$($roles['Helpdesk'])" -Token $ta).Json
    $orig = @($hd.permissionIds)
    $hd.permissionIds = $orig + $perms['SETTINGS_MANAGE']
    $r = Invoke-Api PUT "/api/roles/$($roles['Helpdesk'])" $hd $ta
    Check 'P1-22' 'admin ajoute Paramètres à Helpdesk' $r.Code 204 $r.Text
    $hd.permissionIds = $orig
    Invoke-Api PUT "/api/roles/$($roles['Helpdesk'])" $hd $ta | Out-Null
    $r = Invoke-Api POST '/api/roles' @{ name = 'Admin-Jetable'; description = 'Recette'; permissionIds = @($perms['ADMIN_ACCESS']) } $ta
    Check 'P1-22' 'admin crée un rôle avec Administration' $r.Code 200 $r.Text
    if ($r.Code -eq 200) { $r2 = Invoke-Api DELETE "/api/roles/$($r.Json)" -Token $ta; Check 'P1-22' 'admin supprime ce rôle' $r2.Code 204 $r2.Text }
    $r = Invoke-Api DELETE "/api/users/$($users['admin2'])" -Token $ta
    Check 'P1-22' 'admin supprime admin2' $r.Code { param($x) $x -in 200, 204 } $r.Text

    # SR-04 : dernier administrateur
    $r = Invoke-Api PUT "/api/users/$($users['admin'])" @{ displayName = 'Administrateur'; email = 'admin@lab.autoprint.test'; isActive = $false; roleId = $super; forceChangePassword = $false } $ta
    Check 'SR-04' 'admin se désactive (dernier administrateur)' $r.Code 400 $r.Text
    $sa = (Invoke-Api GET "/api/roles/$super" -Token $ta).Json
    $sa.permissionIds = @($sa.permissionIds | Where-Object { $_ -ne $perms['ADMIN_ACCESS'] })
    $r = Invoke-Api PUT "/api/roles/$super" $sa $ta
    Check 'SR-04' 'retirer Administration à SuperAdmin (dernier administrateur)' $r.Code 400 $r.Text
}

# ------------------------------------------------------------------ lot Sessions
function Lot-Sessions {
    Write-Host "`n--- Lot Sessions : révocation des jetons (3.4) ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    $roles = @{}; (Invoke-Api GET '/api/roles' -Token $ta).Json | ForEach-Object { $roles[$_.name] = $_.id }
    $perms = @{}; (Invoke-Api GET '/api/roles/permissions' -Token $ta).Json | ForEach-Object { $perms[$_.code] = $_.id }

    # Compte A jetable avec son propre rôle (pour P2-25)
    $roleA = (Invoke-Api POST '/api/roles' @{ name = 'Sess-A'; description = 'Recette'; permissionIds = @($perms['PRINTER_READ']) } $ta).Json
    $pw = New-TestPassword
    $r = Invoke-Api POST '/api/users' @{ username = 'sess.a'; password = $pw; displayName = 'Session A'; email = 'sess.a@lab.autoprint.test'; roleId = $roleA; forceChangePassword = $false } $ta
    $idA = $r.Json.id; if (-not $idA) { $idA = ((Invoke-Api GET '/api/users' -Token $ta).Json | Where-Object username -eq 'sess.a').id }
    Set-Plain 'sess.a' $pw
    $base = @{ displayName = 'Session A'; email = 'sess.a@lab.autoprint.test'; isActive = $true; roleId = $roleA; forceChangePassword = $false }

    $cases = @(
        @{ id = 'P2-23'; label = 'compte désactivé'; act = { $b = $base.Clone(); $b.isActive = $false; Invoke-Api PUT "/api/users/$idA" $b $ta }; undo = { Invoke-Api PUT "/api/users/$idA" $base $ta } },
        @{ id = 'P2-24'; label = 'rôle changé'; act = { $b = $base.Clone(); $b.roleId = $roles['Lecteur']; Invoke-Api PUT "/api/users/$idA" $b $ta }; undo = { Invoke-Api PUT "/api/users/$idA" $base $ta } },
        @{ id = 'P2-24'; label = 'mot de passe réinitialisé'; act = { $n = New-TestPassword; Set-Plain 'sess.a' $n; $b = $base.Clone(); $b.newPassword = $n; Invoke-Api PUT "/api/users/$idA" $b $ta }; undo = { } },
        @{ id = 'P2-25'; label = 'permissions de son rôle modifiées'; act = { $ro = (Invoke-Api GET "/api/roles/$roleA" -Token $ta).Json; $ro.permissionIds = @($perms['PRINTER_READ'], $perms['LOCATION_READ']) | Where-Object { $_ }; Invoke-Api PUT "/api/roles/$roleA" $ro $ta }; undo = { } }
    )
    foreach ($c in $cases) {
        $tokA = Get-Token 'sess.a'
        $before = (Invoke-Api GET '/api/users/profile' -Token $tokA).Code
        $a = & $c.act
        Start-Sleep 31
        $after = (Invoke-Api GET '/api/users/profile' -Token $tokA).Code
        Check $c.id "$($c.label) : ancien jeton de A (avant $before, action $($a.Code))" $after 401 $a.Text
        & $c.undo | Out-Null
    }

    # P2-26 : A change lui-même son mot de passe et se reconnecte aussitôt
    $tokA = Get-Token 'sess.a'
    $n = New-TestPassword
    $r = Invoke-Api PUT '/api/users/change-password' @{ currentPassword = (Get-Plain 'sess.a'); newPassword = $n; confirmPassword = $n } $tokA
    if ($r.Code -in 200, 204) { Set-Plain 'sess.a' $n }
    $tokA2 = Get-Token 'sess.a'
    $codes = foreach ($p in '/api/users/profile', '/api/imprimantes', '/api/Dashboard') { (Invoke-Api GET $p -Token $tokA2).Code }
    Check 'P2-26' 'reconnexion immédiate après son propre changement de mot de passe (profil, imprimantes, tableau de bord)' ($codes -join ',') { param($x) $x -notmatch '401' } ($codes -join ',')

    Invoke-Api DELETE "/api/users/$idA" -Token $ta | Out-Null
    Invoke-Api DELETE "/api/roles/$roleA" -Token $ta | Out-Null
}

# ------------------------------------------------------------------ lot Imprimantes
$LabCred = $null
function Get-ServerQueues {
    if (-not $script:LabCred) {
        $pw = (Import-Clixml (Join-Path $env:USERPROFILE '.autoprint-lab\labadmin.cred')).Password
        $script:LabCred = [pscredential]::new('LAB\Administrateur', $pw)
    }
    @(Invoke-Command -VMName SRV-PRINT -Credential $script:LabCred -ScriptBlock { Get-Printer | Select-Object -ExpandProperty Name })
}
function Invoke-Sync([string]$Token) {
    $pending = (Invoke-Api GET '/api/Sync/preview' -Token $Token).Json
    if (-not $pending) { return [pscustomobject]@{ Code = 0; Text = 'rien à synchroniser' } }
    Invoke-Api POST '/api/Sync/apply' ('[' + ((@($pending) | ForEach-Object { $_.id }) -join ',') + ']') $Token
}

function Lot-Imprimantes {
    Write-Host "`n--- Lot Imprimantes et spouleur (2.2, 2.3, NR-04) ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    # Données de référence : pilote installé sur SRV-PRINT, marque, modèle, lieu du réseau du labo
    Invoke-Api POST '/api/Pilotes/sync' -Token $ta | Out-Null
    $pilote = (Invoke-Api GET '/api/Pilotes' -Token $ta).Json | Where-Object nom -eq 'Generic / Text Only' | Select-Object -First 1
    Check 'prep' 'pilote « Generic / Text Only » visible après synchronisation des pilotes' ([bool]$pilote) $true
    $marque = (Invoke-Api GET '/api/Marques' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    if (-not $marque) { $marque = (Invoke-Api POST '/api/Marques' @{ nom = 'Labo' } $ta).Json }
    $modele = (Invoke-Api GET '/api/Modeles' -Token $ta).Json | Where-Object nom -eq 'Texte brut' | Select-Object -First 1
    if (-not $modele) { $modele = (Invoke-Api POST '/api/Modeles' @{ nom = 'Texte brut'; marqueId = $marque.id; piloteId = $pilote.id } $ta).Json }
    $lieu = (Invoke-Api GET '/api/Emplacements' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    if (-not $lieu) {
        $r = Invoke-Api POST '/api/Emplacements' @{ nom = 'Labo'; code = 'LAB'; networks = @(@{ cidrIpv4 = '192.168.99.0/24'; isPrimary = $true }) } $ta
        Check 'prep' 'création du lieu Labo (192.168.99.0/24)' $r.Code { param($x) $x -in 200, 201 } $r.Text
        $lieu = $r.Json
    }
    $base = @{ adresseIp = '192.168.99.10'; emplacementId = $lieu.id; modeleId = $modele.id; estPartagee = $true }

    $queuesBefore = Get-ServerQueues
    # P1-07
    $p = @{} + $base; $p.nomAffiche = "Salle d'attente"; $p.nomPartage = 'SalleAttente'
    $r = Invoke-Api POST '/api/Imprimantes' $p $ta
    Check 'P1-07' "création de « Salle d'attente »" $r.Code { param($x) $x -in 200, 201 } $r.Text
    $imp = $r.Json
    $s = Invoke-Sync $ta
    $q = Get-ServerQueues
    Check 'P1-07' "synchronisation : file « Salle d'attente » créée sur SRV-PRINT" ($q -contains "Salle d'attente") $true $s.Text
    # P1-08 renommage puis suppression
    $cur = (Invoke-Api GET "/api/Imprimantes/$($imp.id)" -Token $ta).Json
    $cur.nomAffiche = "Salle d'attente 2"
    $r = Invoke-Api PUT "/api/Imprimantes/$($imp.id)" $cur $ta
    $s = Invoke-Sync $ta; $q = Get-ServerQueues
    Check 'P1-08' 'renommage synchronisé (nouvelle file présente, ancienne absente)' (($q -contains "Salle d'attente 2") -and ($q -notcontains "Salle d'attente")) $true "$($r.Code) $($s.Text)"
    $r = Invoke-Api DELETE "/api/Imprimantes/$($imp.id)" -Token $ta
    $s = Invoke-Sync $ta; $q = Get-ServerQueues
    Check 'P1-08' 'suppression synchronisée, autres files intactes' ((($q -notcontains "Salle d'attente 2")) -and (@($queuesBefore | Where-Object { $q -notcontains $_ }).Count -eq 0)) $true "$($r.Code) $($s.Text)"
    # P1-09 nom piégé
    $p = @{} + $base; $p.nomAffiche = "Test' OR Name LIKE '%"; $p.nomPartage = 'TestPiege'
    $r = Invoke-Api POST '/api/Imprimantes' $p $ta; $piege = $r.Json
    Invoke-Sync $ta | Out-Null; $q1 = Get-ServerQueues
    if ($piege.id) { Invoke-Api DELETE "/api/Imprimantes/$($piege.id)" -Token $ta | Out-Null }
    Invoke-Sync $ta | Out-Null; $q2 = Get-ServerQueues
    Check 'P1-09' "nom « Test' OR Name LIKE '% » : créée puis supprimée seule" (($q1 -contains "Test' OR Name LIKE '%") -and ($q2 -notcontains "Test' OR Name LIKE '%") -and (@($queuesBefore | Where-Object { $q2 -notcontains $_ }).Count -eq 0)) $true ("création {0} ; files : {1}" -f $r.Code, ($q2 -join ' | '))
    # P1-10, P1-11
    foreach ($n in 'Salle\A', 'Salle,A') {
        $p = @{} + $base; $p.nomAffiche = $n
        $r = Invoke-Api POST '/api/Imprimantes' $p $ta
        Check 'P1-10' "nom « $n » refusé" $r.Code 400 $r.Text
    }
    $p = @{} + $base; $p.nomAffiche = 'Partage invalide'; $p.nomPartage = 'Partage:1'
    $r = Invoke-Api POST '/api/Imprimantes' $p $ta
    Check 'P1-11' 'nom de partage « Partage:1 » refusé' $r.Code 400 $r.Text
    # P1-04 / P1-05 : scan du spouleur
    $r = Invoke-Api POST '/api/Import/Scan/Printers' @{} $ta
    Check 'P1-04' 'admin : lister les files du serveur' $r.Code 200 $r.Text
    $names = @($r.Json | ForEach-Object { $_.name; $_.nom; $_.printerName } | Where-Object { $_ })
    Write-Host "         files remontées : $($names -join ' | ')" -ForegroundColor DarkGray
    # File de test permanente pour la suite (client, impression)
    if (-not ((Invoke-Api GET '/api/Imprimantes' -Token $ta).Json | Where-Object nomAffiche -eq 'Labo-Texte')) {
        $p = @{} + $base; $p.nomAffiche = 'Labo-Texte'; $p.nomPartage = 'Labo-Texte'
        Invoke-Api POST '/api/Imprimantes' $p $ta | Out-Null; Invoke-Sync $ta | Out-Null
    }
    Check 'NR-04' 'file permanente « Labo-Texte » présente sur SRV-PRINT' ((Get-ServerQueues) -contains 'Labo-Texte') $true
}

# ------------------------------------------------------------------ lot Web (en-têtes, nom d'accès, mots de passe)
function Get-Raw([string]$Url, [string]$HostHeader = $null) {
    $req = [Net.HttpWebRequest]::Create($Url); $req.Timeout = 30000; $req.AllowAutoRedirect = $false
    if ($HostHeader) { $req.Host = $HostHeader }
    try { $resp = $req.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response; if (-not $resp) { return [pscustomobject]@{ Code = -1; Headers = @{} } } }
    $h = @{}; foreach ($k in $resp.Headers.AllKeys) { $h[$k] = $resp.Headers[$k] }
    $code = [int]$resp.StatusCode; $resp.Close()
    [pscustomobject]@{ Code = $code; Headers = $h }
}

function Lot-Web {
    Write-Host "`n--- Lot Web : nom d'accès, en-têtes, politique de mot de passe (3.1, 3.2) ---" -ForegroundColor Cyan
    $r = Get-Raw "$BaseUrl/"
    Check 'P2-03' 'console par le nom public' $r.Code 200
    $ip = Get-Raw 'http://192.168.99.20:8080/'
    Check 'P2-04' 'console par l''adresse IP' $ip.Code 400
    $alias = Get-Raw "$BaseUrl/" 'autre-alias.lab.autoprint.test'
    # 400 (filtrage du nom par le site) ou 404 (liaison IIS réservée au nom public) : refus dans les deux cas
    Check 'P2-04' 'console par un autre nom DNS (refus)' $alias.Code { param($x) $x -in 400, 404 }
    if ($BaseUrl -like 'https:*') { Check 'P2-06' 'en-tête Strict-Transport-Security (HTTPS)' ([bool]$r.Headers['Strict-Transport-Security']) $true }
    foreach ($h in @(@{ n = 'Content-Security-Policy'; v = $null }, @{ n = 'X-Frame-Options'; v = 'DENY' }, @{ n = 'X-Content-Type-Options'; v = 'nosniff' }, @{ n = 'Referrer-Policy'; v = 'no-referrer' })) {
        $val = $r.Headers[$h.n]
        Check 'P2-06' "en-tête $($h.n)" $(if ($h.v) { $val } else { [bool]$val }) $(if ($h.v) { $h.v } else { $true })
    }
    Check 'P2-06' 'en-tête Server masqué (information)' ([bool]$r.Headers['X-Powered-By']) $false
    Write-Host "         Server: $($r.Headers['Server'])   (HSTS non attendu en HTTP)" -ForegroundColor DarkGray

    $ta = Get-Token 'admin'
    $lecteurRole = ((Invoke-Api GET '/api/roles' -Token $ta).Json | Where-Object name -eq 'Lecteur').id
    $r = Invoke-Api POST '/api/users' @{ username = 'court'; password = 'Abcdefgh12!'; displayName = 'Court'; email = 'court@lab.autoprint.test'; roleId = $lecteurRole } $ta
    Check 'P2-13' 'mot de passe de 11 caractères refusé' $r.Code 400 $r.Text
    $r = Invoke-Api POST '/api/users' @{ username = 'identique.mdp'; password = 'identique.mdp'; displayName = 'Identique'; email = 'identique@lab.autoprint.test'; roleId = $lecteurRole } $ta
    Check 'P2-14' 'mot de passe égal à l''identifiant refusé' $r.Code 400 $r.Text
    $th = Get-Token 'helpdesk'; $cur = Get-Plain 'helpdesk'
    $r = Invoke-Api PUT '/api/users/change-password' @{ currentPassword = $cur; newPassword = $cur; confirmPassword = $cur } $th
    Check 'P2-15' 'changer son mot de passe pour le même refusé' $r.Code 400 $r.Text
}

# ------------------------------------------------------------------ lot AD
$AdDomain = 'lab.autoprint.test'
$AdBase   = 'DC=lab,DC=autoprint,DC=test'
$AdGroups = "OU=Groupes,OU=Autoprint,$AdBase"

function Invoke-OnDc([scriptblock]$Script, [object[]]$Arguments = @()) {
    if (-not $script:LabCred) { Get-ServerQueues | Out-Null }
    Invoke-Command -VMName SRV-AD -Credential $script:LabCred -ScriptBlock $Script -ArgumentList $Arguments
}

# Paramètres actuels -> objet complet attendu par POST /api/Settings/Save (sinon les autres réglages repartent à leur valeur par défaut)
function Get-SettingsDto([string]$Token) {
    $kv = @{}; (Invoke-Api GET '/api/Settings' -Token $Token).Json | ForEach-Object { $kv[$_.key] = $_.value }
    $ints = 'LogRetentionDays', 'SmtpPort', 'PasswordExpirationDays', 'Monitoring_IntervalMinutes', 'Monitoring_StartHour', 'Monitoring_EndHour'
    $bools = 'SmtpEnableSsl', 'NamingEnabled', 'NamingSameShare', 'Printer_DefaultDirectModeEnabled', 'AdUseServiceAccount', 'Monitoring_Enabled',
             'Monitoring_ScanOnWeekends', 'Monitoring_IncludeOffline', 'Monitoring_IncludeWarning', 'Monitoring_IncludeCritical', 'Monitoring_IncludeArchived'
    $strings = 'SmtpHost', 'SmtpUser', 'SmtpPass', 'SmtpFromAddress', 'NamingTemplate', 'AdDomain', 'AdBaseDn', 'AdLdapFilter', 'AdServiceUser', 'AdServicePassword', 'AdAdminEmails'
    $dto = [ordered]@{}
    foreach ($k in $ints) { if ($kv.ContainsKey($k) -and $kv[$k] -match '^-?\d+$') { $dto[$k] = [int]$kv[$k] } }
    foreach ($k in $bools) { if ($kv.ContainsKey($k) -and $kv[$k]) { $dto[$k] = [bool]::Parse($kv[$k]) } }
    foreach ($k in $strings) { $dto[$k] = if ($kv.ContainsKey($k)) { [string]$kv[$k] } else { '' } }
    $dto['SmtpPass'] = '●●●●●●●●'; $dto['AdServicePassword'] = '●●●●●●●●'   # secrets : ne pas renvoyer la valeur chiffrée
    return $dto
}

function Lot-AD {
    Write-Host "`n--- Lot Active Directory (2.7, 2.8) ---" -ForegroundColor Cyan
    # Comptes de test dans le domaine (mots de passe générés, gardés chiffrés)
    foreach ($u in 'svc.autoprint', 'ad.user1', 'ad.user2') { if (-not (Get-Plain $u)) { Set-Plain $u (New-TestPassword) } }
    $out = Invoke-OnDc {
        param($base, $groups, $pSvc, $p1, $p2)
        $ou = "OU=Autoprint,$base"
        $defs = @(@{ n = 'svc.autoprint'; p = $pSvc; ou = "OU=Comptes de service,$ou" }, @{ n = 'ad.user1'; p = $p1; ou = "OU=Utilisateurs,$ou" }, @{ n = 'ad.user2'; p = $p2; ou = "OU=Utilisateurs,$ou" })
        foreach ($d in $defs) {
            if (-not (Get-ADUser -Filter "SamAccountName -eq '$($d.n)'")) {
                New-ADUser -Name $d.n -SamAccountName $d.n -UserPrincipalName "$($d.n)@lab.autoprint.test" -EmailAddress "$($d.n)@lab.autoprint.test" `
                    -Path $d.ou -AccountPassword $d.p -Enabled $true -PasswordNeverExpires $true
            } else { Set-ADAccountPassword $d.n -Reset -NewPassword $d.p; Enable-ADAccount $d.n }
        }
        Add-ADGroupMember 'GG_Autoprint_Lecteurs' -Members 'ad.user1'
        Remove-ADGroupMember 'GG_Autoprint_Lecteurs' -Members 'ad.user2' -Confirm:$false -ErrorAction SilentlyContinue
        Remove-ADGroupMember 'GG_Autoprint_Admins' -Members 'ad.user1', 'ad.user2' -Confirm:$false -ErrorAction SilentlyContinue
        $pol = Get-ADDefaultDomainPasswordPolicy
        "seuil de verrouillage du domaine : $($pol.LockoutThreshold)"
    } @($AdBase, $AdGroups, $script:Secrets['svc.autoprint'], $script:Secrets['ad.user1'], $script:Secrets['ad.user2'])
    Write-Host "    $out" -ForegroundColor DarkGray

    $ta = Get-Token 'admin'
    $dto = Get-SettingsDto $ta
    $dto.AdDomain = $AdDomain; $dto.AdBaseDn = $AdBase; $dto.AdLdapFilter = ''; $dto.AdUseServiceAccount = $true
    $dto.AdServiceUser = 'svc.autoprint'; $dto.AdServicePassword = (Get-Plain 'svc.autoprint')
    $r = Invoke-Api POST '/api/Settings/Save' $dto $ta
    Check 'prep' 'paramètres AD enregistrés' $r.Code 200 $r.Text

    $r = Invoke-Api POST '/api/Settings/TestAd' @{ domain = $AdDomain; useServiceAccount = $true; serviceUser = 'svc.autoprint'; servicePassword = '●●●●●●●●' } $ta
    Check 'P1-40' 'tester la connexion AD sans ressaisir le mot de passe' $r.Code 200 $r.Text
    $r = Invoke-Api POST '/api/Settings/TestAd' @{ domain = 'autre.domaine.local'; useServiceAccount = $true; serviceUser = 'svc.autoprint'; servicePassword = '●●●●●●●●' } $ta
    Check 'P1-41' 'autre domaine sans mot de passe : refus' $r.Code 400 $r.Text
    $r = Invoke-Api POST '/api/Settings/TestAd' @{ domain = 'autre.domaine.local'; useServiceAccount = $true; serviceUser = 'svc.autoprint'; servicePassword = 'Quelconque-2026!' } $ta
    Check 'P1-42' 'autre domaine avec mot de passe : test tenté (échec de connexion)' ($r.Text -match 'Échec connexion AD') $true $r.Text
    $f = @{ domain = $AdDomain; baseDn = $AdBase; filter = ''; useServiceAccount = $true; serviceUser = 'svc.autoprint'; servicePassword = '●●●●●●●●' }
    $r = Invoke-Api POST '/api/Settings/TestAdFilter' ($f + @{ testUserQuery = 'ad.user1' }) $ta
    Check 'P1-43' 'filtre avec ad.user1 : objet trouvé' $r.Json.success $true $r.Text
    $r = Invoke-Api POST '/api/Settings/TestAdFilter' ($f + @{ testUserQuery = '*' }) $ta
    Check 'P1-44' 'filtre avec * : aucun objet (pas de joker)' $r.Json.success $false $r.Text
    $bad = Get-SettingsDto $ta; $bad.AdLdapFilter = '(objectClass=user'
    $r = Invoke-Api POST '/api/Settings/Save' $bad $ta
    Check 'P1-45' 'filtre LDAP mal formé refusé à l''enregistrement' $r.Code 400 $r.Text
    $good = Get-SettingsDto $ta; $good.AdLdapFilter = "(memberOf=CN=GG_Autoprint_Lecteurs,$AdGroups)"
    $r = Invoke-Api POST '/api/Settings/Save' $good $ta
    $s = Invoke-Api GET '/api/roles/ad/search?q=GG_Autoprint' -Token $ta
    Check 'P1-46' 'filtre valide accepté ; recherche AD des groupes' "$($r.Code)/$($s.Code)" '200/200' $s.Text
    Write-Host "         recherche : $($s.Text.Substring(0, [Math]::Min(200, $s.Text.Length)))" -ForegroundColor DarkGray
    $good.AdLdapFilter = ''; Invoke-Api POST '/api/Settings/Save' $good $ta | Out-Null

    # Liaisons groupe AD -> rôle
    $roles = @{}; (Invoke-Api GET '/api/roles' -Token $ta).Json | ForEach-Object { $roles[$_.name] = $_.id }
    foreach ($m in @(@{ g = 'GG_Autoprint_Lecteurs'; r = 'Lecteur' }, @{ g = 'GG_Autoprint_Admins'; r = 'SuperAdmin' })) {
        $existing = (Invoke-Api GET "/api/roles/$($roles[$m.r])/admappings" -Token $ta).Json | Where-Object adIdentifier -eq $m.g
        if (-not $existing) { $r = Invoke-Api POST '/api/roles/admappings' @{ adIdentifier = $m.g; mappingType = 0; roleId = $roles[$m.r] } $ta; Check 'prep' "liaison $($m.g) -> $($m.r)" $r.Code { param($x) $x -in 200, 201 } $r.Text }
    }

    function Perms([string]$token) { $p = $token.Split('.')[1].Replace('-', '+').Replace('_', '/'); $p += '=' * ((4 - $p.Length % 4) % 4); $j = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json; @($j.Permission) }

    $r = Invoke-Login 'ad.user1' (Get-Plain 'ad.user1')
    Check 'P1-35' 'ad.user1 (membre de GG_Autoprint_Lecteurs) : connexion avec le rôle Lecteur' "$($r.Code) $((Perms $r.Json.token) -join ',')" '200 PRINTER_READ' $r.Text
    Invoke-OnDc { Remove-ADGroupMember 'GG_Autoprint_Lecteurs' -Members 'ad.user1' -Confirm:$false }
    $r = Invoke-Login 'ad.user1' (Get-Plain 'ad.user1')
    Check 'P1-36' 'ad.user1 retiré du groupe : connexion refusée' $r.Code { param($x) $x -in 401, 403 } $r.Text

    $r = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2')
    Write-Host "         ad.user2 sans groupe : $($r.Code) $($r.Text)" -ForegroundColor DarkGray
    $u2 = (Invoke-Api GET '/api/users' -Token $ta).Json | Where-Object username -eq 'ad.user2'
    if ($u2) {
        $r = Invoke-Api PUT "/api/users/$($u2.id)" @{ displayName = 'ad.user2'; email = 'ad.user2@lab.autoprint.test'; isActive = $true; roleId = $roles['Lecteur']; forceChangePassword = $false } $ta
        $l = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2')
        Check 'P1-37' 'rôle Lecteur manuel pour ad.user2 : connexion, rôle conservé' "$($l.Code) $((Perms $l.Json.token) -join ',')" '200 PRINTER_READ' "$($r.Code) $($l.Text)"
        Invoke-OnDc { Add-ADGroupMember 'GG_Autoprint_Admins' -Members 'ad.user2' }
        $l = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2'); $p = Perms $l.Json.token
        Check 'P1-38' 'ad.user2 ajouté à GG_Autoprint_Admins : rôle admin' ($p -contains 'ADMIN_ACCESS') $true
        Invoke-OnDc { Remove-ADGroupMember 'GG_Autoprint_Admins' -Members 'ad.user2' -Confirm:$false }
        $l = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2'); $p = Perms $l.Json.token
        Check 'P1-38' 'retiré du groupe : rôle admin retiré, Lecteur manuel conservé' (($p -notcontains 'ADMIN_ACCESS') -and ($p -contains 'PRINTER_READ')) $true ($p -join ',')
    } else { Check 'P1-37' 'ad.user2 créé dans Autoprint à sa première connexion' $false $true }

    # P1-34 : 5 mauvais mots de passe sur un compte AD
    Start-Sleep 62; $script:Logins.Clear()
    $codes = foreach ($n in 1..5) { $script:Logins.Add((Get-Date)); (Invoke-Api POST '/api/auth/login' @{ username = 'ad.user2'; password = 'Mauvais-mot-de-passe-2' }).Code }
    $lock = Invoke-OnDc { $u = Get-ADUser 'ad.user2' -Properties LockedOut, badPwdCount; "$($u.LockedOut)/$($u.badPwdCount)" }
    Check 'P1-34' '5 mauvais mots de passe AD : compte non verrouillé dans l''AD (verrouillé/nb échecs)' ($lock -like 'False/*') $true "$($codes -join ',') ; AD : $lock"
    Write-Host "         codes : $($codes -join ',') ; AD verrouillé/badPwdCount : $lock" -ForegroundColor DarkGray
}

# ------------------------------------------------------------------ lot Limitation
function Lot-Limitation {
    Write-Host "`n--- Lot Limitation des tentatives (2.6) ---" -ForegroundColor Cyan
    Write-Host '    (attente de 65 s pour repartir d''un compteur par IP vide)' -ForegroundColor DarkGray
    Start-Sleep 65; $script:Logins.Clear()
    $codes = foreach ($n in 1..5) { $script:Logins.Add((Get-Date)); (Invoke-Api POST '/api/auth/login' @{ username = 'lecteur'; password = 'Mauvais-mot-de-passe-1' }).Code }
    Check 'P1-29' '5 mauvais mots de passe pour lecteur' ($codes -join ',') '401,401,401,401,401'
    $r = Invoke-Api POST '/api/auth/login' @{ username = 'lecteur'; password = (Get-Plain 'lecteur') }
    Check 'P1-30' '6e tentative avec le bon mot de passe' $r.Code { param($x) $x -in 429, 401, 423 } $r.Text
    Write-Host "         message : $($r.Text)" -ForegroundColor DarkGray
    $r = Invoke-Api POST '/api/auth/login' @{ username = 'admin'; password = (Get-Plain 'admin') }
    Check 'P1-33' 'pendant le blocage de lecteur : connexion admin' $r.Code 200 $r.Text
    $codes = foreach ($n in 1..6) { (Invoke-Api POST '/api/auth/login' @{ username = "inconnu$n"; password = 'x' }).Code }
    Check 'P1-32' 'plus de 10 tentatives en une minute depuis ce poste : la 11e est refusée' $codes[-1] 429 ($codes -join ',')
    Write-Host '    (attente de 125 s pour la fin du blocage de lecteur)' -ForegroundColor DarkGray
    Start-Sleep 125; $script:Logins.Clear()
    $r = Invoke-Login 'lecteur' (Get-Plain 'lecteur')
    Check 'P1-31' 'après 2 minutes : connexion lecteur avec le bon mot de passe' $r.Code 200 $r.Text
}

if ($Lot -contains 'Tout') { $Lot = 'Comptes', 'Escalade', 'Sessions', 'Imprimantes', 'Web', 'AD', 'Limitation' }
foreach ($l in $Lot) { & "Lot-$l" }
Write-Host ("`nBilan : {0} OK, {1} ÉCHEC" -f $script:Score.OK, $script:Score.KO) -ForegroundColor $(if ($script:Score.KO) { 'Red' } else { 'Green' })
