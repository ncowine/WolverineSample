using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradingAssistant.Api.Middleware;
using TradingAssistant.Api.Services;
using TradingAssistant.Application.Services;
using TradingAssistant.Infrastructure.Caching;
using TradingAssistant.Infrastructure.Persistence;
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

// Register 3 DbContexts
builder.Services.AddDbContext<MarketDataDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MarketDataDb")));

builder.Services.AddDbContext<TradingDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("TradingDb"));
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

builder.Services.AddDbContext<BacktestDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BacktestDb")));

// Register caches as singletons (DataCache starts background tasks in constructor)
builder.Services.AddSingleton<StockPriceCache>();
builder.Services.AddSingleton<BacktestResultCache>();
builder.Services.AddSingleton<PortfolioCache>();

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/problem+json";

                var detail = string.IsNullOrEmpty(context.ErrorDescription)
                    ? "Authentication is required to access this resource."
                    : context.ErrorDescription;

                var problem = new
                {
                    type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    title = "Unauthorized",
                    status = 401,
                    detail
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
            }
        };
    });
builder.Services.AddAuthorization();

// Register ICurrentUser for extracting user identity from JWT claims
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Register audit interceptor
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// Configure Wolverine
builder.Host.UseWolverine(opts =>
{
    opts.UseFluentValidation();

    opts.Discovery.IncludeAssembly(typeof(TradingAssistant.Application.Handlers.MarketData.SeedMarketDataHandler).Assembly);
});

builder.Services.AddWolverineHttp();

var app = builder.Build();

// Apply migrations and seed data in Development
await TradingAssistant.Api.DatabaseInitializer.InitializeAsync(app);

app.UseMiddleware<TradingAssistant.Api.Middleware.ValidationExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapWolverineEndpoints();

app.Run();
