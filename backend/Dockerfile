# syntax=docker/dockerfile:1
# API image. Build context: backend/ (docker build backend). The CLI is a local tool and has no image.
# Base images pinned by digest; Dependabot keeps tag and digest up to date.

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src

# Restore first to leverage layer caching; locked mode fails on any drift from packages.lock.json.
COPY Directory.Build.props ./
COPY src/DotNextAI.Api/DotNextAI.Api.csproj src/DotNextAI.Api/packages.lock.json src/DotNextAI.Api/
COPY src/DotNextAI.Application/DotNextAI.Application.csproj src/DotNextAI.Application/packages.lock.json src/DotNextAI.Application/
COPY src/DotNextAI.Infrastructure/DotNextAI.Infrastructure.csproj src/DotNextAI.Infrastructure/packages.lock.json src/DotNextAI.Infrastructure/
RUN dotnet restore src/DotNextAI.Api/DotNextAI.Api.csproj --locked-mode

COPY src/DotNextAI.Api/ src/DotNextAI.Api/
COPY src/DotNextAI.Application/ src/DotNextAI.Application/
COPY src/DotNextAI.Infrastructure/ src/DotNextAI.Infrastructure/
RUN dotnet publish src/DotNextAI.Api/DotNextAI.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS final
WORKDIR /app

# The LiteLLM settings (Llm__Endpoint, Llm__ApiKey, Llm__DefaultModel) come from the environment at run time,
# never from the image.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "DotNextAI.Api.dll"]
