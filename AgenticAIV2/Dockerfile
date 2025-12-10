# ===========================
#        BUILD STAGE
# ===========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the source
COPY . .

# Build the app
RUN dotnet publish -c Release -o /app/publish


# ===========================
#        RUNTIME STAGE
# ===========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Expose port (optional)
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

# Run the app
ENTRYPOINT ["dotnet", "AgenticAI.dll"]
