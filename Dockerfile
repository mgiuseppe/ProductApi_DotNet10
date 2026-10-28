# syntax=docker/dockerfile:1
# Stages:
#   build ─┬─> migrations-build ─> migrator   (EF migrations bundle image, used by the "migrator" compose service)
#          └─> final                          (API image, the default target)

# ---------- build ----------
# Start from dotnet sdk image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Set the workdir in the container you're building
WORKDIR /src

# Restore first (only csproj files) so the layer is cached until dependencies change
# Copy csproj
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/
# Download nuget dependencies
RUN dotnet restore src/Api/Api.csproj

# Copy code (this layer changes on every code change)
# .dockerignore keeps local build output (**/bin/, **/obj/) and other local files out of the copy
COPY src/ src/
# Compile and put the output in /app/publish without executing restore again (already executed in previous layers)
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false


# ---------- migrations: bundle creation ---------- (SDK image, needs dotnet-ef)
# The app does not apply migrations on startup: an EF bundle is a standalone executable that applies them
FROM build AS migrations-build
RUN dotnet tool install dotnet-ef --version 10.0.12 --tool-path /tools
# Creating the bundle runs Program.cs up to Build(): AddInfrastructure needs non-empty connection strings (never used here)
RUN ConnectionStrings__DbConnection="Server=unused" ConnectionStrings__MongoDb="mongodb://unused/productapi" \
    /tools/dotnet-ef migrations bundle --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj \
    --configuration Release --output /app/efbundle
    
# ---------- migrations: runtime image ---------- (only the bundle + the .NET runtime, produced with --target migrator)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=migrations-build /app/efbundle .
USER $APP_UID
# The bundle builds the app host too: it reads ConnectionStrings__DbConnection / ConnectionStrings__MongoDb from the environment
ENTRYPOINT ["./efbundle"]


# ---------- API image ---------- (last stage: what a plain `docker build .` produces)
# Start from aspnet image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Set as ENV_VAR the port that will be listened by the app inside the container
ENV ASPNETCORE_HTTP_PORTS=8080
# Documentation only: the port is published with -p (in docker run) or ports: (in compose)
EXPOSE 8080

# Copy Compiled output from the first stage
COPY --from=build /app/publish .

# Non-root user provided by the base image (for security reason so that we don't use the root user)
USER $APP_UID

# The command run when the container starts
ENTRYPOINT ["dotnet", "Api.dll"]
