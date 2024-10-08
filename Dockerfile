FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 1633

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "BeeTurbo.sln"
RUN dotnet build "BeeTurbo.sln" -c Release -o /app/build
RUN dotnet test "BeeTurbo.sln" -c Release

FROM build AS publish
RUN dotnet publish "BeeTurbo.sln" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BeeTurbo.dll"]
