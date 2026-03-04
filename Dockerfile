# ZeroBounce C# SDK – test image (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app

COPY . .

# Restore SDK + Tests only (ZeroBounceSample is .NET Framework WPF; dotnet restore would skip it with NU1503)
RUN dotnet restore ZeroBounceTests/ZeroBounceTests.csproj

# Run unit tests (coverage optional)
CMD ["dotnet", "test", "ZeroBounceTests/ZeroBounceTests.csproj", "--no-restore", "-v", "normal"]
