# syntax=docker/dockerfile:1
# Imagen de UniHome.Api para Azure Container Apps. Build multi-stage: el SDK
# compila y publica; la imagen final solo lleva el runtime ASP.NET y el
# resultado de publish, sin SDK, sin código fuente de pruebas y sin secretos.

FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/UniHome.Api/UniHome.Api.csproj --locked-mode
RUN dotnet publish src/UniHome.Api/UniHome.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS final
WORKDIR /app

# OCR local real para M06 (HU-018). No se usa un servicio externo: el motor y
# el modelo inglés se instalan en la imagen y el adaptador falla a "No
# disponible" si el binario no existe.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

# Imagen base de .NET 10: usuario sin privilegios ya definido, sin crear uno propio.
USER $APP_UID

ENTRYPOINT ["dotnet", "UniHome.Api.dll"]
