namespace AuthService.Configurations;

public class LockoutOptions
{
    public const string Section = "Lockout";

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
}
