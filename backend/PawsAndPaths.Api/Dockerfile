FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/PawsAndPaths.Api/PawsAndPaths.Api.csproj backend/PawsAndPaths.Api/
RUN dotnet restore backend/PawsAndPaths.Api/PawsAndPaths.Api.csproj
COPY backend/PawsAndPaths.Api backend/PawsAndPaths.Api
RUN dotnet publish backend/PawsAndPaths.Api/PawsAndPaths.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY frontend ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "PawsAndPaths.Api.dll"]
