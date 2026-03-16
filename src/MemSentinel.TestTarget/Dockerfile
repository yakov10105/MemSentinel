FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/MemSentinel.TestTarget/MemSentinel.TestTarget.csproj", "MemSentinel.TestTarget/"]
RUN dotnet restore "MemSentinel.TestTarget/MemSentinel.TestTarget.csproj" -r linux-x64

COPY src/MemSentinel.TestTarget/ MemSentinel.TestTarget/

RUN dotnet publish "MemSentinel.TestTarget/MemSentinel.TestTarget.csproj" \
    -c Release \
    -o /publish \
    --no-restore \
    -r linux-x64 \
    --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

COPY --from=build /publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "MemSentinel.TestTarget.dll"]
