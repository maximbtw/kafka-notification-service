FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["NotificationService/NotificationService.csproj", "NotificationService/"]
COPY ["NotificationService.Contracts/NotificationService.Contracts.csproj", "NotificationService.Contracts/"]
COPY ["Utils/Utils.csproj", "Utils/"]

RUN dotnet restore "NotificationService/NotificationService.csproj"

COPY ["NotificationService/", "NotificationService/"]
COPY ["NotificationService.Contracts/", "NotificationService.Contracts/"]
COPY ["Utils/", "Utils/"]

WORKDIR "/src/NotificationService"
RUN dotnet build "NotificationService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NotificationService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN apt-get update && apt-get install -y curl
ENTRYPOINT ["dotnet", "NotificationService.dll"]
