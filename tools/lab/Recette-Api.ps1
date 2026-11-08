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
    [string]$BaseUrl = 'http://srv-print.lab.autoprint.test:8080',
    [ValidateSet('Comptes', 'Escalade', 'Sessions', 'Limitation', 'Tout')][string[]]$Lot = 'Tout'
)

$ErrorActionPreference = 'Stop'
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
        $bytes = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
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

if ($Lot -contains 'Tout') { $Lot = 'Comptes', 'Escalade', 'Sessions', 'Limitation' }
foreach ($l in $Lot) { & "Lot-$l" }
Write-Host ("`nBilan : {0} OK, {1} ÉCHEC" -f $script:Score.OK, $script:Score.KO) -ForegroundColor $(if ($script:Score.KO) { 'Red' } else { 'Green' })
