FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["GatewayService/GatewayService.csproj", "GatewayService/"]
RUN dotnet restore "GatewayService/GatewayService.csproj"

COPY ["GatewayService/", "GatewayService/"]
WORKDIR /src/GatewayService
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GatewayService.dll"]
