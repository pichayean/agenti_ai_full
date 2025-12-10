# ========== Build stage ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EmailMcpServer.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

# ========== Runtime stage ==========
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:80
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
EXPOSE 80
#HEALTHCHECK --interval=30s --timeout=5s --retries=5 CMD curl -fsS http://127.0.0.1:5053/_health || exit 1
RUN useradd -m appuser
USER appuser
ENTRYPOINT ["dotnet", "EmailMcpServer.dll"]