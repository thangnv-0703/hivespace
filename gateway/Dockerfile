# Build context: repository root. Builds both SPAs and serves them from wwwroot, one host each (storefront at the root, seller centre in wwwroot/seller; ADR-0027).
#   docker build -f gateway/Dockerfile .
FROM node:24-slim AS web
RUN corepack enable
WORKDIR /src/web
COPY web ./
COPY docs/tokens.json /src/docs/tokens.json
COPY contracts/openapi /src/contracts/openapi
RUN pnpm install --frozen-lockfile && pnpm turbo run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apphost/HiveSpace.ServiceDefaults ./apphost/HiveSpace.ServiceDefaults
COPY libs/dotnet ./libs/dotnet
COPY contracts/errors ./contracts/errors
COPY gateway/src ./gateway/src
RUN dotnet publish gateway/src/Gateway -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
COPY --from=web /src/web/apps/storefront/dist ./wwwroot
COPY --from=web /src/web/apps/seller-centre/dist ./wwwroot/seller
USER app
ENTRYPOINT ["dotnet", "Gateway.dll"]
