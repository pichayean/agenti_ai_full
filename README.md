# Agentic AI Full Stack

A comprehensive agentic AI system with multiple microservices, including an email MCP server, MSSQL MCP server, and the main Agentic AI application.

## Project Structure

```
agentic_ai_full/
├── AgenticAIV2/          # Main application server (.NET)
├── mcp_email/            # Email MCP Server (.NET)
├── mcp_mssql/            # MSSQL MCP Server (.NET)
├── database/             # SQL Server database setup
├── docker-compose.yml    # Docker Compose configuration
└── README.md             # This file
```

## Services Overview

| Service | Port | Purpose |
|---------|------|---------|
| `agenticai-v2` | 5082 | Main Agentic AI application (HTTP) |
| `email-mcp-v1` | 5055 | Email MCP Server for email operations |
| `mcp-mssql-v1` | 5056 | MSSQL MCP Server for database queries |
| `loan-sql` | 1433 | (Optional) Local MSSQL Server instance |

## Prerequisites

- **Docker** and **Docker Compose** installed
- Environment variables configured (see Configuration section below)
- (Optional) MSSQL Server if running the optional `sqlserver` service

## Quick Start

### 1. Build and Start All Services

```powershell
docker compose up -d
```

This command will:
- Build all Docker images
- Start all services in the background
- Create the `agentic-network` bridge network for inter-service communication

### 2. View Running Services

```powershell
docker ps
```

You should see the following containers running:
- `agenticai-v2`
- `email-mcp-v1`
- `mcp-mssql-v1`

### 3. Access the Application

- **Main Application**: http://localhost:5082
- **Email MCP Server**: http://localhost:5055/mcp
- **MSSQL MCP Server**: http://localhost:5056/mcp

### 4. Stop All Services

```powershell
docker compose down
```

To also remove volumes:
```powershell
docker compose down -v
```

## Configuration

### Environment Variables

The `docker-compose.yml` includes the following environment variables for `agenticai-v2`:

```yaml
ASPNETCORE_ENVIRONMENT: "Production"
USE_OLLAMA: "false"
OpenRouter__ApiKey: sk-or-v1-xxxxx  # Replace with your actual API key
McpServers__0__Endpoint: "http://email-mcp-v1:80/mcp"
McpServers__0__Name: "mail_mcp"
McpServers__0__Enable: true
McpServers__1__Endpoint: "http://mcp-mssql-v1:80/mcp"
McpServers__1__Name: "mssql_mcp"
McpServers__1__Enable: true
```

**Important:** Update the `OpenRouter__ApiKey` with your actual API key before running.

### Volume Mounts

The main application mounts the following directories (read-only):
- `./AgenticAI/prompts` → `/app/prompts`
- `./AgenticAI/config` → `/app/config`
- `./AgenticAI/policy` → `/app/policy`

### Optional: Enable Local MSSQL Server

To run a local SQL Server instance, uncomment the `sqlserver` section in `docker-compose.yml`:

```yaml
sqlserver:
  build:
    context: ./database
    dockerfile: Dockerfile.mssql
  container_name: loan-sql
  environment:
    - ACCEPT_EULA=Y
    - MSSQL_PID=Developer
    - SA_PASSWORD=P@ssw0rd!23
  ports:
    - "1433:1433"
```

Then run:
```powershell
docker compose up -d sqlserver
```

**Default Credentials:**
- Username: `sa`
- Password: `P@ssw0rd!23`
- Port: `1433`

## Common Commands

### View Logs

View logs for all services:
```powershell
docker compose logs -f
```

View logs for a specific service:
```powershell
docker compose logs -f agenticai-v2
```

### Rebuild Services

If you make changes to the code, rebuild the images:
```powershell
docker compose build
```

Or rebuild a specific service:
```powershell
docker compose build agenticai-v2
```

Then restart:
```powershell
docker compose up -d
```

### Stop a Specific Service

```powershell
docker compose stop email-mcp-v1
```

### Restart a Service

```powershell
docker compose restart agenticai-v2
```

### Remove All Containers and Networks

```powershell
docker compose down --remove-orphans
```

## Networking

All services are connected via the `agentic-network` bridge network. This allows services to communicate with each other using their container names as hostnames.

**Example:** From `agenticai-v2`, you can access the email MCP server at `http://email-mcp-v1:80/mcp`

## Troubleshooting

### Port Already in Use

If a port is already in use, modify the port mapping in `docker-compose.yml`:
```yaml
ports:
  - "5082:80"  # Change 5082 to another available port
```

### Container Fails to Start

Check the logs:
```powershell
docker compose logs agenticai-v2
```

### Services Cannot Communicate

Ensure all services are on the same network:
```powershell
docker network inspect agentic-network
```

### API Key Error

Make sure you've set the `OpenRouter__ApiKey` environment variable in `docker-compose.yml` with a valid API key.

## Development

For development without Docker, you can run the projects locally:
1. Install .NET 7+ SDK
2. Update connection strings and configuration in `appsettings.json`
3. Run each project:
   ```powershell
   cd AgenticAIV2
   dotnet run
   ```

## Additional Resources

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [.NET Docker Images](https://hub.docker.com/_/microsoft-dotnet-aspnet)
- [MSSQL Docker Documentation](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker)

---

**Last Updated:** December 2025
