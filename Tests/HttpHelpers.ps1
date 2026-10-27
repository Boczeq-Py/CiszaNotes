# HTTP helpers shared by the laboratory assertions. No certificate bypass.
function New-Client {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(15)
    $clients.Add($client)
    return $client
}
function Check($Condition, $Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:passed++
    Write-Output "PASS: $Name"
}
function Request($Client, $Method, $Url, $Body = $null, $Token = '') {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Url)
    if ($Token) { $request.Headers.Add('X-CSRF-TOKEN', $Token) }
    if ($null -ne $Body) { $request.Content = [System.Net.Http.StringContent]::new((ConvertTo-Json -InputObject $Body -Compress), [Text.Encoding]::UTF8, 'application/json') }
    try {
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        try { return [pscustomobject]@{ Status = [int]$response.StatusCode; Content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); Headers = $response.Headers.ToString() } }
        finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
function Json($Response) { ConvertFrom-Json -InputObject $Response.Content }
function Token($Client, $Base) { (Json (Request $Client GET "$Base/api/auth/csrf")).token }
function Login($Client, $Base, $Email, $Password) {
    $token = Token $Client $Base
    Request $Client POST "$Base/api/auth/login" @{email=$Email;password=$Password} $token
}
