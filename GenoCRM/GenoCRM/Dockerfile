FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GenoCRM/GenoCRM/GenoCRM.csproj", "GenoCRM/GenoCRM/"]
COPY ["GenoCRM/GenoCRM.Client/GenoCRM.Client.csproj", "GenoCRM/GenoCRM.Client/"]
RUN dotnet restore "GenoCRM/GenoCRM/GenoCRM.csproj"
COPY . .
WORKDIR "/src/GenoCRM/GenoCRM"
RUN dotnet build "./GenoCRM.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GenoCRM.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GenoCRM.dll"]
