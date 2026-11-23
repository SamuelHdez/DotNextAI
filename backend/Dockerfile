# syntax=docker/dockerfile:1
# API image. Build context: backend/ (docker build backend). The CLI is a local tool and has no image.
# Base images pinned by digest; Dependabot keeps tag and digest up to date.

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4 AS build
WORKDIR /src

# Restore first to leverage layer caching; locked mode fails on any drift from packages.lock.json.
COPY Directory.Build.props ./
COPY src/DotNextAI.Api/DotNextAI.Api.csproj src/DotNextAI.Api/packages.lock.json src/DotNextAI.Api/
COPY src/DotNextAI.Application/DotNextAI.Application.csproj src/DotNextAI.Application/packages.lock.json src/DotNextAI.Application/
COPY src/DotNextAI.Infrastructure/DotNextAI.Infrastructure.csproj src/DotNextAI.Infrastructure/packages.lock.json src/DotNextAI.Infrastructure/
COPY src/DotNextAI.Persistence/DotNextAI.Persistence.csproj src/DotNextAI.Persistence/packages.lock.json src/DotNextAI.Persistence/
RUN dotnet restore src/DotNextAI.Api/DotNextAI.Api.csproj --locked-mode

COPY src/DotNextAI.Api/ src/DotNextAI.Api/
COPY src/DotNextAI.Application/ src/DotNextAI.Application/
COPY src/DotNextAI.Infrastructure/ src/DotNextAI.Infrastructure/
COPY src/DotNextAI.Persistence/ src/DotNextAI.Persistence/
RUN dotnet publish src/DotNextAI.Api/DotNextAI.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS final
WORKDIR /app

# The LiteLLM settings (Llm__Endpoint, Llm__ApiKey, Llm__DefaultModel) come from the environment at run time,
# never from the image. The saved chats live in a SQLite file under /app/data: mount a volume there to keep them.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Chats="Data Source=/app/data/dotnextai.db"
EXPOSE 8080

RUN mkdir /app/data && chown "$APP_UID" /app/data
VOLUME /app/data

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "DotNextAI.Api.dll"]
