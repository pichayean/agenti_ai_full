# EmailMcpServer (MCP + MailKit + Docker)

## Run
```bash
docker compose up --build
```

MCP endpoint: `POST /mcp`  
Health: `GET /_health`  
Root: `GET /`

## Env
- SMTP_HOST=smtp.gmail.com
- SMTP_PORT=587
- SMTP_SECURE=StartTls
- SMTP_USERNAME=lazymarcus005@gmail.com
- SMTP_PASSWORD=ipdk vnrr djqq qcan
- SMTP_FROM=lazymarcus005@gmail.com
- SMTP_FROM_NAME="Email MCP"
- SMTP_MAX_BODY_BYTES=2097152
- SMTP_ONLY_GMAIL=true
