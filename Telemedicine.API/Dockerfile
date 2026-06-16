FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5096

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Telemedicine.API/Telemedicine.API.csproj", "Telemedicine.API/"]
COPY ["Telemedicine.Core/Telemedicine.Core.csproj", "Telemedicine.Core/"]
COPY ["Telemedicine.Infrastructure/Telemedicine.Infrastructure.csproj", "Telemedicine.Infrastructure/"]
RUN dotnet restore "Telemedicine.API/Telemedicine.API.csproj"
COPY . .
WORKDIR "/src/Telemedicine.API"
RUN dotnet build "Telemedicine.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Telemedicine.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Telemedicine.API.dll"]