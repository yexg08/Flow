using System.Text.Json.Serialization;
using Flow.Api;
using Flow.Api.Auth;
using Flow.Api.Filters;
using Flow.Api.Middleware;
using Flow.Api.Services;
using Flow.Application;
using Flow.Application.Abstractions;
using Flow.Infrastructure;
using Flow.Infrastructure.Auth;
using Flow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// La API solo recibe JSON: 1 MB sobra y frena peticiones gigantes.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// La autorización la aplica la política de respaldo (ver Auth/Policies.cs): denegar por defecto.
builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = TokenService.CreateSigningKey(jwt.Key),
            NameClaimType = AppClaims.Email,
            RoleClaimType = AppClaims.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // Revisa en la base de datos la cuenta y el negocio, y carga el TenantId de la sesión.
        options.Events = new JwtBearerEvents { OnTokenValidated = SessionValidator.OnTokenValidated };
    });
builder.Services.AddAuthorization(Policies.Configure);
builder.Services.AddFlowRateLimits(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSecurityHeaders(app.Environment.IsDevelopment());
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Flow API"));
}
else
{
    // HSTS: el navegador recuerda usar siempre HTTPS con este dominio.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

await DbSeeder.SeedAsync(app.Services);

app.Run();

// Expuesto para pruebas de integración (WebApplicationFactory).
public partial class Program;
