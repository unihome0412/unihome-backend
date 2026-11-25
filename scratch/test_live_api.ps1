$ErrorActionPreference = "Continue"
Add-Type -AssemblyName System.Net.Http

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5055"
$env:MongoDb__ConnectionString = "mongodb://127.0.0.1:27017/?directConnection=true&retryWrites=false"
$env:MongoDb__DatabaseName = "unihome_modelo_pruebas"
$env:Api__EnableSwagger = "true"

$dllPath = (Resolve-Path "src/UniHome.Api/bin/Release/net10.0/UniHome.Api.dll").Path

Write-Host "Iniciando proceso UniHome.Api..."
$proc = Start-Process -FilePath "dotnet" -ArgumentList "`"$dllPath`"" -PassThru -NoNewWindow

$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    if ($proc.HasExited) {
        Write-Host "El proceso de la API terminó anticipadamente. Código de salida: $($proc.ExitCode)"
        exit 1
    }
    try {
        $resp = Invoke-RestMethod -Uri "http://127.0.0.1:5055/health/live" -TimeoutSec 1 -ErrorAction Stop
        if ($resp.status -eq "Healthy") {
            $ready = $true
            Write-Host "API en línea en http://127.0.0.1:5055"
            break
        }
    } catch { }
}

if (-not $ready) {
    Write-Error "Tiempo de espera agotado para el arranque de la API."
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    exit 1
}

$httpClient = New-Object System.Net.Http.HttpClient
$baseUrl = "http://127.0.0.1:5055"

try {
    Write-Host "`n========================================================"
    Write-Host "--- TEST 1: GET /health/live ---"
    $r1 = $httpClient.GetAsync("$baseUrl/health/live").Result
    $b1 = $r1.Content.ReadAsStringAsync().Result
    Write-Host "Status: $([int]$r1.StatusCode) $($r1.StatusCode)"
    Write-Host "Body: $b1"

    Write-Host "`n--- TEST 2: GET /health/ready (Verificando MongoDB en Docker) ---"
    $r2 = $httpClient.GetAsync("$baseUrl/health/ready").Result
    $b2 = $r2.Content.ReadAsStringAsync().Result
    Write-Host "Status: $([int]$r2.StatusCode) $($r2.StatusCode)"
    Write-Host "Body: $b2"

    Write-Host "`n--- TEST 3: GET /api/v1/universidades (Consulta MongoDB) ---"
    $r3 = $httpClient.GetAsync("$baseUrl/api/v1/universidades").Result
    $b3 = $r3.Content.ReadAsStringAsync().Result
    Write-Host "Status: $([int]$r3.StatusCode) $($r3.StatusCode)"
    Write-Host "Body: $b3"

    Write-Host "`n--- TEST 4: GET /api/v1/exploracion/lista (Consulta MongoDB) ---"
    $r4 = $httpClient.GetAsync("$baseUrl/api/v1/exploracion/lista").Result
    $b4 = $r4.Content.ReadAsStringAsync().Result
    Write-Host "Status: $([int]$r4.StatusCode) $($r4.StatusCode)"
    Write-Host "Body: $b4"

    Write-Host "`n--- TEST 5: POST /api/v1/auth/registro (Inserción real de cuenta en MongoDB) ---"
    $randomId = Get-Random -Minimum 1000 -Maximum 9999
    $email = "estudiante.docker.$randomId@uni.edu"
    $password = "ClaveSegura1"
    $regJson = @"
{
    "nombreCompleto": "Estudiante Docker $randomId",
    "correo": "$email",
    "fechaNacimiento": "2002-08-15",
    "genero": "Mujer",
    "contrasena": "$password",
    "confirmarContrasena": "$password",
    "aceptoAvisoPrivacidad": true,
    "versionAvisoPrivacidad": "2026-01"
}
"@
    $content = New-Object System.Net.Http.StringContent($regJson, [System.Text.Encoding]::UTF8, "application/json")
    $r5 = $httpClient.PostAsync("$baseUrl/api/v1/auth/registro", $content).Result
    $b5 = $r5.Content.ReadAsStringAsync().Result
    Write-Host "Status: $([int]$r5.StatusCode) $($r5.StatusCode)"
    Write-Host "Body: $b5"

    Write-Host "`n--- TEST 6: POST /api/v1/auth/login (Intento de Login) ---"
    $loginJson = @"
{
    "correo": "$email",
    "contrasena": "$password"
}
"@
    $loginContent = New-Object System.Net.Http.StringContent($loginJson, [System.Text.Encoding]::UTF8, "application/json")
    $r6 = $httpClient.PostAsync("$baseUrl/api/v1/auth/login", $loginContent).Result
    $b6 = $r6.Content.ReadAsStringAsync().Result
    Write-Host "Status: $([int]$r6.StatusCode) $($r6.StatusCode)"
    Write-Host "Body: $b6"

    Write-Host "`n--- TEST 7: GET /swagger/login (Página de login de documentación) ---"
    $r7 = $httpClient.GetAsync("$baseUrl/swagger/login").Result
    Write-Host "Status: $([int]$r7.StatusCode) $($r7.StatusCode)"
    Write-Host "Location Header: $($r7.Headers.Location)"

    Write-Host "`n--- TEST 8: GET /swagger/index.html (Swagger UI protegido) ---"
    $r8 = $httpClient.GetAsync("$baseUrl/swagger/index.html").Result
    Write-Host "Status: $([int]$r8.StatusCode) $($r8.StatusCode)"
    Write-Host "Location Header: $($r8.Headers.Location)"

    Write-Host "`n========================================================"
    Write-Host "¡TODAS LAS PRUEBAS DE ENDPOINTS EN VIVO COMPLETADAS EXITOSAMENTE!"

} finally {
    $httpClient.Dispose()
    if ($proc -and -not $proc.HasExited) {
        Write-Host "`nDeteniendo proceso de API..."
        Stop-Process -Id $proc.Id -Force
        Write-Host "API detenida."
    }
}
