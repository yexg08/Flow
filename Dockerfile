# imagen unica de Flow (la API sirve tambien el frontend compilado)

# etapa 1 (compilar el frontend de Angular)
FROM node:24-alpine AS frontend
WORKDIR /src
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# etapa 2 (compilar y publicar la API)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend
WORKDIR /src
# primero solo los proyectos (asi la restauracion de paquetes queda en cache si el codigo cambia)
COPY backend/src/Flow.Domain/Flow.Domain.csproj Flow.Domain/
COPY backend/src/Flow.Application/Flow.Application.csproj Flow.Application/
COPY backend/src/Flow.Infrastructure/Flow.Infrastructure.csproj Flow.Infrastructure/
COPY backend/src/Flow.Api/Flow.Api.csproj Flow.Api/
RUN dotnet restore Flow.Api/Flow.Api.csproj
COPY backend/src/ ./
RUN dotnet publish Flow.Api/Flow.Api.csproj -c Release --no-restore -o /app

# etapa 3 (imagen final solo con lo necesario para ejecutar)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
# tzdata para las zonas horarias de los negocios (America/Bogota)
RUN apk add --no-cache tzdata
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /src/dist/frontend/browser ./wwwroot

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
# usuario sin privilegios (viene definido en las imagenes de aspnet)
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD wget -qO- http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "Flow.Api.dll"]
