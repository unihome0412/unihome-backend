$ErrorActionPreference = "Continue"
$base = "https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io"
$uni = "bfcb8774-f89c-4dec-b1f9-c8ead14e7145"

function Show($name, $url, $headers) {
    try {
        $r = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing -TimeoutSec 30
        $body = $r.Content
        if ($body.Length -gt 700) { $body = $body.Substring(0, 700) + "..." }
        Write-Host "[$($r.StatusCode)] $name`n  $body"
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "[$code] $name  ERROR: $($_.ErrorDetails.Message)"
    }
}

Show "health/ready" "$base/health/ready" @{}
Show "universidades" "$base/api/v1/universidades" @{}

$login = Invoke-RestMethod -Method Post -Uri "$base/api/v1/auth/login" -ContentType "application/json" `
    -Body '{"correo":"unihome0412@gmail.com","contrasena":"uniHome0412"}'
$token = $login.accessToken
if (-not $token) { $token = $login.tokenAcceso }
Write-Host "Login OK, token len: $($token.Length)"
$h = @{ Authorization = "Bearer $token" }

Show "exploracion/lista (sin token)" "$base/api/v1/exploracion/lista?universidadId=$uni" @{}
Show "exploracion/lista" "$base/api/v1/exploracion/lista?universidadId=$uni" $h
Show "exploracion/mapa" "$base/api/v1/exploracion/mapa?universidadId=$uni" $h
Show "mis-propiedades" "$base/api/v1/mis-propiedades" $h
Show "perfil" "$base/api/v1/perfil" $h
