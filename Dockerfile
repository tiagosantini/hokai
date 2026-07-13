# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.301-noble-amd64 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore hokai.slnx --locked-mode
RUN dotnet publish src/Hokai/Hokai.csproj \
    -c Release \
    -a ${TARGETARCH/amd64/x64} \
    --use-current-runtime \
    --self-contained true \
    -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.9-noble-chiseled
WORKDIR /app
COPY --from=build /app/hokai /app/

RUN mkdir -p /var/lib/hokai && \
    chmod 755 /var/lib/hokai

ENV DOTNET_ENVIRONMENT=Production
ENV HOKAI_CONFIG_PATH=/etc/hokai/appsettings.json

VOLUME ["/var/lib/hokai"]

EXPOSE 8080

ENTRYPOINT ["/app/hokai"]
CMD ["run"]
