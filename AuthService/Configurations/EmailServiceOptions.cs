namespace AuthService.Configurations;

public class EmailServiceOptions
{
    public const string Section = "EmailService";

    public string BaseUrl { get; set; } = "http://email-service:8080";
}
