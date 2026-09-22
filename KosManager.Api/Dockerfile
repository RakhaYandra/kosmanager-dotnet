FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY KosManager.Api/*.csproj KosManager.Api/
COPY KosManager.Application/*.csproj KosManager.Application/
COPY KosManager.Domain/*.csproj KosManager.Domain/
COPY KosManager.Infrastructure/*.csproj KosManager.Infrastructure/
RUN dotnet restore KosManager.Api/KosManager.Api.csproj
COPY . .
RUN dotnet publish KosManager.Api/KosManager.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8090
ENTRYPOINT ["dotnet", "KosManager.Api.dll", "--urls", "http://+:8090"]
