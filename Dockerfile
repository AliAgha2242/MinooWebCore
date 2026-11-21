# Build from the repository root:
# docker compose up -d --build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to improve NuGet restore layer caching.
COPY ["src/MinooWebCore/MinooWebCore.csproj", "src/MinooWebCore/"]
COPY ["MinooWebCore.Data/MinooWebCore.Data.csproj", "MinooWebCore.Data/"]
COPY ["MinooWebCore.Application/MinooWebCore.Application.csproj", "MinooWebCore.Application/"]
COPY ["MinooWebCore.Infrastructure/MinooWebCore.Infrastructure.csproj", "MinooWebCore.Infrastructure/"]
COPY ["MinooWebCore.Persistense/MinooWebCore.Persistense.csproj", "MinooWebCore.Persistense/"]

RUN dotnet restore "src/MinooWebCore/MinooWebCore.csproj"

COPY . .
WORKDIR /src/src/MinooWebCore
RUN dotnet publish "MinooWebCore.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

# The application writes daily logs under ContentRootPath/Logs.
RUN mkdir -p /app/Logs && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

ENTRYPOINT ["dotnet", "MinooWebCore.dll"]
