# ZeroBounce C# SDK — .NET 10 SDK image for tests (the test projects target net10.0).
# Mount the repo at /app (see sdk-docs/docker-compose.yml). Do not rely on COPY for local workflows.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

USER 65532:65532

CMD ["dotnet", "test", "ZeroBounceTests/ZeroBounceTests.csproj", "-v", "normal"]
