#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY appsettings.json ../

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["PetFoodManager.Backend.Api/PetFoodManager.Backend.Api.csproj", "PetFoodManager.Backend.Api/"]
RUN dotnet restore "PetFoodManager.Backend.Api/PetFoodManager.Backend.Api.csproj"
COPY . .
WORKDIR "/src/PetFoodManager.Backend.Api"
RUN dotnet build "PetFoodManager.Backend.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PetFoodManager.Backend.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PetFoodManager.Backend.Api.dll"]
