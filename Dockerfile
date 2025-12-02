# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (for better caching)
COPY ["SessionManager.Api/SessionManager.Api.csproj", "SessionManager.Api/"]
COPY ["SessionManager.Infrastructure/SessionManager.Infrastructure.csproj", "SessionManager.Infrastructure/"]
COPY ["SessionManager.Application/SessionManager.Application.csproj", "SessionManager.Application/"]
COPY ["SessionManager.Domain/SessionManager.Domain.csproj", "SessionManager.Domain/"]

# Restore dependencies
RUN dotnet restore "SessionManager.Api/SessionManager.Api.csproj"

# Copy the rest of the source code
COPY . .

# Build and Publish
WORKDIR "/src/SessionManager.Api"
RUN dotnet publish "SessionManager.Api.csproj" -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SessionManager.Api.dll"]