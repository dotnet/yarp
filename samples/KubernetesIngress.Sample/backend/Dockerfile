#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0  AS publish
WORKDIR /src
# We need to install the SDK manually because we might target an unreleased SDK
COPY ["global.json", ""]
COPY ["TFMs.props", "Directory.Build.props"]
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --jsonfile global.json
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT 1

COPY ["samples/KubernetesIngress.Sample/backend", "backend"]

WORKDIR /src/backend
RUN /root/.dotnet/dotnet publish -c Release -o /app/publish -f net10.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "backend.dll"]
