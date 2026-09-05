# ================================
# Build
# ================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files first for better layer caching
COPY iptv.Api/iptv.Api.csproj iptv.Api/
COPY iptv.Domain/iptv.Domain.csproj iptv.Domain/
COPY iptv.Services/iptv.Services.csproj iptv.Services/
COPY Utilities/Utilities.csproj Utilities/

RUN dotnet restore iptv.Api/iptv.Api.csproj

# Copy source
COPY . .

WORKDIR /src/iptv.Api

RUN dotnet publish \
    -c Release \
    -o /app/publish \
    --no-restore


# ================================
# Runtime
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "iptv.Api.dll"]