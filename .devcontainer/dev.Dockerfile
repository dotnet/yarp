# Base image: .NET SDK for building and running ASP.NET Core
FROM mcr.microsoft.com/devcontainers/dotnet:8.0-bookworm

# Install common dev tools
RUN apt-get update && apt-get install -y \
    curl \
    git \
    unzip \
    zip \
    iproute2 \
    dnsutils \
    procps \
    && apt-get clean

# Install the .NET SDK for ASP.NET Core 10.0 (preview)
RUN curl -SL --output dotnet-install.sh https://dot.net/v1/dotnet-install.sh && \
    bash dotnet-install.sh --version 10.0.100-preview.5.25265.106 --install-dir /usr/share/dotnet && \
    rm dotnet-install.sh

# Install Node.js for Swagger UI builds or UI dashboards (optional)
RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs