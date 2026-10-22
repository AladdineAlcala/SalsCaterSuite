using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SalsCaterSuite.Application;
using SalsCaterSuite.Application.Common.Authorization;
using Serilog;
using SalsCaterSuite.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.WebApi.Logging;
using SalsCaterSuite.WebApi.Middleware;
using SalsCaterSuite.WebApi.Serialization;
using SalsCaterSuite.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ──────────────────────────────────────────────────────────────
// Serilog owns the logging pipeline (DEC-092). UseSerilog on the host builder REPLACES the
// default providers (it calls ClearProviders) and reads the "Serilog" section of the
// configuration, which is the sink list — the "text file or DataDog" switch (BR-23.1, DEC-095).
builder.Host.UseSerilog((context, loggerConfiguration) => SerilogLoggingConfiguration
    .Configure(context.Configuration, context.HostingEnvironment.EnvironmentName, loggerConfiguration));

// The request logger's *behaviour* is configuration, not code (specification §5.2).
builder.Services.Configure<RequestLoggingOptions>(
    builder.Configuration.GetSection(RequestLoggingOptions.SectionName));
builder.Services.AddSingleton<SensitiveDataRedactor>();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        // The login endpoint writes role claims using ClaimTypes.Role; pin the
        // role claim type so [Authorize(Policy = ...)] role checks succeed (DEC-017).
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // SignalR JWT from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var accessToken = ctx.Request.Query["access_token"];
            var path = ctx.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                ctx.Token = accessToken;
            }
            return Task.CompletedTask;
        },

        // Authentication failures return the same { "errors": [ ... ] } contract as every
        // other failure path. Status codes are unchanged: 401 challenge, 403 forbidden.
        OnChallenge = async ctx =>
        {
            // HandleResponse() stops the bearer handler from emitting its own empty
            // response (and WWW-Authenticate header) after this handler returns.
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(new { errors = new[] { "Authentication is required." } }));
        },
        OnForbidden = async ctx =>
        {
            // The bearer handler has already set 403 before invoking this event; only the
            // response body is added here.
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(new { errors = new[] { "You are not authorized to perform this action." } }));
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // Role model and policy names are centralized in the Application layer (DEC-017).
    options.AddPolicy(
        AuthorizationPolicies.BookingsCancel,
        policy => policy.RequireRole(AuthorizationPolicies.BookingsCancelRoles));

    options.AddPolicy(
        AuthorizationPolicies.PaymentsRecord,
        policy => policy.RequireRole(AuthorizationPolicies.PaymentsRecordRoles));
});

// Global mapping: unhandled DomainException → HTTP 400 { "errors": [ ... ] } (DEC-014).
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// The operational failure event (REQ-OPS-01, AC-OPS-02). It is registered AFTER the domain
// exception handler on purpose: IExceptionHandler implementations are invoked in registration
// order and the chain stops at the first one that answers. A DomainException must therefore be
// answered by DomainExceptionHandler — the approved 400 { "errors": [...] } envelope, recorded by
// the request-completion event at the client-error level and by DomainExceptionHandler's own
// Warning — and NOT as a failure event. The request-logging handler therefore observes exactly
// the exceptions the domain handler declines, i.e. genuine faults, and it returns false so that
// the response contract stays exactly as it is (DEC-026, AC-OPS-16).
builder.Services.AddExceptionHandler<RequestLoggingExceptionHandler>();

// Required fallback for app.UseExceptionHandler() with no ExceptionHandlingPath/ExceptionHandler
// configured: any fault not handled by DomainExceptionHandler is rendered as a 500 ProblemDetails
// response (DEC-026). Without this registration the host throws at ApplicationBuilder.Build().
builder.Services.AddProblemDetails();

// HttpContext accessor for CurrentUserService
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SalsCaterSuite.Application.Common.Interfaces.ICurrentUserService, CurrentUserService>();

// JSON contract: enums travel as string names in both directions (approved API contract §8).
builder.Services.AddControllers()
    .AddJsonOptions(options => ApiJsonSerializerOptions.Configure(options.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(options =>
    {
        // The approved error contract (§7) is `{ "errors": [ "..." ] }` for every request
        // validation failure — including the framework's automatic 400 for a malformed or an
        // incomplete request body. The default ProblemDetails body carries an "errors" *object*,
        // which the clients cannot read with the documented array contract, so the automatic
        // response is rendered in the approved shape.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The request payload is invalid."
                        : error.ErrorMessage))
                .ToArray();

            if (errors.Length == 0)
            {
                errors = ["The request payload is invalid."];
            }

            return new BadRequestObjectResult(new { errors });
        };
    });
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sal's CaterSuite API",
        Version = "v1",
        Description = "REST API for Sal's Catering Services management system"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token"
    });

    // Microsoft.OpenApi 2.x (shipped with Swashbuckle 10.x) keys a security requirement on an
    // OpenApiSecuritySchemeReference resolved against the generated document.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// CORS — allow React dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("SalsCaterSuiteUi", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// ─── Database initialization (DEC-018 / DEC-020) ────────────────────────────
// Both flags are false in appsettings.json, so a production host never migrates or seeds on
// startup unless an operator opts in explicitly. Development enables them for the LocalDB
// dev/test database only. Migrations are a reviewed, environment-scoped operation.
var applyMigrationsOnStartup = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false);
var seedOnStartup = app.Configuration.GetValue("Database:SeedOnStartup", false);

if (applyMigrationsOnStartup || seedOnStartup)
{
    using var initializationScope = app.Services.CreateScope();
    var startupLogger = initializationScope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("SalsCaterSuite.Startup");
    var databaseSeeder = initializationScope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

    if (applyMigrationsOnStartup)
    {
        startupLogger.LogInformation(
            "Database:ApplyMigrationsOnStartup is enabled — applying pending migrations.");
        await databaseSeeder.ApplyMigrationsAsync();
    }

    if (seedOnStartup)
    {
        startupLogger.LogInformation(
            "Database:SeedOnStartup is enabled — seeding system-required data (DEC-018).");
        await databaseSeeder.SeedAsync();
    }
}

// ─── Middleware pipeline ────────────────────────────────────────────────────
// The operational request logger is the OUTERMOST middleware (specification §4.4): it observes
// the exception object before the exception handler clears the response, and it wraps the
// handler so the 400 { "errors": [...] } envelope is captured as the response body.
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sal's CaterSuite API v1");
    });
}

app.UseCors("SalsCaterSuiteUi");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

/// <summary>
/// Top-level statements generate an internal <c>Program</c> class. This public partial
/// declaration makes the entry point reachable from <c>WebApplicationFactory&lt;Program&gt;</c>
/// in the integration-test project without changing any startup behaviour.
/// </summary>
public partial class Program { }
