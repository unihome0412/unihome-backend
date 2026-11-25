$ErrorActionPreference = "Continue"
$base = "https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io"
$login = Invoke-RestMethod -Method Post -Uri "$base/api/v1/auth/login" -ContentType "application/json" `
    -Body '{"correo":"unihome0412@gmail.com","contrasena":"uniHome0412"}'
$h = @{ Authorization = "Bearer $($login.accessToken)" }
foreach ($p in "/api/v1/conversaciones", "/api/v1/notificaciones", "/api/v1/mis-propiedades") {
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri "$base$p" -Headers $h
        Write-Host "[$($r.StatusCode)] $p -> $($r.Content)"
    } catch {
        Write-Host "[$($_.Exception.Response.StatusCode.value__)] $p -> $($_.ErrorDetails.Message)"
    }
}
