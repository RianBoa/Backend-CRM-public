using System.Text;
using backend.Auth;
using backend.Infrastructure;
using backend.Infrastructure.Messaging;
using backend.Features;
using backend.Middleware;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.OpenApi.Models;

if (File.Exists(".env"))
{
    Env.Load();
}
var builder = WebApplication.CreateBuilder(args);
var isMigrationRun = args.Contains("--migrate");
var isBootstrapRun = args.Contains("--bootstrap-admin");

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();


// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddBackendFeatures();
builder.Services.AddBotMessaging(builder.Configuration, runWorkers: !isMigrationRun && !isBootstrapRun);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

var configuredJwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var jwtOptions = new JwtOptions
{
    Issuer = string.IsNullOrWhiteSpace(configuredJwtOptions.Issuer)
        ? (builder.Configuration["JWT_ISSUER"] ?? "ffa-backend")
        : configuredJwtOptions.Issuer,
    Audience = string.IsNullOrWhiteSpace(configuredJwtOptions.Audience)
        ? (builder.Configuration["JWT_AUDIENCE"] ?? "ffa-tooljet")
        : configuredJwtOptions.Audience,
    SigningKey = string.IsNullOrWhiteSpace(configuredJwtOptions.SigningKey)
        ? (string.IsNullOrWhiteSpace(builder.Configuration["JWT_SIGNING_KEY"])
        ? "migration-placeholder-signing-key-change-me-immediately"
        : builder.Configuration["JWT_SIGNING_KEY"]!)
        : configuredJwtOptions.SigningKey,
    AccessTokenMinutes = configuredJwtOptions.AccessTokenMinutes <= 0 ? 15 : Math.Min(configuredJwtOptions.AccessTokenMinutes, 60)
};

if (!isMigrationRun && !isBootstrapRun &&
    string.IsNullOrWhiteSpace(configuredJwtOptions.SigningKey) &&
    string.IsNullOrWhiteSpace(builder.Configuration["JWT_SIGNING_KEY"]))
{
    throw new InvalidOperationException("JWT signing key is not configured. Set Jwt__SigningKey.");
}
if (!isMigrationRun && !isBootstrapRun && Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
    throw new InvalidOperationException("JWT signing key must contain at least 32 bytes.");

builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));
builder.Services.AddSingleton<IUserPasswordService, UserPasswordService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

builder.Services.AddBackendSecurity(jwtOptions);

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT bearer token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (isBootstrapRun)
{
    using var scope = app.Services.CreateScope();
    await AdminBootstrap.CreateAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
        scope.ServiceProvider.GetRequiredService<IUserPasswordService>(), builder.Configuration);
    return;
}

if (isMigrationRun)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await db.Database.MigrateAsync();

    return;
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_HTTPS_PORT"]) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_HTTPS_PORTS"]))
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<RequestLogMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
