FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Api/Api.csproj", "NodeManager.Api/"]
COPY ["Application/Application.csproj", "NodeManager.Application/"]
COPY ["Domain/Domain.csproj", "NodeManager.Domain/"]

COPY . . 

WORKDIR /src/Api 
RUN dotnet restore "Api.csproj"

RUN dotnet build "Api.csproj" -c Release -o /app/build

FROM build AS publish
WORKDIR /src/Api 
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    jq \
    iputils-ping \
    net-tools \
    ufw \
    docker.io \
    sudo && \
    rm -rf /var/lib/apt/lists/*


EXPOSE 80 
EXPOSE 443

ENTRYPOINT ["dotnet", "Api.dll"]