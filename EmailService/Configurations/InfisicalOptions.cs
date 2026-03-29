namespace EmailService.Configurations;

public class InfisicalOptions
{
    public const string Section = "Infisical";

    public bool Enabled { get; set; } = false;
    public string SiteUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Environment { get; set; } = "prod";
    public string SecretPath { get; set; } = "/";
}
