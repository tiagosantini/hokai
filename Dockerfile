# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.301-noble-amd64 AS build
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src
COPY . .
RUN dotnet restore hokai.slnx --locked-mode
RUN arch=x64; [ "$TARGETARCH" = "arm64" ] && arch=arm64; \
    dotnet publish src/Hokai/Hokai.csproj \
    -c Release \
    -a $arch \
    -p:Version=$VERSION \
    --use-current-runtime \
    --self-contained true \
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
