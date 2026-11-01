[CmdletBinding()]
param([Parameter(Mandatory)][string]$PublicUrl)

$ErrorActionPreference = 'Stop'
$base = [Uri]$PublicUrl
if ($base.Scheme -ne 'https' -or $base.Host -notlike '*.trycloudflare.com') { throw 'PublicUrl phải là HTTPS Quick Tunnel trycloudflare.com.' }
$baseText = $base.GetLeftPart([UriPartial]::Authority)
$client = [Net.Http.HttpClient]::new()
try {
    foreach ($path in @('/', '/lib/vue/vue.global.prod.js', '/js/app.js', '/css/phase7.css')) {
        $response = $client.GetAsync("$baseText$path").GetAwaiter().GetResult()
        if ([int]$response.StatusCode -ne 200) { throw "FAIL $path => HTTP $([int]$response.StatusCode)" }
        Write-Host "PASS HTTPS $path => 200"
    }
    $swaggerResponse = $client.GetAsync("$baseText/swagger").GetAwaiter().GetResult()
    if ([int]$swaggerResponse.StatusCode -ne 404) { throw "FAIL Swagger should be disabled in Production; got HTTP $([int]$swaggerResponse.StatusCode)" }
    Write-Host 'PASS Swagger is disabled in Production => 404'
    $healthResponse = $client.GetAsync("$baseText/api/v1/health").GetAwaiter().GetResult()
    $healthBody = $healthResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if ([int]$healthResponse.StatusCode -ne 200 -or ($healthBody | ConvertFrom-Json).status -ne 'Healthy') { throw "FAIL SQL health => HTTP $([int]$healthResponse.StatusCode)" }
    Write-Host 'PASS SQL health endpoint => Healthy'

    $privateResponse = $client.GetAsync("$baseText/api/v1/users/me").GetAwaiter().GetResult()
    if ([int]$privateResponse.StatusCode -ne 401) { throw "FAIL protected API expected 401, got $([int]$privateResponse.StatusCode)" }
    Write-Host 'PASS protected profile endpoint rejects anonymous request => 401'

    $hubRequest = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, "$baseText/hubs/game/negotiate?negotiateVersion=1")
    $hubResponse = $client.SendAsync($hubRequest).GetAwaiter().GetResult()
    if ([int]$hubResponse.StatusCode -ne 401) { throw "FAIL protected SignalR negotiate expected 401, got $([int]$hubResponse.StatusCode)" }
    Write-Host 'PASS SignalR negotiate is reachable and requires authentication => 401'
    Write-Warning 'HTTP checks do not prove an authenticated WebSocket session or two-client game sync; test those in a browser.'
} finally { $client.Dispose() }
