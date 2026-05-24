# syntax=docker/dockerfile:1

# ── Build stage ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiamos todo el repo (incluye los 4 csproj y las referencias entre ellos)
COPY . .

# Restore y publish del proyecto host (server). Esto compila también el cliente
# WebAssembly como parte del proceso de Blazor Web App.
RUN dotnet restore VotifyIU/VotifyIU.csproj
RUN dotnet publish VotifyIU/VotifyIU.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Runtime stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./

# Render rutea desde 443 público al puerto que expongamos aquí.
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000

ENTRYPOINT ["dotnet", "VotifyIU.dll"]
