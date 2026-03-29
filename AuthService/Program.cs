using System.Text;
using AuthService.Auth;
using AuthService.Configurations;
using AuthService.Data;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Microsoft.Extensions.Configuration;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Infisical — carga secretos remotos antes de cualquier otra configuración
    var infisicalOpts = builder.Configuration
        .GetSection(InfisicalOptions.Section)
        .Get<InfisicalOptions>() ?? new();

    if (infisicalOpts.Enabled)
    {
        ((IConfigurationBuilder)builder.Configuration).Add(new InfisicalConfigurationSource(infisicalOpts));
        Log.Information("Infisical habilitado — secretos cargados desde {SiteUrl}", infisicalOpts.SiteUrl);
    }

    // Serilog
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console(outputTemplate:
               "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // Options
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
    builder.Services.Configure<LockoutOptions>(builder.Configuration.GetSection(LockoutOptions.Section));
    builder.Services.Configure<EmailServiceOptions>(builder.Configuration.GetSection(EmailServiceOptions.Section));
    builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));

    // Database
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    // HTTP clients
    builder.Services.AddHttpClient("EmailService");

    // Auth services
    builder.Services.AddScoped<TokenService>();
    builder.Services.AddScoped<PasswordHasher>();
    builder.Services.AddScoped<EmailNotificationService>();
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<RefreshTokenService>();
    builder.Services.AddScoped<AuthenticationService>();

    // Phase 2 services
    builder.Services.AddScoped<ApplicationService>();
    builder.Services.AddScoped<RoleService>();
    builder.Services.AddScoped<PermissionService>();

    // JWT authentication
    var jwtSection = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()!;
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection.Issuer,
                ValidAudience = jwtSection.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSection.SecretKey)),
                ClockSkew = TimeSpan.Zero,
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Kauthen", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

    var app = builder.Build();

    // Apply migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "El servicio falló al iniciar.");
}
finally
{
    Log.CloseAndFlush();
}
