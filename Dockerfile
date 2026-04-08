# ZeroBounce C# SDK — .NET 8 SDK image for tests and NuGet pack/publish.
# Mount the repo at /app (see repo-root docker-compose.yml). Do not rely on COPY for local workflows.
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app
CMD ["dotnet", "test", "ZeroBounceTests/ZeroBounceTests.csproj", "-v", "normal"]
