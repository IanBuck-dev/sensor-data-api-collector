FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /service

# Copy csproj file.
COPY service/*.csproj .
# Restore as distinct layers
RUN dotnet restore
# Copy everything
COPY service .
# Build and publish a release
RUN dotnet publish -c Release -o /publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:7.0.2
WORKDIR /publish
COPY --from=build-env /publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "SensorData.Api.Collector.dll"]