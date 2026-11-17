# Build context: repository root. Example:
#   docker build -f services/order/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apphost/HiveSpace.ServiceDefaults ./apphost/HiveSpace.ServiceDefaults
COPY libs/dotnet ./libs/dotnet
COPY contracts ./contracts
COPY services/order/src ./services/order/src
RUN dotnet publish services/order/src/Order.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
USER app
ENTRYPOINT ["dotnet", "Order.Api.dll"]
