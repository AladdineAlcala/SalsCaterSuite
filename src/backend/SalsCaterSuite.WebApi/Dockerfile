# ---------------------------------------------------------------------------
# Sal's CaterSuite API - multi-stage build.
#
# Build context is src/backend (see docker-compose.yml). Only the four projects
# the WebApi needs are restored, so a change to the test projects never busts the
# NuGet layer cache.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["SalsCaterSuite.WebApi/SalsCaterSuite.WebApi.csproj", "SalsCaterSuite.WebApi/"]
COPY ["SalsCaterSuite.Application/SalsCaterSuite.Application.csproj", "SalsCaterSuite.Application/"]
COPY ["SalsCaterSuite.Domain/SalsCaterSuite.Domain.csproj", "SalsCaterSuite.Domain/"]
COPY ["SalsCaterSuite.Infrastructure/SalsCaterSuite.Infrastructure.csproj", "SalsCaterSuite.Infrastructure/"]
RUN dotnet restore "SalsCaterSuite.WebApi/SalsCaterSuite.WebApi.csproj"

COPY . .
RUN dotnet publish "SalsCaterSuite.WebApi/SalsCaterSuite.WebApi.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Kestrel listens on plain HTTP inside the container; TLS terminates at the
# edge (nginx). Never set ASPNETCORE_HTTPS_PORT here - UseHttpsRedirection would
# answer every API call with a 307 to a port that does not exist.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

# Serilog's file sink writes logs/ relative to the content root. The directory is
# created and owned by the non-root user so the rolling file sink can open it.
RUN mkdir -p /app/logs && chown -R app:app /app
USER app

ENTRYPOINT ["dotnet", "SalsCaterSuite.WebApi.dll"]
