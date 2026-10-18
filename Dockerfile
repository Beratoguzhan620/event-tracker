FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props EventTracker.slnx ./
COPY src/ src/
RUN dotnet publish src/EventTracker.Web/EventTracker.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Database__Path=/app/data/eventtracker.db \
    DataProtection__KeysPath=/app/data/keys

EXPOSE 8080

COPY --from=build /app/publish .

RUN mkdir -p /app/wwwroot/uploads /app/data/keys \
    && chown -R "$APP_UID":"$APP_UID" /app/wwwroot/uploads /app/data

USER $APP_UID

ENTRYPOINT ["dotnet", "EventTracker.Web.dll"]
