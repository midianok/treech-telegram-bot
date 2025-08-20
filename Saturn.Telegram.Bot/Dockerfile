FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Saturn.Telegram.Bot/Saturn.Telegram.Bot.csproj", "Saturn.Telegram.Bot/"]
RUN dotnet restore "Saturn.Telegram.Bot/Saturn.Telegram.Bot.csproj"
COPY . .
WORKDIR "/src/Saturn.Telegram.Bot"
RUN dotnet build "Saturn.Telegram.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Saturn.Telegram.Bot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Saturn.Telegram.Bot.dll"]
