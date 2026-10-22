# Etapa de compilacion
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY global.json ./
COPY backend/src/Services/Access.API/Access.API.csproj backend/src/Services/Access.API/

RUN dotnet restore backend/src/Services/Access.API/Access.API.csproj

COPY backend/ backend/

RUN dotnet publish backend/src/Services/Access.API/Access.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Etapa de ejecucion
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "Access.API.dll"]
