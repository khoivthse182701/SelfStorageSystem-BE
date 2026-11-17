# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first for better layer caching
COPY BE/SelfStorageSystem.Domain/SelfStorageSystem.Domain.csproj BE/SelfStorageSystem.Domain/
COPY BE/SelfStorageSystem.Contracts/SelfStorageSystem.Contracts.csproj BE/SelfStorageSystem.Contracts/
COPY BE/SelfStorageSystem.Application/SelfStorageSystem.Application.csproj BE/SelfStorageSystem.Application/
COPY BE/SelfStorageSystem.Infrastructure/SelfStorageSystem.Infrastructure.csproj BE/SelfStorageSystem.Infrastructure/
COPY BE/SelfStorageSystem/SelfStorageSystem.csproj BE/SelfStorageSystem/

RUN dotnet restore BE/SelfStorageSystem/SelfStorageSystem.csproj

# Copy the rest of the backend source code
COPY BE/ BE/

RUN dotnet publish BE/SelfStorageSystem/SelfStorageSystem.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Ensure appsettings.json exists so AddJsonFile("appsettings.json", optional: false) never crashes
# Real secrets (ConnectionStrings, JwtSettings, CorsSettings) are injected via Environment Variables
RUN [ -f appsettings.json ] || echo '{}' > appsettings.json

# Support dynamic PORT from Render/Railway/Cloud Run (defaults to 8080)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet SelfStorageSystem.dll"]

