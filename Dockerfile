FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN apt-get update && apt-get install -y clang zlib1g-dev
WORKDIR /src
COPY ["src/SabaMemDb/SabaMemDb.csproj", "src/SabaMemDb/"]
RUN dotnet restore "src/SabaMemDb/SabaMemDb.csproj" -a $TARGETARCH
COPY . .
WORKDIR "/src/src/SabaMemDb"
RUN dotnet build "./SabaMemDb.csproj" -c $BUILD_CONFIGURATION -a $TARGETARCH -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN dotnet publish "./SabaMemDb.csproj" -c $BUILD_CONFIGURATION -a $TARGETARCH -o /app/publish /p:PublishAot=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./SabaMemDb"]