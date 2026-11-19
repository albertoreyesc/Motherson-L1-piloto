FROM node:22-bookworm-slim AS frontend
WORKDIR /src/portal/frontend
COPY portal/frontend/package*.json ./
RUN npm ci
COPY portal/frontend/ ./
RUN npm test && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY portal/backend/Motherson.Api/ portal/backend/Motherson.Api/
COPY --from=frontend /src/portal/backend/Motherson.Api/wwwroot/ portal/backend/Motherson.Api/wwwroot/
RUN dotnet publish portal/backend/Motherson.Api/Motherson.Api.csproj -c Release -o /app --no-self-contained
COPY portal/config/ /app/config/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends nginx && rm -rf /var/lib/apt/lists/* /etc/nginx/sites-enabled/default
WORKDIR /app
COPY --from=build /app/ ./
COPY despliegue/render/iniciar.sh /app/iniciar.sh
RUN chmod 755 /app/iniciar.sh && chown -R app:app /app /var/lib/nginx /var/log/nginx /etc/nginx && mkdir -p /tmp/motherson-piloto && chown app:app /tmp/motherson-piloto
USER app
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:8081 MVP_ROOT=/tmp/motherson-piloto Pilot__SqlPersistence=true Pilot__LoopbackProxy=true
EXPOSE 10000
ENTRYPOINT ["/app/iniciar.sh"]
