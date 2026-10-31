# ---------------------------------------------------------------------------
# Sal's CaterSuite background worker - multi-stage build.
#
# Build context is src/backend (see docker-compose.yml). The worker depends on
# Infrastructure and Domain projects, so restore/publish the worker project and
# its project references together.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["SalsCaterSuite.Worker/SalsCaterSuite.Worker.csproj", "SalsCaterSuite.Worker/"]
COPY ["SalsCaterSuite.Infrastructure/SalsCaterSuite.Infrastructure.csproj", "SalsCaterSuite.Infrastructure/"]
COPY ["SalsCaterSuite.Application/SalsCaterSuite.Application.csproj", "SalsCaterSuite.Application/"]
COPY ["SalsCaterSuite.Domain/SalsCaterSuite.Domain.csproj", "SalsCaterSuite.Domain/"]
RUN dotnet restore "SalsCaterSuite.Worker/SalsCaterSuite.Worker.csproj"

COPY . .
RUN dotnet publish "SalsCaterSuite.Worker/SalsCaterSuite.Worker.csproj" \
	-c Release \
	-o /app/publish \
	/p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SalsCaterSuite.Worker.dll"]
