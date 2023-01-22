FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
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
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /publish
COPY --from=build-env /publish .

# Set environment variables (unsafe because in git but for testing)
ENV NETATMO_ACCESS_TOKEN="63b5e4a909ab07d60d015714|f92d7278f92318cba51628af8e44c78f"
ENV NETATMO_REFRESH_TOKEN="63b5e4a909ab07d60d015714|1c6d6e56cdb48ac84fca69b4dae8a638"
ENV NETATMO_CLIENT_ID="63b5e6511e8a6f161b0a7a01"
ENV NETATMO_CLIENT_SECRET="cyR7ozStxBq0VbytPDUPC9AI8uQ"
ENV SENSOR_MONGODB_PW="NTyuMmwwptmn6xdqXf_J"

EXPOSE 80
ENTRYPOINT ["dotnet", "SensorData.Api.Collector.dll"]