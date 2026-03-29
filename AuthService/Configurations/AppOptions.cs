namespace AuthService.Configurations;

public class AppOptions
{
    public const string Section = "App";

    public string BaseUrl { get; set; } = "http://localhost:8080";
}
