FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Saturn.Telegram.Service/Saturn.Telegram.Service.csproj", "Saturn.Telegram.Service/"]
RUN dotnet restore "Saturn.Telegram.Service/Saturn.Telegram.Service.csproj"
COPY . .
WORKDIR "/src/Saturn.Telegram.Service"
RUN dotnet build "Saturn.Telegram.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Saturn.Telegram.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Saturn.Telegram.Service.dll"]
