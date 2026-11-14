# ZeroBounce C# SDK — .NET 10 SDK image for tests (the test projects target net10.0).
# Mount the repo at /app (see sdk-docs/docker-compose.yml). Do not rely on COPY for local workflows.
# Pinned by digest (the multi-arch index of sdk:10.0); Dependabot's docker ecosystem keeps it current.
FROM mcr.microsoft.com/dotnet/sdk:11.0@sha256:65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea
WORKDIR /app

# The non-root user needs a writable home for the dotnet CLI and the NuGet cache.
RUN mkdir -p /home/dotnet/.nuget/packages /home/dotnet/.dotnet \
    && chown -R 65532:65532 /home/dotnet
ENV HOME=/home/dotnet \
    DOTNET_CLI_HOME=/home/dotnet \
    NUGET_PACKAGES=/home/dotnet/.nuget/packages \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

USER 65532:65532

CMD ["dotnet", "test", "ZeroBounceTests/ZeroBounceTests.csproj", "-v", "normal"]
