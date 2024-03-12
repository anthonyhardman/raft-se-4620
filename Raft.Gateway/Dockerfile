FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

COPY ["Raft.Gateway/Raft.Gateway.csproj", "Raft.Gateway/"]
RUN dotnet restore "Raft.Gateway/Raft.Gateway.csproj"

COPY . .
WORKDIR /source/Raft.Gateway
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Raft.Gateway.dll"]