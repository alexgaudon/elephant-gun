FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o out

FROM debian:trixie-slim AS runtime

RUN apt-get update && \
    apt-get install -y cowsay fortune libicu-dev ca-certificates && \
    rm -rf /var/lib/apt/lists/*
ENV PATH="/usr/games:${PATH}"

WORKDIR /app

COPY --from=build /app/out ./

ENTRYPOINT ["/app/ElephantGun"] 
