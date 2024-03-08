FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
RUN apt-get update -y && apt-get install -y libgdiplus libc6-dev
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ENV SolutionDir /src
WORKDIR /src
COPY . .
# Build Api
WORKDIR "/src/Api.System.Health"
RUN dotnet build Api.System.Health.csproj -c Release -o /app/build


FROM build AS publish
RUN dotnet publish Api.System.Health.csproj --no-restore -c Release -o /app/publish


FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.System.Health.dll"]
