FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Libraries/Data/Dev.Data.Mongo/Dev.Data.Mongo.csproj", "Dev.Data.Mongo/"]
RUN dotnet restore "Libraries/Data/Dev.Data.Mongo/Dev.Data.Mongo.csproj"
COPY . .
WORKDIR "/src/Dev.Data.Mongo"
RUN dotnet build "Dev.Data.Mongo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Dev.Data.Mongo.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dev.Data.Mongo.dll"]
