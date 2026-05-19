# See here for image contents: https://github.com/devcontainers/images/tree/main/src/dotnet
#
# We intentionally do not pin a specific .NET SDK version here. The repository's
# restore.sh / build.sh scripts will install the exact SDK pinned in global.json
# into a repo-local `.dotnet` directory, and devcontainer.json prepends that
# directory to PATH. This matches the pattern used by dotnet/aspnetcore and
# dotnet/runtime.

ARG VARIANT="8.0-noble"
FROM mcr.microsoft.com/devcontainers/dotnet:${VARIANT}

SHELL [ "/bin/bash", "-c" ]

# Common dev tools used by the build scripts and by contributors.
RUN apt-get update \
    && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends \
        curl \
        git \
        unzip \
        zip \
        procps \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
