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
    [ValidateSet('Comptes', 'Escalade', 'Sessions', 'Imprimantes', 'Web', 'AD', 'Cle', 'Donnees', 'Serveur', 'Email', 'Snmp', 'Divers', 'Infra', 'Limitation', 'Tout')][string[]]$Lot = 'Tout',
    # Autre installation d'Autoprint (ex. « sql ») : comptes Autoprint à part (recette-secrets-<profil>.xml), comptes du labo partagés
    [string]$Profil = ''
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
if ($Profil) {
    $main = $SecretsFile
    $SecretsFile = Join-Path $env:USERPROFILE ".autoprint-lab\recette-secrets-$Profil.xml"
    if (-not (Test-Path $SecretsFile)) {
        # Comptes du labo (AD, SMTP, SQL, SNMP) repris ; comptes locaux Autoprint recréés sur cette installation
        $s = Import-Clixml $main; foreach ($k in 'admin', 'admin2', 'helpdesk', 'lecteur', 'sess.a', 'hd.cree') { $s.Remove($k) }
        $s | Export-Clixml $SecretsFile
    }
}
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
    # Compte d'un passage précédent : supprimé par admin pour rejouer la création
    $old = (Invoke-Api GET '/api/users' -Token (Get-Token 'admin')).Json | Where-Object username -eq 'hd.cree'
    if ($old) { Invoke-Api DELETE "/api/users/$($old.id)" -Token (Get-Token 'admin') | Out-Null }
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

    foreach ($old in @((Invoke-Api GET '/api/roles' -Token $ta).Json | Where-Object name -eq 'HD-Suppr')) { Invoke-Api DELETE "/api/roles/$($old.id)" -Token $ta | Out-Null }
    $r = Invoke-Api POST '/api/roles' @{ name = 'HD-Suppr'; description = 'Recette'; permissionIds = @($perms['PRINTER_DELETE']) } $th
    Check 'P1-19' 'helpdesk crée un rôle avec PRINTER_DELETE' $r.Code 200 $r.Text
    $dup = Invoke-Api POST '/api/roles' @{ name = 'hd-suppr '; description = 'Doublon'; permissionIds = @($perms['PRINTER_READ']) } $th
    $fake = Invoke-Api POST '/api/roles' @{ name = 'SuperAdmin'; description = 'Faux'; permissionIds = @($perms['PRINTER_READ']) } $th
    Check 'B69' 'nom de rôle déjà utilisé (casse, espaces) ou « SuperAdmin » : refus' "$($dup.Code)/$($fake.Code)" '400/400' "$($dup.Text) | $($fake.Text)"
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
    elseif (-not $modele.piloteId -and $pilote) {
        # Modèle créé lors d'un passage où le pilote manquait encore sur le serveur
        $modele.piloteId = $pilote.id
        Invoke-Api PUT "/api/Modeles/$($modele.id)" ($modele | Select-Object id, nom, code, marqueId, piloteId, snmpProfileId, snmpCredentialId) $ta | Out-Null
    }
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
    # B75 : filtre de l'administrateur relu (RFC 4515) puis reconstruit
    function F([string]$flt, [string]$q = '') { $h = $f.Clone(); $h.filter = $flt; if ($q) { $h.testUserQuery = $q }; $h }
    $r = Invoke-Api POST '/api/Settings/TestAdFilter' (F '(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))' 'ad.user1') $ta
    Check 'B75-1' 'filtre étendu (comptes actifs) + ad.user1 : trouvé' $r.Json.success $true $r.Text
    $r = Invoke-Api POST '/api/Settings/TestAdFilter' (F '(sAMAccountName=ad.us*)') $ta
    Check 'B75-2' 'filtre avec joker ad.us* : trouvé' $r.Json.success $true $r.Text
    $r = Invoke-Api POST '/api/Settings/TestAdFilter' (F "(memberOf=CN=GG_Autoprint_Lecteurs,$AdGroups)" 'ad.user2') $ta
    Check 'B75-3' 'ad.user2 hors du groupe filtré : aucun objet' $r.Json.success $false $r.Text
    $r = Invoke-Api POST '/api/Settings/TestAdFilter' ($f + @{ testUserQuery = 'ad.user1)(objectClass=*' }) $ta
    Check 'B75-4' 'injection dans le nom testé : aucun objet' $r.Json.success $false $r.Text
    $r = Invoke-Api POST '/api/Settings/TestAdFilter' (F '(cn=a(b))') $ta
    Check 'B75-5' 'filtre mal formé refusé au test' ($r.Text -match 'Filtre LDAP invalide') $true $r.Text
    $bad = Get-SettingsDto $ta; $bad.AdLdapFilter = '(cn=a**b)'
    $r = Invoke-Api POST '/api/Settings/Save' $bad $ta
    Check 'B75-6' 'filtre invalide (**) refusé à l''enregistrement' $r.Code 400 $r.Text
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

    # P1-34 (B53) : domaine avec un seuil de verrouillage de 5, 5 mauvais mots de passe sur un compte AD
    $oldThreshold = Invoke-OnDc { $p = Get-ADDefaultDomainPasswordPolicy; Set-ADDefaultDomainPasswordPolicy -Identity $p.DistinguishedName -LockoutThreshold 5 -LockoutObservationWindow '00:30:00' -LockoutDuration '00:30:00'; $p.LockoutThreshold }
    Start-Sleep 62; $script:Logins.Clear()
    $codes = foreach ($n in 1..5) { $script:Logins.Add((Get-Date)); (Invoke-Api POST '/api/auth/login' @{ username = 'ad.user2'; password = 'Mauvais-mot-de-passe-2' }).Code }
    $lock = Invoke-OnDc { $u = Get-ADUser 'ad.user2' -Properties LockedOut, badPwdCount; "$($u.LockedOut)/$($u.badPwdCount)" }
    Invoke-OnDc { param($t) Unlock-ADAccount 'ad.user2'; $p = Get-ADDefaultDomainPasswordPolicy; Set-ADDefaultDomainPasswordPolicy -Identity $p.DistinguishedName -LockoutThreshold $t } @($oldThreshold)
    Check 'P1-34' '5 mauvais mots de passe AD (seuil du domaine 5) : 3 × 401 puis 429 ; compte AD non verrouillé' "$($codes -join ',') ; $lock" { param($x) $x -like '401,401,401,429,429 ; False/*' }
    Write-Host "         codes : $($codes -join ',') ; AD verrouillé/badPwdCount : $lock (seuil du domaine remis à $oldThreshold)" -ForegroundColor DarkGray
}

# ------------------------------------------------------------------ lot Clé agent (rotation, B18) ; finit avec une clé neuve déployée sur CLT-TEST
function Test-AgentKey([System.Security.SecureString]$Key) {
    $req = [Net.HttpWebRequest]::Create("$BaseUrl/api/imprimantes"); $req.Timeout = 30000
    $req.Headers['X-Agent-Secret'] = [Net.NetworkCredential]::new('', $Key).Password
    try { $resp = $req.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response }
    $code = [int]$resp.StatusCode; $resp.Close(); $code
}

function Lot-Cle {
    Write-Host "`n--- Lot Clé agent : rotation (SC-05 à SC-07), P2-35 ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    $k0 = ConvertTo-SecureString (Invoke-Api GET '/api/Settings/agent-key' -Token $ta).Json.key -AsPlainText -Force
    $audit = (Invoke-Api GET '/api/Audit?pageSize=20' -Token $ta).Json.items
    Check 'P2-35' 'consultation de la clé tracée (AGENT_KEY_REVEAL, avec l''utilisateur)' ([bool]($audit | Where-Object { $_.action -eq 'AGENT_KEY_REVEAL' -and $_.utilisateur -eq 'admin' })) $true

    $r = Invoke-Api POST '/api/Settings/agent-key/rotate' @{ transitionDays = 30 } $ta
    $k1 = ConvertTo-SecureString $r.Json.key -AsPlainText -Force; $until = $r.Json.previousValidUntil; $r = $null
    Check 'SC-05' 'nouvelle clé avec 30 jours de transition (date de fin renvoyée)' ([bool]$until) $true
    $audit = (Invoke-Api GET '/api/Audit?pageSize=20' -Token $ta).Json.items
    Check 'SC-05' 'journal d''audit AGENT_KEY_ROTATE' ([bool]($audit | Where-Object action -eq 'AGENT_KEY_ROTATE')) $true
    Check 'SC-06' 'pendant la transition : ancienne clé / nouvelle clé' "$(Test-AgentKey $k0)/$(Test-AgentKey $k1)" '200/200'

    $r = Invoke-Api POST '/api/Settings/agent-key/rotate' @{ transitionDays = 0 } $ta
    $k2 = ConvertTo-SecureString $r.Json.key -AsPlainText -Force; $r = $null
    Check 'SC-07' 'révocation immédiate : clé initiale / clé précédente / nouvelle clé' "$(Test-AgentKey $k0)/$(Test-AgentKey $k1)/$(Test-AgentKey $k2)" '401/401/200'
    $r = Invoke-Api POST '/api/Settings/agent-key/rotate' @{ transitionDays = 91 } $ta
    Check 'SC-05' 'période de transition hors limites (91 jours) refusée' $r.Code 400 $r.Text

    # Déploiement de la nouvelle clé sur CLT-TEST (comme une mise à jour de configuration Intune)
    if (-not $script:LabCred) { Get-ServerQueues | Out-Null }
    $clt = [pscredential]::new('.\LabAdmin', $script:LabCred.Password)
    $ok = Invoke-Command -VMName CLT-TEST -Credential $clt -ArgumentList $k2 -ScriptBlock {
        param($k) Set-ItemProperty 'HKLM:\SOFTWARE\Autoprint' -Name APIKEY -Value ([Net.NetworkCredential]::new('', $k).Password)
        $h = @{ 'X-Agent-Secret' = (Get-ItemProperty 'HKLM:\SOFTWARE\Autoprint').APIKEY }
        (Invoke-WebRequest https://srv-print.lab.autoprint.test/api/imprimantes -Headers $h -UseBasicParsing).StatusCode
    }
    Check 'SC-07' 'CLT-TEST avec la nouvelle clé (registre mis à jour)' $ok 200
}

# ------------------------------------------------------------------ lot Données : sauvegardes, audit, jeton d'intégration
function Lot-Donnees {
    Write-Host "`n--- Lot Données : sauvegarde / restauration (4.2, NR-08, P2-18, P2-40), audit (P2-39), supervision (NR-09) ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    $secretKeys = 'AgentApiKey', 'AdServicePassword', 'SmtpPass'
    $agentKey = ConvertTo-SecureString (Invoke-Api GET '/api/Settings/agent-key' -Token $ta).Json.key -AsPlainText -Force
    $count = { param($t) "{0} imprimantes, {1} comptes, {2} rôles" -f @((Invoke-Api GET '/api/Imprimantes' -Token $t).Json).Count, @((Invoke-Api GET '/api/users' -Token $t).Json).Count, @((Invoke-Api GET '/api/roles' -Token $t).Json).Count }
    $before = & $count $ta

    $r = Invoke-Api POST '/api/Backup/export' @{ secretsPassword = 'Court-11car' } $ta
    Check 'P3-09' 'export avec un mot de passe de 11 caractères refusé' $r.Code 400 $r.Text

    $plainExport = Invoke-Api POST '/api/Backup/export' @{} $ta
    $s = @($plainExport.Json.settings | Where-Object { $secretKeys -contains $_.key })
    Check 'P3-08' 'export sans mot de passe : SecretsProtected=false, secrets vides' "$($plainExport.Json.secretsProtected)/$(@($s | Where-Object { $_.value }).Count)" 'False/0'

    if (-not (Get-Plain 'backup')) { Set-Plain 'backup' (New-TestPassword) }
    $prot = Invoke-Api POST '/api/Backup/export' @{ secretsPassword = (Get-Plain 'backup') } $ta
    $s = @($prot.Json.settings | Where-Object { $secretKeys -contains $_.key -and $_.value })
    $keyPlain = [Net.NetworkCredential]::new('', $agentKey).Password
    Check 'P3-10' 'export protégé : SecretsProtected=true, secrets en bkp:v1:, clé agent absente en clair' "$($prot.Json.secretsProtected)/$(@($s | Where-Object { $_.value -notlike 'bkp:v1:*' }).Count)/$($prot.Text.Contains($keyPlain))" 'True/0/False'
    $keyPlain = $null

    $body = $prot.Text  # JSON du fichier ; le mot de passe de protection est un champ du même objet
    $r = Invoke-Api POST '/api/Backup/restore' $body $ta
    Check 'P3-11' 'restauration du fichier protégé sans mot de passe : refus' $r.Code 400 $r.Text
    $withBad = ($prot.Json | Select-Object *); $withBad | Add-Member -NotePropertyName secretsPassword -NotePropertyValue 'Mauvais-mot-de-passe-1' -Force
    $r = Invoke-Api POST '/api/Backup/restore' $withBad $ta
    Check 'P3-11' 'avec un mauvais mot de passe : refus' $r.Code 400 $r.Text
    Check 'P3-11' 'aucune donnée modifiée' (& $count $ta) $before

    $withGood = ($prot.Json | Select-Object *); $withGood | Add-Member -NotePropertyName secretsPassword -NotePropertyValue (Get-Plain 'backup') -Force
    $r = Invoke-Api POST '/api/Backup/restore' $withGood $ta
    Check 'P3-12' 'restauration avec le bon mot de passe' $r.Code 200 $r.Text
    $ta = Get-Token 'admin'
    Check 'NR-08' 'données identiques après restauration' (& $count $ta) $before
    $codes = foreach ($u in 'helpdesk', 'lecteur') { (Invoke-Login $u (Get-Plain $u)).Code }
    Check 'P2-40' 'comptes locaux : connexion avec leur mot de passe habituel' ($codes -join ',') '200,200'
    $l = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2')
    if ($l.Code -eq 429) {
        # ad.user2 encore bloqué (30 min) par P1-34 : le compteur est en mémoire (B14), un recyclage du pool le remet à zéro
        Write-Host '    (ad.user2 bloqué par P1-34 : recyclage du pool Autoprint)' -ForegroundColor DarkGray
        Invoke-OnPrint { Import-Module WebAdministration; Restart-WebAppPool Autoprint }; Start-Sleep 5
        $l = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2')
    }
    Check 'P2-40' 'compte AD avec rôle manuel conservé (ad.user2 Lecteur)' $l.Code 200 $l.Text
    Check 'P3-12' 'clé agent conservée après restauration' (Test-AgentKey $agentKey) 200
    $t = Invoke-Api POST '/api/Settings/TestAd' @{ domain = 'lab.autoprint.test'; useServiceAccount = $true; serviceUser = 'svc.autoprint'; servicePassword = '●●●●●●●●' } $ta
    Check 'P3-12' 'mot de passe AD restauré (test de connexion AD)' $t.Code 200 $t.Text

    $r = Invoke-Api POST '/api/Backup/restore' $plainExport.Text $ta
    Check 'P3-13' 'restauration du fichier sans secrets' $r.Code 200 $r.Text
    $ta = Get-Token 'admin'
    Check 'P3-13' 'secrets actuels conservés (clé agent inchangée)' (Test-AgentKey $agentKey) 200

    $broken = [regex]::Replace($plainExport.Text, '(?i)"imprimantes"\s*:\s*\[', '"Imprimantes": [{"Id":"pas-un-nombre"},', 'None', [TimeSpan]::FromSeconds(5))
    if ($broken -eq $plainExport.Text) { throw 'P2-18 : liste des imprimantes introuvable dans l''export' }
    $r = Invoke-Api POST '/api/Backup/restore' $broken $ta
    Check 'P2-18' 'sauvegarde corrompue : refus sans détail technique' "$($r.Code)/$(($r.Text -match 'Exception|   at |System\.'))" { param($x) $x -match '^(400|500)/False$' } $r.Text

    $r = Invoke-Api DELETE '/api/Audit/cleanup' -Token $ta
    Check 'P2-39' 'purge des anciens journaux d''audit (admin)' $r.Code { param($x) $x -in 200, 204 } $r.Text

    $tok = Invoke-Api POST '/api/IntegrationTokens' @{ description = 'Recette NR-09' } $ta
    $raw = ConvertTo-SecureString $tok.Json.rawToken -AsPlainText -Force; $tokId = $tok.Json.id; $tok = $null
    $req = [Net.HttpWebRequest]::Create("$BaseUrl/api/v1/Monitoring/export"); $req.Headers['X-Api-Token'] = [Net.NetworkCredential]::new('', $raw).Password
    try { $resp = $req.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response }
    $code = [int]$resp.StatusCode; $txt = (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd(); $resp.Close()
    Check 'NR-09' 'export supervision avec un jeton d''intégration (JSON)' "$code/$($txt.TrimStart().Substring(0,1))" { param($x) $x -match '^200/[\[{]$' } $txt
    $r = Get-Raw "$BaseUrl/api/v1/Monitoring/export"
    Check 'NR-09' 'sans jeton : refus' $r.Code 401
    Invoke-Api DELETE "/api/IntegrationTokens/$tokId" -Token $ta | Out-Null
}

# Copie de la base SQLite du serveur (avec ses fichiers -wal / -shm, où sont les écritures récentes) ; renvoie le chemin local de la base
function Copy-LabDatabase {
    if (-not $script:LabCred) { Get-ServerQueues | Out-Null }
    $dir = Join-Path $env:TEMP 'autoprint-db'; Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force $dir | Out-Null
    $sess = New-PSSession -VMName SRV-PRINT -Credential $script:LabCred
    Invoke-Command -Session $sess { New-Item -ItemType Directory -Force C:\Windows\Temp\rdb | Out-Null; Copy-Item C:\ProgramData\Autoprint\Data\Autoprint.db* C:\Windows\Temp\rdb\ -Force }
    Copy-Item C:\Windows\Temp\rdb\* -FromSession $sess -Destination $dir -Force
    Invoke-Command -Session $sess { Remove-Item C:\Windows\Temp\rdb -Recurse -Force }; Remove-PSSession $sess
    Join-Path $dir 'Autoprint.db'
}
# ------------------------------------------------------------------ lot Serveur : service AutoprintSpooler, tube, compte du site, base
function Invoke-OnPrint([scriptblock]$Script, [object[]]$Arguments = @()) {
    if (-not $script:LabCred) { Get-ServerQueues | Out-Null }
    Invoke-Command -VMName SRV-PRINT -Credential $script:LabCred -ScriptBlock $Script -ArgumentList $Arguments
}

function Lot-Serveur {
    Write-Host "`n--- Lot Serveur : service, tube nommé, compte du site, base (5.1, 5.3, 5.6, 4.1) ---" -ForegroundColor Cyan
    $info = Invoke-OnPrint {
        Import-Module WebAdministration
        $svc = Get-CimInstance Win32_Service -Filter "Name='AutoprintSpooler'"
        $w3 = Get-CimInstance Win32_Process -Filter "Name='w3wp.exe'" | ForEach-Object { (Invoke-CimMethod -InputObject $_ -MethodName GetOwner).User }
        $cfg = Get-Content 'C:\Program Files\Autoprint Server\appsettings.json' -Raw | ConvertFrom-Json
        [pscustomobject]@{
            Service = "$($svc.State)/$($svc.StartMode)/$($svc.StartName)"
            Pool = (Get-ItemProperty IIS:\AppPools\Autoprint -Name processModel.identityType)
            W3wp = ($w3 -join ',')
            Mode = $cfg.Spooler.Mode; Jwt = $cfg.Jwt.ExpireMinutes; Db = $cfg.Database.ConnectionStrings.Sqlite
            DbInProgramFiles = @(Get-ChildItem 'C:\Program Files\Autoprint Server' -Filter *.db -ErrorAction SilentlyContinue).Count
            KeyFiles = @(Get-ChildItem C:\ProgramData\Autoprint\Keys -Filter 'key-*.xml').Count
            NetworkService = [bool]((Get-Acl 'C:\Program Files\Autoprint Server').Access | Where-Object { $_.IdentityReference -like '*NETWORK SERVICE*' -or $_.IdentityReference -like '*SERVICE RÉSEAU*' })
            Events = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'Autoprint.SpoolerService' } -MaxEvents 200 -ErrorAction SilentlyContinue | Where-Object { $_.Message -match 'Commande \w+' }).Count
        }
    }
    Check 'S8-01' 'service AutoprintSpooler démarré, automatique, Système local' $info.Service 'Running/Auto/LocalSystem'
    Check 'S8-02' 'pool IIS en ApplicationPoolIdentity' "$($info.Pool)" 'ApplicationPoolIdentity'
    Check 'S8-19' 'processus w3wp sous le compte du pool (Autoprint)' $info.W3wp { param($x) $x -match 'Autoprint' -and $x -notmatch 'SYSTEM|Système' } $info.W3wp
    Check 'S8-03' 'Spooler:Mode = Service' $info.Mode 'Service'
    Check 'SC-01' 'Jwt:ExpireMinutes = 30' $info.Jwt 30
    Check 'SB-01' 'base dans ProgramData, aucune .db dans Program Files' "$($info.Db -like '*ProgramData\Autoprint\Data\Autoprint.db*')/$($info.DbInProgramFiles)" 'True/0' $info.Db
    Check 'P3-07' 'fichier de clé key-*.xml présent' ($info.KeyFiles -ge 1) $true
    Check 'P3-19' 'NETWORK SERVICE absent du dossier du programme' $info.NetworkService $false
    Check 'S8-12' 'journal Application : entrées du service pour les commandes de synchronisation' ($info.Events -gt 0) $true "$($info.Events) entrée(s)"

    # S8-17 : le tube n'autorise que SYSTEM et le compte du pool : même un administrateur local (session PowerShell Direct) est refusé
    $pipe = Invoke-OnPrint { try { $p = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'AutoprintSpooler'); $p.Connect(3000); 'CONNECTE' } catch { $_.Exception.Message } }
    Check 'S8-17' 'tube AutoprintSpooler depuis un autre compte local (administrateur, ni SYSTEM ni le pool) : refusé' "$pipe" { param($x) $x -match 'UnauthorizedAccess|refus' } "$pipe"
    $clt = [pscredential]::new('.\LabAdmin', $script:LabCred.Password)
    $adCred = [pscredential]::new('LAB\ad.user1', $script:Secrets['ad.user1'])
    $net = Invoke-Command -VMName CLT-TEST -Credential $clt -ArgumentList $adCred -ScriptBlock {
        param($c)
        New-SmbMapping -RemotePath '\\srv-print.lab.autoprint.test\IPC$' -UserName $c.UserName -Password $c.GetNetworkCredential().Password -Persistent $false | Out-Null
        try { $p = New-Object System.IO.Pipes.NamedPipeClientStream('srv-print.lab.autoprint.test', 'AutoprintSpooler'); $p.Connect(3000); 'CONNECTE' } catch { $_.Exception.GetType().Name + ': ' + $_.Exception.Message }
        Remove-SmbMapping -RemotePath '\\srv-print.lab.autoprint.test\IPC$' -Force
    }
    Check 'S8-18' 'tube depuis un autre poste (compte de domaine authentifié) : refusé' "$net" { param($x) $x -match 'UnauthorizedAccess|refus' } "$net"

    # S8-13 : modèle dont le pilote n'est pas installé sur le serveur
    $ta = Get-Token 'admin'
    $marque = (Invoke-Api GET '/api/Marques' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    $lieu = (Invoke-Api GET '/api/Emplacements' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    $pil = Invoke-Api POST '/api/Pilotes' @{ nom = 'Pilote Fantome 1.0'; version = '1.0'; estInstalle = $false } $ta
    $piloteId = if ($pil.Code -in 200, 201) { $pil.Json.id } else { $null }
    $mod = Invoke-Api POST '/api/Modeles' @{ nom = 'Modèle sans pilote'; marqueId = $marque.id; piloteId = $piloteId } $ta
    $imp = Invoke-Api POST '/api/Imprimantes' @{ nomAffiche = 'Test-PiloteAbsent'; adresseIp = '192.168.99.10'; emplacementId = $lieu.id; modeleId = $mod.Json.id; estPartagee = $false } $ta
    $s = Invoke-Sync $ta
    Check 'S8-13' 'pilote absent du serveur : échec de synchronisation explicite' ($s.Text -match 'Pilote|pilote') $true $s.Text
    if ($imp.Json.id) { Invoke-Api DELETE "/api/Imprimantes/$($imp.Json.id)" -Token $ta | Out-Null; Invoke-Sync $ta | Out-Null }
    if ($mod.Json.id) { Invoke-Api DELETE "/api/Modeles/$($mod.Json.id)" -Token $ta | Out-Null }

    # S8-14 / S8-15 / SB-05 : service arrêté
    Invoke-OnPrint { Stop-Service AutoprintSpooler -Force }
    $dash = Invoke-Api GET '/api/Dashboard' -Token $ta
    $st = ([regex]::Matches($dash.Text, '"[^"]*(?i:spool|service)[^"]*"\s*:\s*("[^"]*"|true|false|\d+)') | ForEach-Object Value) -join ' '
    $lab = (Invoke-Api GET '/api/Imprimantes' -Token $ta).Json | Where-Object nomAffiche -eq 'Labo-Texte'
    $lab.commentaire = "Recette $(Get-Date -Format HHmmss)"
    Invoke-Api PUT "/api/Imprimantes/$($lab.id)" $lab $ta | Out-Null
    $reads = foreach ($p in '/api/Dashboard', '/api/Imprimantes', '/api/Sync/preview') { (Invoke-Api GET $p -Token $ta).Code }
    $scan = (Invoke-Api POST '/api/Import/Scan/Printers' @{} $ta).Code
    Check 'S8-15' 'service arrêté : tableau de bord, imprimantes, prévisualisation, scan du spouleur' "$($reads -join ',')/$scan" '200,200,200/200'
    $s = Invoke-Sync $ta
    Check 'S8-14' 'service arrêté : synchronisation refusée avec message clair' ($s.Text -match 'AutoprintSpooler') $true $s.Text
    Write-Host "         tableau de bord (service arrêté) : $st" -ForegroundColor DarkGray
    Invoke-OnPrint { Start-Service AutoprintSpooler }
    Start-Sleep 5
    $dash2 = Invoke-Api GET '/api/Dashboard' -Token $ta
    $st2 = ([regex]::Matches($dash2.Text, '"[^"]*(?i:spool|service)[^"]*"\s*:\s*("[^"]*"|true|false|\d+)') | ForEach-Object Value) -join ' '
    Write-Host "         tableau de bord (service redémarré) : $st2" -ForegroundColor DarkGray
    Check 'SB-05' 'état du service AutoprintSpooler remonté au tableau de bord (change entre arrêt et marche)' ($st -ne $st2 -and $st) $true
    $s = Invoke-Sync $ta
    Check 'S8-14' 'service redémarré : la synchronisation passe' ($s.Text -match '"success":true') $true $s.Text

    # P3-01 : secrets chiffrés en base (copie de la base vers le poste, lecture des préfixes seulement)
    $res = (Invoke-LabSql "SELECT [Key], [Value] FROM ServerSettings WHERE [Key] IN ('AgentApiKey','AdServicePassword','SmtpPass')" -Full 'Key' |
            ForEach-Object { $_ -replace '^Key=([^;]*);Value=', '$1=' }) -join ';'
    Check 'P3-01' 'AgentApiKey et AdServicePassword chiffrés en base (enc:v1:)' ($res -match 'AgentApiKey=enc:v1:' -and $res -match 'AdServicePassword=enc:v1:') $true $res
    Write-Host "         préfixes : $res" -ForegroundColor DarkGray
}

# ------------------------------------------------------------------ lot E-mail : SMTP de capture sur SRV-AD (tools/lab/SmtpCapture.ps1)
function Get-CapturedMails([datetime]$Since) {
    Invoke-OnDc { param($s) Get-ChildItem C:\SmtpCapture\Mails -Filter *.eml | Where-Object LastWriteTime -gt $s | Sort-Object LastWriteTime | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Text = [IO.File]::ReadAllText($_.FullName) } } } @($Since)
}
function Get-SmtpLog([int]$Tail = 5) { Invoke-OnDc { param($n) Get-Content C:\SmtpCapture\capture.log -Tail $n } @($Tail) }
function ConvertFrom-QuotedPrintable([string]$t) { [regex]::Replace(($t -replace "=\r?\n", ''), '=([0-9A-F]{2})', { param($m) [char][Convert]::ToInt32($m.Groups[1].Value, 16) }) }
# Texte lisible d'un .eml : parties base64 décodées + quoted-printable
function Get-MailText([string]$eml) {
    $parts = [regex]::Matches($eml, '(?s)Content-Transfer-Encoding:\s*base64\s*\r?\n\r?\n([A-Za-z0-9+/=\r\n]+)') | ForEach-Object { try { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($_.Groups[1].Value -replace '\s', ''))) } catch { '' } }
    (ConvertFrom-QuotedPrintable $eml) + "`n" + ($parts -join "`n")
}

function Lot-Email {
    Write-Host "`n--- Lot E-mail : SMTP (4.1, 4.4), rapports (NR-06, NR-07), mot de passe oublié (PA-17 à PA-21) ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    if (-not (Get-Plain 'smtp')) { Set-Plain 'smtp' (New-TestPassword) }
    $dto = Get-SettingsDto $ta
    $dto.SmtpHost = 'srv-ad.lab.autoprint.test'; $dto.SmtpPort = 25; $dto.SmtpUser = 'autoprint-smtp'; $dto.SmtpPass = (Get-Plain 'smtp')
    $dto.SmtpEnableSsl = $true; $dto.SmtpFromAddress = 'autoprint@lab.autoprint.test'
    $r = Invoke-Api POST '/api/Settings/Save' $dto $ta
    Check 'prep' 'SMTP enregistré (SRV-AD, port 25, TLS, authentification)' $r.Code 200 $r.Text

    $t0 = Get-Date
    $r = Invoke-Api POST '/api/Settings/TestEmail' @{ host = 'srv-ad.lab.autoprint.test'; port = 25; user = 'autoprint-smtp'; password = '●●●●●●●●'; ssl = $true; from = 'autoprint@lab.autoprint.test'; to = 'admin@lab.autoprint.test' } $ta
    $log = (Get-SmtpLog 3) -join ' '
    Check 'P3-05' 'test SMTP avec mot de passe masqué : mot de passe enregistré utilisé (TLS + AUTH)' "$($r.Code)/$($log -match 'TLS=True ; AUTH=autoprint-smtp')" '200/True' $r.Text
    $r = Invoke-Api POST '/api/Settings/TestEmail' @{ host = 'autre-smtp.lab.autoprint.test'; port = 25; user = 'autoprint-smtp'; password = '●●●●●●●●'; ssl = $true; from = 'autoprint@lab.autoprint.test'; to = 'admin@lab.autoprint.test' } $ta
    Check 'P3-06' 'autre serveur SMTP sans ressaisir le mot de passe : refus' $r.Code 400 $r.Text

    # Rapports PDF et CSV, « Envoyer maintenant »
    foreach ($fmt in 'PDF', 'CSV') {
        $since = Get-Date
        $s = Invoke-Api POST '/api/ReportSchedules' @{ reportName = "Recette $fmt"; selectedMetricsJson = '["Pages","Toner","Availability","Alerts"]'; scopeFilterJson = '{}'; frequency = 'Hebdomadaire'; emailRecipients = 'admin@lab.autoprint.test'; format = $fmt; isActive = $true; predictionThresholdDays = 14; runHour = 7; runMinute = 30 } $ta
        $id = $s.Json.id
        $send = Invoke-Api POST "/api/ReportSchedules/$id/send" -Token $ta
        Start-Sleep 5
        $m = @(Get-CapturedMails $since) | Select-Object -Last 1
        $att = if ($m) { [regex]::Match($m.Text, '(?im)^Content-Type:\s*application/(pdf|octet-stream|vnd\.ms-excel|csv)[^\r\n]*|name="?[^"\r\n]+\.(pdf|csv)').Value } else { '' }
        $id2 = if ($fmt -eq 'PDF') { 'NR-06' } else { 'NR-07' }
        Check $id2 "rapport $fmt « Envoyer maintenant » : e-mail reçu avec pièce jointe" "$($send.Code)/$([bool]$m)/$([bool]$att)" '200/True/True' "$($s.Code) $($send.Text) $att"
        if ($m -and $fmt -eq 'PDF') {
            $b64 = [regex]::Match($m.Text, '(?s)Content-Type:\s*application/(?:pdf|octet-stream)[^\r\n]*.*?\r?\n\r?\n([A-Za-z0-9+/=\r\n]+)').Groups[1].Value -replace '\s', ''
            if ($b64) { $pdf = [Convert]::FromBase64String($b64); $head = [Text.Encoding]::ASCII.GetString($pdf, 0, 5)
                $out = Join-Path $env:TEMP 'recette-rapport.pdf'; [IO.File]::WriteAllBytes($out, $pdf)
                Check 'NR-06' 'pièce jointe : vrai PDF' "$head ($([math]::Round($pdf.Length/1KB)) Ko)" { param($x) $x -like '%PDF-*' } $out }
        }
        if ($m -and $fmt -eq 'CSV') {
            $b64 = [regex]::Match($m.Text, '(?s)name="?[^"\r\n]+\.csv"?.*?\r?\n\r?\n([A-Za-z0-9+/=\r\n]+)').Groups[1].Value -replace '\s', ''
            if ($b64) { $csv = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64)); Write-Host "         CSV, 1re ligne : $(($csv -split "`n")[0].Trim())" -ForegroundColor DarkGray }
        }
        $g = (Invoke-Api GET "/api/ReportSchedules/$id" -Token $ta).Json
        Check 'NR-10' "planification $fmt conservée (heure, minute, fréquence, seuil)" "$($g.runHour):$($g.runMinute) $($g.frequency) $($g.predictionThresholdDays)" '7:30 Hebdomadaire 14'
        Invoke-Api DELETE "/api/ReportSchedules/$id" -Token $ta | Out-Null
    }

    # Mot de passe oublié (B5) : compte local avec e-mail = lecteur
    Start-Sleep 62; $script:Logins.Clear()
    $since = Get-Date
    $r1 = Invoke-Api POST '/api/auth/forgot-password' @{ emailOrUsername = 'lecteur' }
    Start-Sleep 10
    $mail = @(Get-CapturedMails $since) | Select-Object -Last 1
    $body = if ($mail) { Get-MailText $mail.Text } else { '' }
    $link = [regex]::Match($body, 'https://[^\s"<>]+/reset-password\?token=([A-Za-z0-9_\-%]+)')
    Check 'PA-17' 'compte local avec e-mail : e-mail reçu avec un lien https://<nom public>/reset-password?token=…' "$($r1.Code)/$($link.Success -and $link.Value -like 'https://srv-print.lab.autoprint.test/*')" '200/True' $r1.Text
    $since2 = Get-Date
    $r2 = Invoke-Api POST '/api/auth/forgot-password' @{ emailOrUsername = 'inconnu.total' }
    $r3 = Invoke-Api POST '/api/auth/forgot-password' @{ emailOrUsername = 'ad.user2' }
    Start-Sleep 4
    Check 'PA-18' 'identifiant inexistant et compte AD : même message, aucun e-mail' "$($r2.Text -eq $r1.Text)/$($r3.Text -eq $r1.Text)/$(@(Get-CapturedMails $since2).Count)" 'True/True/0' "$($r2.Text) | $($r3.Text)"
    if ($link.Success) {
        $token = [uri]::UnescapeDataString($link.Groups[1].Value)
        # PA-21 : la base ne contient qu'une empreinte du jeton
        # Empreinte stockée (pas le jeton) : lue en entier pour la comparer au jeton du lien
        $v = ((Invoke-LabSql "SELECT PasswordResetToken FROM Users WHERE Username = 'lecteur'" -Full 'PasswordResetToken') -replace '^PasswordResetToken=', '')
        $res = "$(if ($v -match '^[0-9a-fA-F]+$') { 'hex' } else { 'autre' });$($v.Length);$(if ($v -eq $token) { 'identique' } else { 'different' })"
        Check 'PA-21' 'base : empreinte hexadécimale, différente du jeton du lien' $res { param($x) $x -like 'hex;*;different' } $res
        $r = Invoke-Api POST '/api/auth/reset-password' @{ token = $token; newPassword = 'Court-11car'; confirmPassword = 'Court-11car' }
        Check 'PA-19' 'nouveau mot de passe de 11 caractères refusé' $r.Code 400 $r.Text
        $new = New-TestPassword
        $r = Invoke-Api POST '/api/auth/reset-password' @{ token = $token; newPassword = $new; confirmPassword = $new }
        if ($r.Code -eq 200) { Set-Plain 'lecteur' $new }
        $l = Invoke-Login 'lecteur' $new
        Check 'PA-19' 'mot de passe valide : modifié, connexion avec le nouveau' "$($r.Code)/$($l.Code)" '200/200' $r.Text
        $audit = (Invoke-Api GET '/api/Audit?pageSize=30' -Token $ta).Json.items.action
        Check 'PA-19' 'audit : USER_PWD_RESET_REQUEST et USER_PWD_RESET' (($audit -contains 'USER_PWD_RESET_REQUEST') -and ($audit -contains 'USER_PWD_RESET')) $true
        $n2 = New-TestPassword
        $r = Invoke-Api POST '/api/auth/reset-password' @{ token = $token; newPassword = $n2; confirmPassword = $n2 }
        Check 'PA-20' 'même lien réutilisé : invalide' $r.Code 400 $r.Text
        $token = $null
    }
}

# ------------------------------------------------------------------ lot Snmp : SNMPv3 et identifiants SNMP (5.5)
# Requête sur la base du serveur (SQL Server ou SQLite) ; chaque valeur est tronquée à 7 caractères sur le serveur
# (préfixe enc:v1: seulement, jamais de secret complet sur ce poste)
function Invoke-LabSql([string]$Query, [string[]]$Full = @()) {   # $Full : colonnes non tronquées (noms, clés)
    $prov = Invoke-OnPrint { (Get-Content 'C:\Program Files\Autoprint Server\appsettings.json' -Raw | ConvertFrom-Json).Database.Provider }
    if ($prov -eq 'SqlServer') {
        return Invoke-OnPrint {
            param($q, $full)
            $cs = (Get-Content 'C:\Program Files\Autoprint Server\appsettings.json' -Raw | ConvertFrom-Json).Database.ConnectionStrings.SqlServer
            # Format Microsoft.Data.SqlClient -> System.Data.SqlClient (Windows PowerShell)
            $cs = $cs -replace 'Trust Server Certificate', 'TrustServerCertificate' -replace 'Multiple Active Result Sets', 'MultipleActiveResultSets' -replace 'Application Intent', 'ApplicationIntent'
            $c = New-Object Data.SqlClient.SqlConnection $cs; $c.Open(); $cmd = $c.CreateCommand(); $cmd.CommandText = $q; $r = $cmd.ExecuteReader()
            while ($r.Read()) { (0..($r.FieldCount - 1) | ForEach-Object { $v = "$($r[$_])"; $n = $r.GetName($_); if ($full -notcontains $n) { $v = $v.Substring(0, [Math]::Min(7, $v.Length)) }; "$n=$v" }) -join ';' }
            $c.Close()
        } @($Query, $Full)
    }
    $db = Copy-LabDatabase
    $pyFile = Join-Path $env:TEMP 'autoprint-query.py'
    Set-Content $pyFile "import sqlite3,sys`nc=sqlite3.connect(sys.argv[1])`ncur=c.execute(sys.argv[2])`nn=[d[0] for d in cur.description]`nfull=sys.argv[3].split(',')
for row in cur: print(';'.join(k+'='+(str(v if v is not None else '') if k in full else str(v if v is not None else '')[:7]) for k,v in zip(n,row)))" -Encoding ASCII
    $res = & python -I $pyFile $db $Query ('-,' + ($Full -join ','));   # jamais vide (argument vide perdu par PowerShell 5.1) Remove-Item (Split-Path $db) -Recurse -Force; Remove-Item $pyFile -Force
    return $res
}
# Journal du simulateur : repère = nombre de lignes (pas l'heure : horloges de l'hôte et de SRV-AD différentes)
function Get-SnmpSimMark { Invoke-OnDc { @(Get-Content C:\SnmpSim\snmpsim.log).Count } }
function Get-SnmpSimLog([int]$Mark) {
    Invoke-OnDc { param($m) Get-Content C:\SnmpSim\snmpsim.log | Select-Object -Skip $m | Where-Object { $_ -notmatch 'key' } } @($Mark)
}

function Lot-Snmp {
    Write-Host "`n--- Lot SNMPv3 et identifiants SNMP (5.5, simulateur SRV-AD : .10 v3, .11 v2c « parc ») ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    $auth = Get-Plain 'snmp.v3.auth'; $priv = Get-Plain 'snmp.v3.priv'
    if (-not $auth) { throw 'Simulateur SNMP non installé (tools\lab\Install-LabSnmp.ps1)' }
    $v3 = @{ version = 3; v3Username = 'autoprint.v3'; v3SecurityLevel = 2; v3AuthProtocol = 1; v3PrivProtocol = 0 }
    function New-Cred([hashtable]$extra) { $h = $v3.Clone(); foreach ($k in $extra.Keys) { $h[$k] = $extra[$k] }; $h }

    # Nettoyage d'un passage précédent
    foreach ($p in @((Invoke-Api GET '/api/Imprimantes' -Token $ta).Json | Where-Object { $_.nomAffiche -like 'SIM-*' })) { Invoke-Api DELETE "/api/Imprimantes/$($p.id)" -Token $ta | Out-Null }
    foreach ($m in @((Invoke-Api GET '/api/Modeles' -Token $ta).Json | Where-Object { $_.nom -like 'SIM *' })) { Invoke-Api DELETE "/api/Modeles/$($m.id)" -Token $ta | Out-Null }
    foreach ($c in @((Invoke-Api GET '/api/SnmpCredentials' -Token $ta).Json | Where-Object { $_.name -like 'SNMP*' -or $_.name -like 'SV3*' })) { Invoke-Api DELETE "/api/SnmpCredentials/$($c.id)" -Token $ta | Out-Null }
    foreach ($p in @((Invoke-Api GET '/api/SnmpProfiles' -Token $ta).Json | Where-Object { $_.name -eq 'Profil SIM' })) { Invoke-Api DELETE "/api/SnmpProfiles/$($p.id)" -Token $ta | Out-Null }

    $r1 = Invoke-Api POST '/api/SnmpCredentials' (New-Cred @{ name = 'SV3 sans utilisateur'; v3Username = ''; v3AuthPassword = $auth; v3PrivPassword = $priv }) $ta
    $r2 = Invoke-Api POST '/api/SnmpCredentials' (New-Cred @{ name = 'SV3 sans chiffrement'; v3AuthPassword = $auth }) $ta
    $r3 = Invoke-Api POST '/api/SnmpCredentials' (New-Cred @{ name = 'SV3 court'; v3AuthPassword = $auth; v3PrivPassword = 'Court-7' }) $ta
    Check 'SV3-01' 'v3 sans utilisateur / authPriv sans mot de passe de chiffrement / 7 caractères : refus' "$($r1.Code),$($r2.Code),$($r3.Code)" '400,400,400' "$($r1.Text) | $($r2.Text) | $($r3.Text)"
    Write-Host "         messages : $($r1.Text) | $($r2.Text) | $($r3.Text)" -ForegroundColor DarkGray

    $c = Invoke-Api POST '/api/SnmpCredentials' (New-Cred @{ name = 'SNMPv3 Parc'; description = 'Recette'; v3AuthPassword = $auth; v3PrivPassword = $priv }) $ta
    $credId = $c.Json.id
    $list = Invoke-Api GET '/api/SnmpCredentials' -Token $ta
    Check 'SV3-02' 'profil « SNMPv3 Parc » créé ; la liste ne contient aucun mot de passe' "$($c.Code)/$($list.Text.Contains($auth) -or $list.Text.Contains($priv))/$(($list.Json | Where-Object id -eq $credId).hasPrivPassword)" '201/False/True' $c.Text
    $db = Invoke-LabSql 'SELECT Name, V3AuthPassword, V3PrivPassword FROM SnmpCredentials' -Full 'Name'
    Check 'SV3-03' 'base : V3AuthPassword et V3PrivPassword en enc:v1:' (@($db | Where-Object { $_ -like 'Name=SNMPv3*' }) -join ' ') { param($x) $x -match 'V3AuthPassword=enc:v1:' -and $x -match 'V3PrivPassword=enc:v1:' }
    $u = Invoke-Api PUT "/api/SnmpCredentials/$credId" (New-Cred @{ name = 'SNMPv3 Parc'; description = 'Recette (description modifiée)' }) $ta
    Check 'SV3-04' 'changer uniquement la description (mots de passe vides)' $u.Code 204 $u.Text

    # Profil d'OID, modèle avec le profil v3, deux imprimantes simulées
    $prof = Invoke-Api POST '/api/SnmpProfiles' (@{ name = 'Profil SIM'; isColor = $true; oidPageCounter = '1.3.6.1.2.1.43.10.2.1.4.1.1'
            oidTonerBlack = '1.3.6.1.2.1.43.11.1.1.9.1.1'; oidTonerCyan = '1.3.6.1.2.1.43.11.1.1.9.1.2'
            items = @(@{ category = 0; name = 'Toner noir'; oid = '1.3.6.1.2.1.43.11.1.1.9.1.1'; oidMaxCapacity = '1.3.6.1.2.1.43.11.1.1.8.1.1'; valueType = 1; colorHex = '#212529'; sortOrder = 1 },
                      @{ category = 0; name = 'Toner cyan'; oid = '1.3.6.1.2.1.43.11.1.1.9.1.2'; oidMaxCapacity = '1.3.6.1.2.1.43.11.1.1.8.1.2'; valueType = 1; colorHex = '#0dcaf0'; sortOrder = 2 },
                      @{ category = 2; name = 'Compteur'; oid = '1.3.6.1.2.1.43.10.2.1.4.1.1'; valueType = 2; sortOrder = 3 }) } | ConvertTo-Json -Depth 5) $ta
    $marque = (Invoke-Api GET '/api/Marques' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    if (-not $marque) { $marque = (Invoke-Api POST '/api/Marques' @{ nom = 'Labo' } $ta).Json }
    $lieu = (Invoke-Api GET '/api/Emplacements' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    if (-not $lieu) { $lieu = (Invoke-Api POST '/api/Emplacements' @{ nom = 'Labo'; code = 'LAB'; networks = @(@{ cidrIpv4 = '192.168.99.0/24'; isPrimary = $true }) } $ta).Json }
    $mod = (Invoke-Api POST '/api/Modeles' @{ nom = 'SIM LaserJet'; marqueId = $marque.id; snmpProfileId = $prof.Json.id } $ta).Json
    $mod.snmpCredentialId = $credId
    $m = Invoke-Api PUT "/api/Modeles/$($mod.id)" ($mod | Select-Object id, nom, code, marqueId, piloteId, snmpProfileId, snmpCredentialId) $ta
    Check 'prep' "modèle SIM LaserJet (profil d'OID + SNMPv3 Parc)" "$($prof.Code)/$($m.Code)" '201/204' "$($prof.Text) $($m.Text)"
    $pV3 = (Invoke-Api POST '/api/Imprimantes' @{ nomAffiche = 'SIM-V3'; adresseIp = '192.168.99.10'; modeleId = $mod.id; emplacementId = $lieu.id; snmpCommunity = 'inutilisee'; snmpVersion = 2 } $ta).Json
    $pV2 = (Invoke-Api POST '/api/Imprimantes' @{ nomAffiche = 'SIM-V2'; adresseIp = '192.168.99.11'; modeleId = $mod.id; emplacementId = $lieu.id; snmpCommunity = 'inutilisee'; snmpVersion = 2 } $ta).Json
    $g = (Invoke-Api GET "/api/Modeles/$($mod.id)" -Token $ta).Json
    Check 'SV3-05' 'modèle : identifiants « SNMPv3 Parc » (affichage de la fiche : à l''écran)' $g.snmpCredentialId $credId

    $diag = { param($id) Invoke-Api GET "/api/Imprimantes/$id/diagnostic" -Token $ta }
    $since = Get-SnmpSimMark
    $d = & $diag $pV3.id
    $summary = { param($j) "{0}|{1}|{2}" -f $j.serialNumber, $j.pageCounter, (($j.toners | ForEach-Object { "$($_.color)=$($_.currentLevel)" }) -join ',') }
    Check 'SV3-06' 'diagnostic SIM-V3 (v3) : n° de série, compteur, toners' (& $summary $d.Json) { param($x) $x -like 'SIMV3-0001|48213|*62*18*' } $d.Text
    $log = Get-SnmpSimLog $since
    Check 'SV3-06' 'simulateur : requêtes reçues en SNMPv3 (securityModel 3, authPriv)' ([bool]($log -match 'securityModel 3, securityName autoprint\.v3, securityLevel 3')) $true
    $p = (Invoke-Api GET "/api/Imprimantes/$($pV3.id)" -Token $ta).Json
    Check 'SV3-07' 'fiche après diagnostic : n° de série et dernière réponse renseignés (cycle de supervision et PDF : à l''écran)' "$($p.serialNumber)/$([bool]$p.lastSeen)" 'SIMV3-0001/True'

    # SV3-08 : mauvais mot de passe
    $bad = 'Mauvais-' + (New-TestPassword).Substring(3, 10)
    Invoke-Api PUT "/api/SnmpCredentials/$credId" (New-Cred @{ name = 'SNMPv3 Parc'; description = 'Recette'; v3AuthPassword = $bad }) $ta | Out-Null
    $since8 = Get-Date
    $d = & $diag $pV3.id
    $t = Invoke-Api POST '/api/SnmpProfiles/test' @{ ipAddress = '192.168.99.10'; credentialId = $credId; items = @(@{ name = 'Toner noir'; oid = '1.3.6.1.2.1.43.11.1.1.9.1.1'; category = 0; valueType = 0 }) } $ta
    Write-Host "         diagnostic : $($d.Json.status) | éditeur d'OID : $($t.Code) $($t.Text)" -ForegroundColor DarkGray
    Check 'SV3-08' 'mauvais mot de passe : diagnostic sans données, message v3 explicite (éditeur d''OID)' "$([string]::IsNullOrEmpty($d.Json.serialNumber))/$($t.Code)/$($t.Text -match 'SNMPv3')" 'True/400/True' $t.Text
    $leak = Invoke-OnPrint { param($s, $w) $e = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $s } -ErrorAction SilentlyContinue | Where-Object { $_.Message -like "*$w*" }
        $f = Get-ChildItem 'C:\Program Files\Autoprint Server', 'C:\ProgramData\Autoprint' -Recurse -Include *.log, *.txt -ErrorAction SilentlyContinue | Where-Object LastWriteTime -ge $s | Select-String -SimpleMatch $w -List
        @($e).Count + @($f).Count } @($since8, $bad)
    Check 'SV3-08' 'mot de passe absent du message, du journal Windows et des fichiers journaux' "$($t.Text.Contains($bad))/$leak" 'False/0'
    $u = Invoke-Api PUT "/api/SnmpCredentials/$credId" (New-Cred @{ name = 'SNMPv3 Parc'; description = 'Recette'; v3AuthPassword = $auth }) $ta
    $d = & $diag $pV3.id
    Check 'SV3-08' 'bon mot de passe remis : diagnostic de nouveau OK' "$($u.Code)/$($d.Json.serialNumber)" '204/SIMV3-0001'

    # SV3-09 / SV3-10 : exception v2c sur une imprimante du même modèle
    $c2 = (Invoke-Api POST '/api/SnmpCredentials' @{ name = 'SNMP v2c parc'; version = 2; community = 'parc' } $ta).Json
    $pr = (Invoke-Api GET "/api/Imprimantes/$($pV2.id)" -Token $ta).Json; $pr.snmpCredentialId = $c2.id
    $u = Invoke-Api PUT "/api/Imprimantes/$($pV2.id)" $pr $ta
    $since9 = Get-SnmpSimMark
    $d2 = & $diag $pV2.id; $d3 = & $diag $pV3.id
    $log = Get-SnmpSimLog $since9
    Check 'SV3-09' 'SIM-V2 en v2c (profil d''exception) et SIM-V3 en v3 : supervision OK' "$($u.Code)/$($d2.Json.serialNumber)/$($d3.Json.serialNumber)" '204/SIMV2-0002/SIMV3-0001' $u.Text
    Check 'SV3-09' 'simulateur : SIM-V2 interrogée en v2c (securityModel 2)' ([bool]($log -match 'transportDomain \(1, 3, 6, 1, 6, 1, 1, 1\).*securityModel 2')) $true
    $pr = (Invoke-Api GET "/api/Imprimantes/$($pV2.id)" -Token $ta).Json; $pr.snmpCredentialId = $null
    $u = Invoke-Api PUT "/api/Imprimantes/$($pV2.id)" $pr $ta
    $since10 = Get-SnmpSimMark
    $d2 = & $diag $pV2.id
    $log = Get-SnmpSimLog $since10
    # En v3 (contexte vide), le simulateur sert les données « public » (n° SIMV3-0001) aussi sur .11
    Check 'SV3-10' 'exception vidée : SIM-V2 reprend le profil du modèle (v3)' "$($u.Code)/$($d2.Json.serialNumber)/$([bool]($log -match 'transportDomain \(1, 3, 6, 1, 6, 1, 1, 1\).*securityModel 3'))" '204/SIMV3-0001/True'

    # SV3-11 : imprimante sans profil (ni elle, ni son modèle)
    $mod0 = (Invoke-Api POST '/api/Modeles' @{ nom = 'SIM sans profil'; marqueId = $marque.id; snmpProfileId = $prof.Json.id } $ta).Json
    $pr = (Invoke-Api GET "/api/Imprimantes/$($pV2.id)" -Token $ta).Json; $pr.modeleId = $mod0.id; $pr.snmpCommunity = 'parc'; $pr.snmpVersion = 2
    $u = Invoke-Api PUT "/api/Imprimantes/$($pV2.id)" $pr $ta
    $g = (Invoke-Api GET "/api/Imprimantes/$($pV2.id)" -Token $ta).Json
    $d2 = & $diag $pV2.id
    Check 'SV3-11' 'sans profil : communauté « parc » / v2c enregistrées et utilisées' "$($u.Code)/$($g.snmpCommunity)/$($g.snmpVersion)/$($d2.Json.serialNumber)" '204/parc/2/SIMV2-0002'

    # SV3-12 : éditeur de profils d'OID avec les identifiants v3
    $t = Invoke-Api POST '/api/SnmpProfiles/test' @{ ipAddress = '192.168.99.10'; credentialId = $credId; items = @(@{ name = 'Toner cyan'; oid = '1.3.6.1.2.1.43.11.1.1.9.1.2'; category = 0; valueType = 0 }) } $ta
    $s = Invoke-Api POST '/api/SnmpProfiles/scan-printer' @{ ipAddress = '192.168.99.10'; credentialId = $credId } $ta
    Check 'SV3-12' 'Tester / Auto-détecter en v3 : valeurs lues' "$($t.Code)/$($t.Text -match '18')/$($s.Code)/$(@($s.Json).Count -gt 0)" '200/True/200/True' "$($t.Text) $($s.Text)"

    # SV3-16 : utilisateur avec seulement PRINTER_READ
    $tl = Get-Token 'lecteur'
    $a = Invoke-Api GET "/api/Imprimantes/$($pV3.id)" -Token $tl; $b = Invoke-Api GET '/api/SnmpCredentials' -Token $tl
    Check 'SV3-16' 'lecteur : fiche imprimante lisible, identifiants SNMP indisponibles' "$($a.Code)/$($b.Code)" '200/403'

    # SV3-14 / SV3-15 : sauvegarde et restauration (même serveur)
    if (-not (Get-Plain 'backup')) { Set-Plain 'backup' (New-TestPassword) }
    $prot = Invoke-Api POST '/api/Backup/export' @{ secretsPassword = (Get-Plain 'backup') } $ta
    $plain = Invoke-Api POST '/api/Backup/export' @{} $ta
    $w = ($prot.Json | Select-Object *); $w | Add-Member -NotePropertyName secretsPassword -NotePropertyValue (Get-Plain 'backup') -Force
    $r = Invoke-Api POST '/api/Backup/restore' $w $ta
    $ta = Get-Token 'admin'
    $g = (Invoke-Api GET "/api/Modeles/$($mod.id)" -Token $ta).Json; $d = & $diag $pV3.id
    Check 'SV3-14' 'sauvegarde protégée restaurée : profil lié au modèle, supervision v3 OK' "$($r.Code)/$($g.snmpCredentialId -eq $credId)/$($d.Json.serialNumber)" '200/True/SIMV3-0001' $r.Text
    $r = Invoke-Api POST '/api/Backup/restore' $plain.Text $ta
    $ta = Get-Token 'admin'
    $d = & $diag $pV3.id
    Check 'SV3-15' 'sauvegarde sans mot de passe restaurée : secrets actuels conservés (v3 OK)' "$($r.Code)/$($d.Json.serialNumber)" '200/SIMV3-0001' $r.Text

    # SV3-13 : suppression d'un profil utilisé par un modèle et une imprimante
    $tmp = (Invoke-Api POST '/api/SnmpCredentials' (New-Cred @{ name = 'SV3 temporaire'; v3AuthPassword = $auth; v3PrivPassword = $priv }) $ta).Json
    $g = (Invoke-Api GET "/api/Modeles/$($mod.id)" -Token $ta).Json; $g.snmpCredentialId = $tmp.id
    Invoke-Api PUT "/api/Modeles/$($mod.id)" ($g | Select-Object id, nom, code, marqueId, piloteId, snmpProfileId, snmpCredentialId) $ta | Out-Null
    $pr = (Invoke-Api GET "/api/Imprimantes/$($pV2.id)" -Token $ta).Json; $pr.snmpCredentialId = $tmp.id
    Invoke-Api PUT "/api/Imprimantes/$($pV2.id)" $pr $ta | Out-Null
    $pr3 = (Invoke-Api GET "/api/Imprimantes/$($pV3.id)" -Token $ta).Json; $pr3.snmpCommunity = 'public'; $pr3.snmpVersion = 2
    Invoke-Api PUT "/api/Imprimantes/$($pV3.id)" $pr3 $ta | Out-Null
    $del = Invoke-Api DELETE "/api/SnmpCredentials/$($tmp.id)" -Token $ta
    $d3 = & $diag $pV3.id; $d2 = & $diag $pV2.id
    Check 'SV3-13' 'suppression : « 1 modèle(s) et 1 imprimante(s) … par défaut » ; supervision toujours OK (paramètres par défaut)' "$($del.Code)/$($del.Text -match '1 modèle\(s\) et 1 imprimante\(s\)')/$($d3.Json.serialNumber)/$($d2.Json.serialNumber)" '200/True/SIMV3-0001/SIMV2-0002' $del.Text

    # Nettoyage : imprimantes simulées sans pilote (elles feraient échouer la synchronisation des autres lots) et leurs modèles
    foreach ($id in $pV3.id, $pV2.id) { Invoke-Api DELETE "/api/Imprimantes/$id" -Token $ta | Out-Null }
    Invoke-Sync $ta | Out-Null
    foreach ($id in $mod.id, $mod0.id) { Invoke-Api DELETE "/api/Modeles/$id" -Token $ta | Out-Null }
}

# ------------------------------------------------------------------ lot Divers (SQL Server uniquement : modifie la base directement)
function Invoke-LabSqlExec([string]$Sql) {
    Invoke-OnPrint {
        param($q)
        $cs = (Get-Content 'C:\Program Files\Autoprint Server\appsettings.json' -Raw | ConvertFrom-Json).Database.ConnectionStrings.SqlServer
        $cs = $cs -replace 'Trust Server Certificate', 'TrustServerCertificate'
        $c = New-Object Data.SqlClient.SqlConnection $cs; $c.Open(); $cmd = $c.CreateCommand(); $cmd.CommandText = $q; [void]$cmd.ExecuteNonQuery(); $c.Close()
    } @($Sql)
}
function New-V3Hash([string]$Password) {
    # Format ASP.NET Identity v3 (PasswordHasher) : PBKDF2-HMAC-SHA512, 100 000 itérations, sel 16 octets, clé 32 octets
    $salt = New-Object byte[] 16; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes($Password, $salt, 100000, [Security.Cryptography.HashAlgorithmName]::SHA512)
    $sub = $kdf.GetBytes(32)
    $out = New-Object byte[] 61; $out[0] = 1
    $be = { param($v) $b = [BitConverter]::GetBytes([uint32]$v); [Array]::Reverse($b); $b }
    [Array]::Copy((& $be 2), 0, $out, 1, 4); [Array]::Copy((& $be 100000), 0, $out, 5, 4); [Array]::Copy((& $be 16), 0, $out, 9, 4)
    [Array]::Copy($salt, 0, $out, 13, 16); [Array]::Copy($sub, 0, $out, 29, 32)
    [Convert]::ToBase64String($out)
}

function Lot-Divers {
    Write-Host "`n--- Lot Divers : import, noms, anciens mots de passe, SNMP / SMTP / AD, comptes, sessions, CORS, proxy, PDF ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    $lieu = (Invoke-Api GET '/api/Emplacements' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    $modele = (Invoke-Api GET '/api/Modeles' -Token $ta).Json | Where-Object nom -eq 'Texte brut' | Select-Object -First 1

    # P1-05, PA-05 : import depuis le spouleur local
    Invoke-OnPrint { foreach ($n in 'Import-A', 'Import-B') { if (-not (Get-Printer $n -ErrorAction SilentlyContinue)) { Add-Printer -Name $n -DriverName 'Generic / Text Only' -PortName 'IP_192.168.99.10' } } }
    foreach ($p in @((Invoke-Api GET '/api/Imprimantes' -Token $ta).Json | Where-Object { $_.nomAffiche -like 'Import-*' })) { Invoke-Api DELETE "/api/Imprimantes/$($p.id)" -Token $ta | Out-Null }
    $scan = (Invoke-Api POST '/api/Import/Scan/Printers' -Token $ta).Json | Where-Object { $_.name -like 'Import-*' }
    $sel = '[' + ((@($scan) | ForEach-Object { $ok = $_.name -eq 'Import-A'
        '{"name":"' + $_.name + '","portName":"192.168.99.10","isShared":false,"shareName":"","isDirectPrintingEnabled":false,"selectedLieuId":' + $(if ($ok) { $lieu.id } else { 0 }) + ',"selectedModeleId":' + $(if ($ok) { $modele.id } else { 0 }) + '}' }) -join ',') + ']'
    $imp = Invoke-Api POST '/api/Import/Import/Printers' $sel $ta
    $list = (Invoke-Api GET '/api/Imprimantes' -Token $ta).Json | Where-Object { $_.nomAffiche -like 'Import-*' }
    $st = ($list | Sort-Object nomAffiche | ForEach-Object { "$($_.nomAffiche)=$($_.status)" }) -join ','
    Check 'P1-05' 'scan du spouleur puis import de deux imprimantes : présentes dans la liste' "$(@($scan).Count)/$($imp.Code)/$(@($list).Count)" '2/200/2' $imp.Text
    $again = Invoke-Api POST '/api/Import/Import/Printers' ('[' + ($sel.Trim('[', ']').Split('}')[0]) + '}]') $ta
    Check 'PA-05' 'sans lieu ni modèle : statut « à corriger » ; réimport : « 1 ignorée(s) »' "$st | $($again.Text)" { param($x) $x -match 'Import-B=(ImportedNeedsFix|1)(,| |$)' -and $x -match '1 ignorée' } "$st | $($again.Text)"
    foreach ($p in @($list)) { Invoke-Api DELETE "/api/Imprimantes/$($p.id)" -Token $ta | Out-Null }
    Invoke-OnPrint { foreach ($n in 'Import-A', 'Import-B') { Remove-Printer $n -ErrorAction SilentlyContinue } }

    # P1-12 : nom désormais interdit, seul le commentaire est modifié
    $q = (Invoke-Api POST '/api/Imprimantes' @{ nomAffiche = 'Ancien-Nom'; adresseIp = '192.168.99.10'; modeleId = $modele.id; emplacementId = $lieu.id } $ta).Json
    Invoke-LabSqlExec "UPDATE Imprimantes SET NomAffiche = N'Ancien/Nom*', NomPartage = N'Ancien/Nom*' WHERE Id = $($q.id)"
    $full = (Invoke-Api GET "/api/Imprimantes/$($q.id)" -Token $ta).Json; $full.commentaire = 'P1-12'
    $u1 = Invoke-Api PUT "/api/Imprimantes/$($q.id)" $full $ta
    $full.nomAffiche = 'Autre/Nom*'; $u2 = Invoke-Api PUT "/api/Imprimantes/$($q.id)" $full $ta
    Check 'P1-12' 'nom interdit existant : commentaire seul accepté ; renommage en nom interdit refusé' "$($u1.Code)/$($u2.Code)" '204/400' "$($u1.Text) $($u2.Text)"
    Invoke-Api DELETE "/api/Imprimantes/$($q.id)" -Token $ta | Out-Null

    # P2-16, P2-42 : anciens mots de passe (9 caractères) ; hachage v3 et SHA-256 hérité
    $roles = @{}; (Invoke-Api GET '/api/roles' -Token $ta).Json | ForEach-Object { $roles[$_.name] = $_.id }
    $users = @{}; (Invoke-Api GET '/api/users' -Token $ta).Json | ForEach-Object { $users[$_.username] = $_.id }
    foreach ($n in 'old.v3', 'old.sha', 'pa14', 'pa16', 'sc03') { if ($users.ContainsKey($n)) { Invoke-Api DELETE "/api/users/$($users[$n])" -Token $ta | Out-Null } }
    foreach ($n in 'old.v3', 'old.sha') {
        $r = Invoke-Api POST '/api/users' @{ username = $n; password = (New-TestPassword); displayName = $n; email = "$n@lab.autoprint.test"; roleId = $roles['Lecteur']; forceChangePassword = $false } $ta
        $short = 'Ancien9' + (Get-Random -Minimum 10 -Maximum 99)   # 9 caractères
        $hash = if ($n -eq 'old.v3') { New-V3Hash $short } else { $b = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($short)); -join ($b | ForEach-Object { $_.ToString('x2') }) }
        Invoke-LabSqlExec "UPDATE Users SET PasswordHash = '$hash', ForceChangePassword = 0 WHERE Id = $($r.Json.id)"
        $l = Invoke-Login $n $short
        if ($n -eq 'old.v3') { Check 'P2-16' 'ancien mot de passe de 9 caractères (hachage actuel) : connexion acceptée, pas de changement imposé' "$($l.Code)/$($l.Json.passwordExpired)" '200/False' $l.Text }
        else { Check 'P2-42' 'ancien hachage SHA-256 : connexion acceptée puis changement imposé' "$($l.Code)/$($l.Json.passwordExpired)" '200/True' $l.Text }
    }

    # PA-14, PA-16 : case « changement obligatoire »
    $p14 = New-TestPassword
    $r = Invoke-Api POST '/api/users' @{ username = 'pa14'; password = $p14; displayName = 'pa14'; email = 'pa14@lab.autoprint.test'; roleId = $roles['Lecteur']; forceChangePassword = $false } $ta
    $l = Invoke-Login 'pa14' $p14
    Check 'PA-14' 'case décochée : première connexion sans changement imposé' "$($l.Code)/$($l.Json.passwordExpired)" '200/False'
    $r = Invoke-Api POST '/api/users' @{ username = 'pa16'; password = (New-TestPassword); displayName = 'pa16'; email = 'pa16@lab.autoprint.test'; roleId = $roles['Lecteur']; forceChangePassword = $true } $ta
    $u = (Invoke-Api GET "/api/users/$($r.Json.id)" -Token $ta).Json; $u | Add-Member -NotePropertyName displayName -NotePropertyValue 'pa16 renommé' -Force
    $put = Invoke-Api PUT "/api/users/$($r.Json.id)" $u $ta
    $after = (Invoke-Api GET "/api/users/$($r.Json.id)" -Token $ta).Json
    Check 'PA-16' 'modification du seul nom : « changement obligatoire » reste coché' "$($put.Code)/$($after.displayName)/$($after.forceChangePassword)" '204/pa16 renommé/True' $put.Text

    # SC-03 : permission retirée au rôle d'un utilisateur connecté : renouvellement refusé
    $rr = Invoke-Api POST '/api/roles' @{ name = 'SC03'; description = 'Recette SC-03'; permissionIds = @(((Invoke-Api GET '/api/roles/permissions' -Token $ta).Json | Where-Object code -in 'PRINTER_READ', 'LOCATION_READ').id) } $ta
    $roles = @{}; (Invoke-Api GET '/api/roles' -Token $ta).Json | ForEach-Object { $roles[$_.name] = $_.id }
    $p3 = New-TestPassword
    Invoke-Api POST '/api/users' @{ username = 'sc03'; password = $p3; displayName = 'sc03'; email = 'sc03@lab.autoprint.test'; roleId = $roles['SC03']; forceChangePassword = $false } $ta | Out-Null
    $t3 = (Invoke-Login 'sc03' $p3).Json.token
    $before = (Invoke-Api POST '/api/auth/refresh' -Token $t3).Code
    $role = (Invoke-Api GET "/api/roles/$($roles['SC03'])" -Token $ta).Json; $role.permissionIds = @(((Invoke-Api GET '/api/roles/permissions' -Token $ta).Json | Where-Object code -eq 'PRINTER_READ').id)
    Invoke-Api PUT "/api/roles/$($roles['SC03'])" $role $ta | Out-Null
    Start-Sleep 31
    $afterR = Invoke-Api POST '/api/auth/refresh' -Token $t3
    $relog = Invoke-Login 'sc03' $p3
    Check 'SC-03' 'permission retirée : renouvellement refusé ; reconnexion avec les nouveaux droits' "$before/$($afterR.Code)/$($relog.Code)/$(($relog.Json.permissions) -join ',')" '200/401/200/PRINTER_READ'
    foreach ($n in 'old.v3', 'old.sha', 'pa14', 'pa16', 'sc03') { $id = ((Invoke-Api GET '/api/users' -Token $ta).Json | Where-Object username -eq $n).id; if ($id) { Invoke-Api DELETE "/api/users/$id" -Token $ta | Out-Null } }
    Invoke-Api DELETE "/api/roles/$($roles['SC03'])" -Token $ta | Out-Null

    # P2-19, P2-20 : test d'un profil d'OID
    $t = Invoke-Api POST '/api/SnmpProfiles/test' @{ ipAddress = '192.168.99.77'; community = 'public'; version = 2; items = @(@{ name = 'x'; oid = '1.3.6.1.2.1.1.1.0'; category = 3; valueType = 3 }) } $ta
    Check 'P2-19' 'IP sans imprimante : message de diagnostic clair' "$($t.Code)/$($t.Text -match 'pas répondu|ne répond pas')" '400/True' $t.Text
    $codes = foreach ($ip in '10.0.0.1; rm', '<script>') { $x = Invoke-Api POST '/api/SnmpProfiles/test' @{ ipAddress = $ip; items = @() } $ta; "$($x.Code):$($x.Text -match 'invalide')" }
    Check 'P2-20' 'adresses « 10.0.0.1; rm » et « <script> » : refus « adresse invalide »' ($codes -join ',') '400:True,400:True'

    # P2-21 : tests SMTP et AD avec de mauvais paramètres (détail utile conservé, réservé aux admins)
    $e = Invoke-Api POST '/api/Settings/TestEmail' @{ host = 'smtp.inexistant.lab.autoprint.test'; port = 25; user = 'x'; password = 'Mauvais-1'; ssl = $true; from = 'a@lab.autoprint.test'; to = 'admin@lab.autoprint.test' } $ta
    $a = Invoke-Api POST '/api/Settings/TestAd' @{ domain = 'domaine-inexistant.test'; useServiceAccount = $true; serviceUser = 'x'; servicePassword = 'Mauvais-1' } $ta
    Check 'P2-21' 'SMTP / AD erronés : refus avec le détail utile' "$($e.Code)/$($a.Code)" { param($x) $x -match '^(400|500)/(400|500)$' } "$($e.Text) | $($a.Text)"
    Write-Host "         SMTP : $($e.Text)`n         AD : $($a.Text)" -ForegroundColor DarkGray

    # SC-09 : restauration sans mot de passe : message sur la conservation des secrets
    $plain = Invoke-Api POST '/api/Backup/export' @{} $ta
    $r = Invoke-Api POST '/api/Backup/restore' $plain.Text $ta
    # SC-09 : le message sur les secrets conservés est affiché par la console (Paramètres) : contrôle à l'écran
    Check 'SC-09' 'restauration sans mot de passe acceptée (message de la console : à l''écran)' $r.Code 200 $r.Text
    Write-Host "         message : $($r.Text)" -ForegroundColor DarkGray
    $ta = Get-Token 'admin'

    # SD-05 : recherche d'un groupe AD contenant « & », « # » et un espace
    Invoke-OnDc { if (-not (Get-ADGroup -Filter "Name -eq 'GG Test & #1'")) { New-ADGroup -Name 'GG Test & #1' -GroupScope Global -Path ((Get-ADOrganizationalUnit -Filter "Name -eq 'Groupes'").DistinguishedName) } }
    $s = Invoke-Api GET ('/api/roles/ad/search?type=0&q=' + [uri]::EscapeDataString('GG Test & #1')) -Token $ta
    Check 'SD-05' 'liaison AD : recherche « GG Test & #1 »' "$($s.Code)/$($s.Text -match 'GG Test & #1')" '200/True' $s.Text

    # SD-07 : requête depuis une autre origine (CORS)
    $req = [Net.HttpWebRequest]::Create("$BaseUrl/api/Imprimantes"); $req.Headers['Origin'] = 'https://autre-origine.example'; $req.Headers['Authorization'] = "Bearer $ta"
    $resp = $req.GetResponse(); $acao = $resp.Headers['Access-Control-Allow-Origin']; $resp.Close()
    $req = [Net.HttpWebRequest]::Create("$BaseUrl/api/Imprimantes"); $req.Method = 'OPTIONS'; $req.Headers['Origin'] = 'https://autre-origine.example'; $req.Headers['Access-Control-Request-Method'] = 'GET'
    try { $resp = $req.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response }; $acao2 = $resp.Headers['Access-Control-Allow-Origin']; $resp.Close()
    Check 'SD-07' 'autre origine : aucun en-tête Access-Control-Allow-Origin (GET et pré-vol)' "[$acao][$acao2]" '[][]'

    # SE-09 : bibliothèques du rapport PDF
    $files = Invoke-OnPrint { $d = 'C:\Program Files\Autoprint Server'; "$(Test-Path "$d\runtimes\win-x64\native\libSkiaSharp.dll")/$(Test-Path "$d\Licenses\AutoprintSans-OFL-LICENSE.txt")/$(@(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddDays(-1) } -ErrorAction SilentlyContinue | Where-Object Message -match 'libSkiaSharp').Count)" }
    Check 'SE-09' 'libSkiaSharp.dll et licence de la police présentes ; aucune erreur libSkiaSharp' $files 'True/True/0'

    # SE-08 / SD-11 : rapport PDF avec plus de 45 imprimantes, mesures « Compteur » et « Pannes »
    $ids = foreach ($n in 1..62) { (Invoke-Api POST '/api/Imprimantes' @{ nomAffiche = ('PDF-{0:D2}' -f $n); adresseIp = "10.77.0.$n"; modeleId = $modele.id; emplacementId = $lieu.id } $ta).Json.id }
    $since = Get-Date
    $s = Invoke-Api POST '/api/ReportSchedules' @{ reportName = 'Recette SE-08 é à ç'; selectedMetricsJson = '["Alerts","Pages"]'; scopeFilterJson = '{}'; frequency = 'Hebdomadaire'; emailRecipients = 'admin@lab.autoprint.test'; format = 'PDF'; isActive = $true; predictionThresholdDays = 14; runHour = 7; runMinute = 30 } $ta
    $send = Invoke-Api POST "/api/ReportSchedules/$($s.Json.id)/send" -Token $ta
    Start-Sleep 6
    $m = @(Get-CapturedMails $since) | Select-Object -Last 1
    $b64 = if ($m) { [regex]::Match($m.Text, '(?s)Content-Type: application/pdf.*?\r?\n\r?\n([A-Za-z0-9+/=\r\n]+)').Groups[1].Value -replace '\s', '' } else { '' }
    $pdfFile = Join-Path $env:TEMP 'recette-SE-08.pdf'
    if ($b64) { [IO.File]::WriteAllBytes($pdfFile, [Convert]::FromBase64String($b64)) }
    $pages = if (Test-Path $pdfFile) { ([regex]::Matches([IO.File]::ReadAllText($pdfFile, [Text.Encoding]::GetEncoding(28591)), '/Type\s*/Page[^s]')).Count } else { 0 }
    $ko = if (Test-Path $pdfFile) { [int]((Get-Item $pdfFile).Length / 1KB) } else { 0 }
    Check 'SE-08' 'rapport PDF 60+ imprimantes, Compteur + Pannes : reçu, quelques dizaines de Ko, plusieurs pages' "$($send.Code)/$ko Ko/$pages pages" { param($x) $x -match '^200/\d+ Ko/[2-9] pages' -and [int]($x.Split('/')[1] -replace ' Ko') -lt 300 } "$($send.Text) $pdfFile"
    Write-Host "         PDF enregistré pour contrôle visuel (SD-11 : bandeau, titre, accents) : $pdfFile" -ForegroundColor DarkGray
    Invoke-Api DELETE "/api/ReportSchedules/$($s.Json.id)" -Token $ta | Out-Null
    foreach ($id in $ids) { Invoke-Api DELETE "/api/Imprimantes/$id" -Token $ta | Out-Null }
}

# ------------------------------------------------------------------ lot Infra : spouleur bloqué, file non supprimable, AD injoignable, Base DN, proxy
function Lot-Infra {
    Write-Host "`n--- Lot Infra : spouleur bloqué (SB-06), file non supprimable (SD-03), AD injoignable (PA-12), Base DN (PA-13), proxy (PA-11) ---" -ForegroundColor Cyan
    $ta = Get-Token 'admin'
    $lieu = (Invoke-Api GET '/api/Emplacements' -Token $ta).Json | Where-Object nom -eq 'Labo' | Select-Object -First 1
    $modele = (Invoke-Api GET '/api/Modeles' -Token $ta).Json | Where-Object nom -eq 'Texte brut' | Select-Object -First 1

    # Restes d'un passage précédent
    foreach ($x in @((Invoke-Api GET '/api/Imprimantes' -Token $ta).Json | Where-Object nomAffiche -eq 'Spouleur-Bloque')) { Invoke-LabSqlExec "DELETE FROM Imprimantes WHERE Id = $($x.id)" }

    # SB-06 : spouleur Windows suspendu pendant une synchronisation
    $p = (Invoke-Api POST '/api/Imprimantes' @{ nomAffiche = 'Spouleur-Bloque'; adresseIp = '192.168.99.10'; modeleId = $modele.id; emplacementId = $lieu.id; estPartagee = $true } $ta).Json
    $susp = @'
using System; using System.Runtime.InteropServices;
public static class Proc { [DllImport("ntdll.dll")] public static extern int NtSuspendProcess(IntPtr h); [DllImport("ntdll.dll")] public static extern int NtResumeProcess(IntPtr h); }
'@
    Invoke-OnPrint { param($src) Add-Type -TypeDefinition $src; [Diagnostics.Process]::EnterDebugMode(); [void][Proc]::NtSuspendProcess((Get-Process spoolsv).Handle); 'suspendu' } @($susp) | Out-Null
    $sw = [Diagnostics.Stopwatch]::StartNew(); $s1 = Invoke-Sync $ta; $sec = [int]$sw.Elapsed.TotalSeconds
    Invoke-OnPrint { param($src) Add-Type -TypeDefinition $src; [Diagnostics.Process]::EnterDebugMode(); [void][Proc]::NtResumeProcess((Get-Process spoolsv).Handle); 'repris' } @($susp) | Out-Null
    Check 'SB-06' 'spouleur suspendu : échec au bout de ~60 s avec message clair' "$sec s / $($s1.Text -match 'n.a pas répondu|60 s')" { param($x) $x -match '^(5[5-9]|6\d|7\d|8\d|9\d) s / True' } $s1.Text
    Start-Sleep 5
    $s2 = Invoke-Sync $ta
    Check 'SB-06' 'spouleur relancé : la synchronisation suivante aboutit' ($s2.Text -match '"success":true') $true $s2.Text

    # SD-03 : file Windows impossible à supprimer (SYSTEM sans le droit de gestion), suppression puis synchronisation
    Invoke-OnPrint { $sd = (Get-Printer 'Spouleur-Bloque' -Full).PermissionSDDL; $i = $sd.IndexOf('D:') + 2; Set-Printer 'Spouleur-Bloque' -PermissionSDDL ($sd.Substring(0, $i) + '(D;;SD;;;SY)(D;;SD;;;BA)' + $sd.Substring($i)) }
    Invoke-Api DELETE "/api/Imprimantes/$($p.id)" -Token $ta | Out-Null
    $s3 = Invoke-Sync $ta
    $still = Invoke-OnPrint { [bool](Get-Printer 'Spouleur-Bloque' -ErrorAction SilentlyContinue) }
    Check 'SD-03' 'file non supprimable : la synchronisation signale l''échec' "$still/$($s3.Text -match 'ERREUR|chec|refus')" 'True/True' $s3.Text
    # Suppression de la file interdite de suppression : par le registre du spouleur (Set-Printer est aussi refusé)
    Invoke-OnPrint { Stop-Service Spooler -Force; Start-Sleep 2; Remove-Item 'HKLM:\SYSTEM\CurrentControlSet\Control\Print\Printers\Spouleur-Bloque' -Recurse -Force -ErrorAction SilentlyContinue; Start-Service Spooler; Start-Sleep 3; Restart-Service AutoprintSpooler }

    # PA-12 : contrôleur de domaine injoignable (LDAP / Kerberos bloqués sur SRV-PRINT ; ses deux adresses, .11 étant celle du simulateur SNMP)
    Invoke-OnPrint { New-NetFirewallRule -Name 'Recette-PA12' -DisplayName 'Recette PA-12' -Direction Outbound -Action Block -RemoteAddress 192.168.99.10, 192.168.99.11 -Protocol TCP -RemotePort 88, 389, 636, 3268, 3269, 445 | Out-Null
        New-NetFirewallRule -Name 'Recette-PA12u' -DisplayName 'Recette PA-12 UDP' -Direction Outbound -Action Block -RemoteAddress 192.168.99.10, 192.168.99.11 -Protocol UDP -RemotePort 88, 389 | Out-Null }
    $l = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2')
    Invoke-OnPrint { Remove-NetFirewallRule -Name 'Recette-PA12', 'Recette-PA12u' }
    Check 'PA-12' 'AD injoignable : « Annuaire Active Directory injoignable », pas de connexion' "$($l.Code)/$($l.Text -match 'injoignable')" '503/True' $l.Text
    Write-Host "         message : $($l.Text)" -ForegroundColor DarkGray

    # PA-13 : utilisateur hors de la base de recherche, avec un rôle manuel (ad.user2 : Lecteur manuel), membre d'un groupe lié à SuperAdmin
    Invoke-OnDc { Add-ADGroupMember 'GG_Autoprint_Admins' -Members 'ad.user2' }
    $dto = Get-SettingsDto $ta; $oldBase = $dto['AdBaseDn']
    $dto['AdBaseDn'] = [string](Invoke-OnDc { [string](Get-ADOrganizationalUnit -Filter "Name -eq 'Groupes'").DistinguishedName })
    $sv = Invoke-Api POST '/api/Settings/Save' $dto $ta
    Start-Sleep 2
    $l = Invoke-Login 'ad.user2' (Get-Plain 'ad.user2')
    $perms = if ($l.Json.token) { $l.Json.permissions -join ',' } else { '' }
    $dto['AdBaseDn'] = $oldBase; Invoke-Api POST '/api/Settings/Save' $dto $ta | Out-Null
    Invoke-OnDc { Remove-ADGroupMember 'GG_Autoprint_Admins' -Members 'ad.user2' -Confirm:$false }
    Check 'PA-13' 'hors Base DN, rôle manuel Lecteur, membre d''un groupe admin : seul Lecteur' "$($sv.Code)/$($l.Code)/$perms" { param($x) $x -match '^200/200/' -and $x -notmatch 'ADMIN_ACCESS' -and $x -match 'PRINTER_READ' } $l.Text

    # PA-11 : proxy inverse déclaré : l'IP du client (X-Forwarded-For) est utilisée par la limite par IP
    $cfg = { param($on) $f = 'C:\Program Files\Autoprint Server\appsettings.json'; $j = Get-Content $f -Raw | ConvertFrom-Json
        if ($on) { $j | Add-Member -NotePropertyName ReverseProxy -NotePropertyValue ([pscustomobject]@{ KnownProxies = @('192.168.99.1') }) -Force } else { $j.PSObject.Properties.Remove('ReverseProxy') }
        $j | ConvertTo-Json -Depth 10 | Set-Content $f -Encoding UTF8; Import-Module WebAdministration; Restart-WebAppPool Autoprint }
    $try = { param($forwarded) $codes = foreach ($n in 1..12) { $req = [Net.HttpWebRequest]::Create("$BaseUrl/api/auth/login"); $req.Method = 'POST'; $req.ContentType = 'application/json'
            if ($forwarded) { $req.Headers['X-Forwarded-For'] = "10.66.0.$n" }
            $b = [Text.Encoding]::UTF8.GetBytes('{"username":"px' + $n + '","password":"x"}'); $s = $req.GetRequestStream(); $s.Write($b, 0, $b.Length); $s.Close()
            try { $r = $req.GetResponse(); [int]$r.StatusCode; $r.Close() } catch [Net.WebException] { [int]$_.Exception.Response.StatusCode; $_.Exception.Response.Close() } }
        ($codes | Where-Object { $_ -eq 429 }).Count }
    Start-Sleep 62
    Invoke-OnPrint $cfg @($true); Start-Sleep 5
    $with = & $try $true
    Start-Sleep 62
    $spoof = & $try $false
    Invoke-OnPrint $cfg @($false); Start-Sleep 5; $script:Logins.Clear()
    Check 'PA-11' 'proxy déclaré : 12 connexions de 12 IP réelles sans 429 ; sans en-tête, la limite par IP joue' "$with/$spoof" { param($x) $x -match '^0/[1-9]' } "$with/$spoof"
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

if ($Lot -contains 'Tout') { $Lot = 'Comptes', 'Escalade', 'Sessions', 'Imprimantes', 'Web', 'AD', 'Cle', 'Donnees', 'Serveur', 'Email', 'Snmp', 'Limitation' }
foreach ($l in $Lot) { & "Lot-$l" }
Write-Host ("`nBilan : {0} OK, {1} ÉCHEC" -f $script:Score.OK, $script:Score.KO) -ForegroundColor $(if ($script:Score.KO) { 'Red' } else { 'Green' })
