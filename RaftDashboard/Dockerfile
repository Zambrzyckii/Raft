FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RaftDashboard/RaftDashboard.csproj", "RaftDashboard/"]
RUN dotnet restore "RaftDashboard/RaftDashboard.csproj"
COPY RaftDashboard/ RaftDashboard/
COPY RaftServer/Protos/ RaftServer/Protos/

RUN dotnet build "RaftDashboard/RaftDashboard.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RaftDashboard/RaftDashboard.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RaftDashboard.dll"]
