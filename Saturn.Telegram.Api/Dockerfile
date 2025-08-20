FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Saturn.Telegram.Api/Saturn.Telegram.Api.csproj", "Saturn.Telegram.Api/"]
RUN dotnet restore "Saturn.Telegram.Api/Saturn.Telegram.Api.csproj"
COPY . .
WORKDIR "/src/Saturn.Telegram.Api"
RUN dotnet build "./Saturn.Telegram.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Saturn.Telegram.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Saturn.Telegram.Api.dll"]
