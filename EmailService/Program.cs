using EmailService.Configurations;
using EmailService.Infrastructure;
using EmailService.Models;
using EmailService.Services;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

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
    }

    // Serilog
    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddControllers();

    // Register Email Configuration
    builder.Services.Configure<EmailConfiguration>(builder.Configuration.GetSection("EmailConfiguration"));

    // Register Email Service
    builder.Services.AddTransient<IEmailSenderService, EmailSenderService>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "EmailService terminó inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
