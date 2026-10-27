# PowerShell 7. -Reset deletes and recreates ONLY the two dedicated lab databases.
param([switch]$Reset)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    docker compose up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Uruchom Docker i ponów inicjalizację.' }
    dotnet dev-certs https --check --trust
    if ($LASTEXITCODE -ne 0) { throw 'Przygotuj zaufany certyfikat localhost: dotnet dev-certs https --trust. Potem ponów inicjalizację.' }
    foreach ($variant in @('Vulnerable','Secured')) {
        Push-Location (Join-Path $PSScriptRoot $variant)
        try {
            dotnet restore
            if ($LASTEXITCODE -ne 0) { throw "Restore: $variant" }
            dotnet tool restore
            if ($LASTEXITCODE -ne 0) { throw "Tool restore: $variant" }
            $initFlag = if ($Reset) { '--reset-lab-data' } else { '--init-db' }
            dotnet run --no-launch-profile -- $initFlag
            if ($LASTEXITCODE -ne 0) { throw "Inicjalizacja nie powiodła się: $variant" }
        } finally { Pop-Location }
    }
    Write-Output 'Gotowe. Uruchom warianty w osobnych terminalach według README.md.'
} finally { Pop-Location }
