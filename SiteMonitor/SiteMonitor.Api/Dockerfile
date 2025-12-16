# syntax=docker/dockerfile:1.6

FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0 AS build
WORKDIR /src

# Install PowerShell so we can execute Playwright's installation script.
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget \
    apt-transport-https \
    ca-certificates \
    gnupg && \
    wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends powershell && \
    rm -rf /var/lib/apt/lists/*

COPY SiteMonitor.Api.csproj ./
RUN dotnet restore SiteMonitor.Api.csproj

COPY . .

# Publish the API and download Playwright's Chromium engine into a known location.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN dotnet publish SiteMonitor.Api.csproj -c Release -o /app/publish /p:UseAppHost=false && \
    pwsh -NoLogo -NoProfile -File /src/bin/Release/net10.0/playwright.ps1 install chromium && \
    mkdir -p /app/publish/ms-playwright && \
    cp -a /ms-playwright/. /app/publish/ms-playwright/

FROM mcr.microsoft.com/dotnet/nightly/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

# Install the system libraries Playwright's Chromium build needs at runtime.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libasound2t64 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libatspi2.0-0 \
    libcairo2 \
    libcups2 \
    libdbus-1-3 \
    libdrm2 \
    libgbm1 \
    libglib2.0-0 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libpango-1.0-0 \
    libpangocairo-1.0-0 \
    libx11-6 \
    libx11-xcb1 \
    libxcb1 \
    libxcb-dri3-0 \
    libxcomposite1 \
    libxcursor1 \
    libxdamage1 \
    libxfixes3 \
    libxi6 \
    libxkbcommon0 \
    libxrandr2 \
    libxshmfence1 \
    libxss1 \
    libxt6 \
    libxtst6 \
    libglu1-mesa \
    libopengl0 \
    libu2f-udev \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY --from=build /app/publish/ms-playwright /ms-playwright

COPY docker-entrypoint.sh .
RUN chmod +x docker-entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["./docker-entrypoint.sh"]
