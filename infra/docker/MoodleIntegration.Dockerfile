# Etapa 1: compilar y publicar la aplicacion
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copiar archivos necesarios para restaurar dependencias
COPY global.json ./
COPY backend/src/Services/MoodleIntegration.API/MoodleIntegration.API.csproj backend/src/Services/MoodleIntegration.API/

RUN dotnet restore backend/src/Services/MoodleIntegration.API/MoodleIntegration.API.csproj

# Copiar el codigo del backend
COPY backend/ backend/

# Publicar la aplicacion
RUN dotnet publish backend/src/Services/MoodleIntegration.API/MoodleIntegration.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Etapa 2: ejecutar con ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "MoodleIntegration.API.dll"]
