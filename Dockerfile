# ZeroBounce C# SDK – test image (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app

COPY . .

RUN dotnet restore

# Run unit tests (coverage optional)
CMD ["dotnet", "test", "ZeroBounceTests/ZeroBounceTests.csproj", "--no-restore", "-v", "normal"]
