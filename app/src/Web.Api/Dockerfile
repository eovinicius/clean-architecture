# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage restores and publishes the service project (a single publish, no separate build)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
# The analyzer configuration lives in .editorconfig; without it the analyzer-strict build fails.
COPY [".editorconfig", "."]
COPY ["app/src/Web.Api/Web.Api.csproj", "app/src/Web.Api/"]
COPY ["app/src/Infrastructure/Infrastructure.csproj", "app/src/Infrastructure/"]
COPY ["app/src/Application/Application.csproj", "app/src/Application/"]
COPY ["app/src/Domain/Domain.csproj", "app/src/Domain/"]
COPY ["app/src/SharedKernel/SharedKernel.csproj", "app/src/SharedKernel/"]
RUN dotnet restore "./app/src/Web.Api/Web.Api.csproj"
COPY ["app/src/", "app/src/"]
RUN dotnet publish "./app/src/Web.Api/Web.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Web.Api.dll"]
