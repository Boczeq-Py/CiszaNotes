# PowerShell 7. Uses the OS certificate trust store; does NOT disable TLS validation.
param(
    [string]$VulnerableUrl = 'http://127.0.0.1:5181',
    [string]$SecuredUrl = 'https://localhost:7182'
)
$ErrorActionPreference = 'Stop'
$script:passed = 0
$clients = [System.Collections.Generic.List[System.Net.Http.HttpClient]]::new()
. (Join-Path $PSScriptRoot 'Tests/HttpHelpers.ps1')

$injection = "') OR TRUE -- "
$piotrId = '20000000-0000-0000-0000-000000000003'
$snapshot = @{}
$sentinel = $null
try {
    foreach ($variant in @('Vulnerable','Secured')) {
        $secure = $variant -eq 'Secured'
        $base = if ($secure) { $SecuredUrl } else { $VulnerableUrl }
        $anna = New-Client
        $piotr = New-Client
        $anon = New-Client
        $root = Request $anon GET "$base/"
        Check ($root.Status -eq 200 -and $root.Content.Contains("Cisza LAB — $variant")) "$variant : variant label and frontend"
        Check ($root.Headers.Contains("X-Lab-Variant: $variant")) "$variant : variant response header"
        Check ((Request $anon GET "$base/api/notes").Status -eq 401) "$variant : authentication still required"
        $login = Login $anna $base 'anna@example.test' 'Anna!Notes2026'
        Check ($login.Status -eq 200) "$variant : Anna login"
        Check ((Login $piotr $base 'piotr@example.test' 'Piotr!Notes2026').Status -eq 200) "$variant : Piotr login"
        Check (($login.Headers -match '(?i); secure(?:;|\s|$)') -eq $secure) "$variant : Secure cookie matches expected mode"
        Check (($login.Headers -match '(?i); httponly(?:;|\s|$)') -eq $secure) "$variant : HttpOnly cookie matches expected mode"
        $sameSite = if ($secure) { 'strict' } else { 'lax' }
        Check ($login.Headers -match "(?i)samesite=$sameSite") "$variant : SameSite=$sameSite"
        Check (($root.Headers -match 'Content-Security-Policy:') -eq $secure) "$variant : CSP matches expected mode"
        $at = Token $anna $base
        $pt = Token $piotr $base
        $aNotes = @(Json (Request $anna GET "$base/api/notes"))
        $pNotes = @(Json (Request $piotr GET "$base/api/notes"))
        Check ($aNotes.Count -eq 3 -and $pNotes.Count -eq 3) "$variant : reproducible three notes per user (reset first)"
        Check (@($aNotes.id | Where-Object { $_ -in $pNotes.id }).Count -eq 0) "$variant : normal lists scoped to owner"
        $snapshot[$variant] = @($aNotes + $pNotes | Sort-Object id | Select-Object id,title,content,createdAt) | ConvertTo-Json -Depth 5 -Compress
        $normal = @(Json (Request $anna GET ("$base/api/notes?q=" + [Uri]::EscapeDataString('ŻÓŁĆ'))))
        Check ($normal.Count -eq 1) "$variant : Unicode and case insensitive normal search"
        $result = Request $anna GET ("$base/api/notes?q=" + [Uri]::EscapeDataString($injection))
        Check ($result.Status -eq 200) "$variant : SQLi request completed"
        $found = @(Json $result)
        if ($secure) {
            Check ($found.Count -eq 0) 'Secured : SQLi is treated as literal text'
        } else {
            Check ($found.Count -eq 6 -and $piotrId -in $found.id) 'Vulnerable : SQLi returns notes of both users'
        }
        $read = Request $anna GET "$base/api/notes/$piotrId"
        Check ($read.Status -eq $(if($secure){404}else{200})) "$variant : IDOR read expected status"
        if (!$secure) { Check ($read.Content.Contains('PIOTR_PRIVATE_MARKER')) 'Vulnerable : foreign private content actually disclosed' }
        $foreign = Request $anna PUT "$base/api/notes/$piotrId" @{title='IDOR lab changed';content='LAB_IDOR_EDIT'} $at
        Check ($foreign.Status -eq $(if($secure){404}else{200})) "$variant : IDOR edit expected status"
        $observed = Json (Request $piotr GET "$base/api/notes/$piotrId")
        Check ($(if($secure){$observed.content.Contains('PIOTR_PRIVATE_MARKER')}else{$observed.content -eq 'LAB_IDOR_EDIT'})) "$variant : foreign edit outcome persisted"
        # Restore seeded note as its legitimate owner, preserving a repeatable lab snapshot.
        $original = $pNotes | Where-Object id -eq $piotrId
        $restored = Request $piotr PUT "$base/api/notes/$piotrId" @{title=$original.title;content=$original.content} $pt
        Check ($restored.Status -eq 200) "$variant : legitimate owner can edit"
        $temp = Request $piotr POST "$base/api/notes" @{title='Disposable IDOR deletion test';content='test-only'} $pt
        $tempId = (Json $temp).id
        $deleteForeign = Request $anna DELETE "$base/api/notes/$tempId" $null $at
        Check ($deleteForeign.Status -eq $(if($secure){404}else{204})) "$variant : IDOR delete expected status"
        Check ((Request $piotr GET "$base/api/notes/$tempId").Status -eq $(if($secure){200}else{404})) "$variant : foreign delete actual result"
        if ($secure) { $null = Request $piotr DELETE "$base/api/notes/$tempId" $null $pt }
        $inputNote = @{title='CSRF lab probe';content='created without anti-forgery token'}
        $noCsrf = Request $anna POST "$base/api/notes" $inputNote
        Check ($noCsrf.Status -eq $(if($secure){400}else{201})) "$variant : missing CSRF expected status"
        if (!$secure) { $null = Request $anna DELETE ("$base/api/notes/" + (Json $noCsrf).id) }
        $badCsrf = Request $anna POST "$base/api/notes" $inputNote 'invalid-token'
        Check ($badCsrf.Status -eq $(if($secure){400}else{201})) "$variant : invalid CSRF expected status"
        if (!$secure) { $null = Request $anna DELETE ("$base/api/notes/" + (Json $badCsrf).id) }
        $create = Request $anna POST "$base/api/notes" @{title='Own CRUD test';content='<b>plain data</b>'} $at
        Check ($create.Status -eq 201) "$variant : legitimate own create"
        $ownId = (Json $create).id
        Check ((Request $anna PUT "$base/api/notes/$ownId" @{title='Changed';content='saved'} $at).Status -eq 200) "$variant : legitimate own update"
        Check ((Json (Request $anna GET "$base/api/notes/$ownId")).content -eq 'saved') "$variant : legitimate own read after update"
        Check ((Request $anna DELETE "$base/api/notes/$ownId" $null $at).Status -eq 204) "$variant : legitimate own delete"
        Check ((Request $anna GET "$base/api/notes/$ownId").Status -eq 404) "$variant : legitimate deletion persisted"
        $xss = Json (Request $anna GET "$base/api/notes/10000000-0000-0000-0000-000000000003")
        Check ($xss.content.Contains("onerror=") -and $xss.content.Contains('XSS WYKONANY')) "$variant : identical stored XSS fixture (execution tested in browser)"
        if ($secure -and $sentinel) {
            Check ((Request $anna GET "$base/api/notes/$sentinel").Status -eq 404) 'Independent databases : vulnerable-only note absent in secured DB'
        }
        if (!$secure) {
            $probe = Request $anna POST "$base/api/notes" @{title='DB isolation sentinel';content='vulnerable database only'} $at
            $sentinel = (Json $probe).id
            $sentinelClient = $anna
        }
        Check ((Request $anna POST "$base/api/auth/logout" $null $at).Status -eq 204) "$variant : logout"
        Check ((Request $anna GET "$base/api/notes").Status -eq 401) "$variant : logged-out session rejected"
    }
    Check ($snapshot.Vulnerable -eq $snapshot.Secured) 'Both snapshots have identical IDs, creation dates, titles and content'
    $transport = New-Client
    $redirect = Request $transport GET 'http://127.0.0.1:5182/'
    Check ($redirect.Status -eq 307 -and $redirect.Headers.Contains('https://127.0.0.1:7182/')) 'Secured HTTP entry redirects to HTTPS'
    Check ((Request $transport POST 'http://127.0.0.1:5182/api/auth/login' @{email='anna@example.test';password='Anna!Notes2026'}).Status -eq 307) 'HTTP login does not create a session; redirected before authentication'
    Check ((Request $transport GET "$SecuredUrl/").Status -eq 200) 'TLS handshake and OS certificate validation succeed (no certificate bypass)'
    Write-Output "All $script:passed laboratory API assertions passed. Reset datasets after tests to restore original timestamps."
} finally {
    if ($sentinel) {
        $null = Login $sentinelClient $VulnerableUrl 'anna@example.test' 'Anna!Notes2026'
        $sentinelToken = Token $sentinelClient $VulnerableUrl
        $null = Request $sentinelClient DELETE "$VulnerableUrl/api/notes/$sentinel" $null $sentinelToken
    }
    foreach ($client in $clients) { $client.Dispose() }
}
