using EmailService.Configurations;
using EmailService.Infrastructure;
using EmailService.Models;
using EmailService.Services;

var builder = WebApplication.CreateBuilder(args);

// Infisical — carga secretos remotos antes de cualquier otra configuración
var infisicalOpts = builder.Configuration
    .GetSection(InfisicalOptions.Section)
    .Get<InfisicalOptions>() ?? new();

if (infisicalOpts.Enabled)
{
    ((IConfigurationBuilder)builder.Configuration).Add(new InfisicalConfigurationSource(infisicalOpts));
}

builder.Services.AddControllers();

// Register Email Configuration
builder.Services.Configure<EmailConfiguration>(builder.Configuration.GetSection("EmailConfiguration"));

// Register Email Service
builder.Services.AddTransient<IEmailSenderService, EmailSenderService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
