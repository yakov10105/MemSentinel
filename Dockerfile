FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/MemSentinel.Agent/MemSentinel.Agent.csproj", "MemSentinel.Agent/"]
COPY ["src/MemSentinel.Core/MemSentinel.Core.csproj", "MemSentinel.Core/"]
COPY ["src/MemSentinel.Contracts/MemSentinel.Contracts.csproj", "MemSentinel.Contracts/"]
RUN dotnet restore "MemSentinel.Agent/MemSentinel.Agent.csproj"

COPY src/MemSentinel.Agent/    MemSentinel.Agent/
COPY src/MemSentinel.Core/     MemSentinel.Core/
COPY src/MemSentinel.Contracts/ MemSentinel.Contracts/

RUN dotnet publish "MemSentinel.Agent/MemSentinel.Agent.csproj" \
    -c Release \
    -o /publish \
    --no-restore \
    -r linux-x64 \
    --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S sentinel && adduser -S sentinel -G sentinel

COPY --from=build /publish .

USER sentinel

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "MemSentinel.Agent.dll"]
