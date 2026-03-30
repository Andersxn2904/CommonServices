using System.Threading.RateLimiting;
using Serilog;
using Yarp.ReverseProxy.Transforms;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console(outputTemplate:
               "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        var seqUrl = ctx.Configuration["Seq:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(seqUrl))
            cfg.WriteTo.Seq(seqUrl);
    });

    // ── CORS ─────────────────────────────────────────────────
    // El gateway es el único punto de entrada público — maneja CORS aquí.
    // Los servicios internos no necesitan configurar CORS.
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            else
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }));

    // ── YARP ─────────────────────────────────────────────────
    // Los transforms eliminan los headers CORS del upstream para evitar duplicados.
    builder.Services
        .AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddTransforms(ctx =>
        {
            ctx.AddResponseTransform(async responseCtx =>
            {
                responseCtx.ProxyResponse?.Headers.Remove("Access-Control-Allow-Origin");
                responseCtx.ProxyResponse?.Headers.Remove("Access-Control-Allow-Methods");
                responseCtx.ProxyResponse?.Headers.Remove("Access-Control-Allow-Headers");
                responseCtx.ProxyResponse?.Headers.Remove("Access-Control-Allow-Credentials");
                responseCtx.ProxyResponse?.Headers.Remove("Access-Control-Expose-Headers");
                await Task.CompletedTask;
            });
        });

    // ── Rate limiting ─────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Política global: 100 req/min por IP real del cliente.
        // CF-Connecting-IP tiene la IP real cuando el tráfico pasa por Cloudflare Tunnel.
        // Si no hay Cloudflare (dev/directo), se usa la IP de la conexión TCP.
        options.AddPolicy("global", context =>
        {
            var ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ip,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
        });
    });

    // ── Health checks ─────────────────────────────────────────
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseRateLimiter();
    app.MapHealthChecks("/health");
    app.MapReverseProxy();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway falló al iniciar.");
}
finally
{
    Log.CloseAndFlush();
}
