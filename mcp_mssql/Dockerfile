# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ถ้ามีโซลูชันรวมหลายโปรเจกต์ แนะนำ COPY .sln ด้วย
# COPY ./MssqlMcpServer.sln ./
COPY ./MssqlMcpServer.csproj ./
RUN dotnet restore ./MssqlMcpServer.csproj

COPY ./ ./
RUN dotnet publish ./MssqlMcpServer.csproj -c Release -o /app/publish

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

# เปิดพอร์ต MCP Server (HTTP+SSE)
# ENV ASPNETCORE_URLS=http://0.0.0.0:8082
# EXPOSE 8082

ENV ASPNETCORE_URLS=http://0.0.0.0:80
EXPOSE 80
# ตั้งค่าเริ่มต้น (แก้ได้ผ่าน env ใน compose)
# ถ้ารันภายใน network เดียวกับ service mssql ให้ชี้ Server=mssql
#ENV MssqlMcpServer__Database__ConnectionString="Server=host.docker.internal,1433;Database=LoanCoreDB;User Id=sa;Password=P@ssw0rd!23;Encrypt=true;TrustServerCertificate=true;"
ENV MssqlMcpServer__Database__ConnectionString="Server=host.docker.internal,1433;Database=LoanDataDB;User Id=sa;Password=P@ssw0rd!23;Encrypt=true;TrustServerCertificate=true;"
ENV MssqlMcpServer__Database__CommandTimeoutSeconds=30

ENTRYPOINT ["dotnet", "MssqlMcpServer.dll"]
