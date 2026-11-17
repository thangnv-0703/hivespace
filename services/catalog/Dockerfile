# Build context: repository root. Example:
#   docker build -f services/catalog/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apphost/HiveSpace.ServiceDefaults ./apphost/HiveSpace.ServiceDefaults
COPY libs/dotnet ./libs/dotnet
COPY contracts ./contracts
COPY services/catalog/src ./services/catalog/src
RUN dotnet publish services/catalog/src/Catalog.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
# The cloud runs migrations in a deploy step, not on start (architecture §8.2).
ENV Database__MigrateOnStartup=false
USER app
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
