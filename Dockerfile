FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DynamicFlightStorageSimulation/", "DynamicFlightStorageSimulation/"]
COPY ["DynamicFlightStorageDTOs/", "DynamicFlightStorageDTOs/"]
COPY ["EventDataStores/", "EventDataStores/"]
COPY ["DynamicFlightStorageUI/", "DynamicFlightStorageUI/"]

WORKDIR "/src/DynamicFlightStorageUI/"
RUN dotnet publish -c Release -o /app 

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "DynamicFlightStorageUI.dll"]