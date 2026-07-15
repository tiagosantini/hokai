# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.301-noble AS build
ARG TARGETARCH
ARG APP_VERSION=0.0.0-dev
WORKDIR /src

RUN apt-get update -qq && \
    apt-get install -y -qq --no-install-recommends \
      clang \
      zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

COPY . .
RUN dotnet restore hokai.slnx --locked-mode
RUN arch=x64; [ "$TARGETARCH" = "arm64" ] && arch=arm64; \
    dotnet publish src/Hokai/Hokai.csproj \
    -c Release \
    -r "linux-${arch}" \
    -p:Version=$APP_VERSION \
    -p:PublishAot=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=full \
    --self-contained \
    -warnaserror \
    -o /app
RUN mkdir -p /data && chown 1000:1000 /data

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.9-noble-chiseled
WORKDIR /app
COPY --from=build --chown=1000:1000 /data /var/lib/hokai
COPY --from=build /app/hokai /app/

ENV DOTNET_ENVIRONMENT=Production
ENV HOKAI_CONFIG_PATH=/etc/hokai/appsettings.json

VOLUME ["/var/lib/hokai"]

USER 1000

ENTRYPOINT ["/app/hokai"]
CMD ["run"]
