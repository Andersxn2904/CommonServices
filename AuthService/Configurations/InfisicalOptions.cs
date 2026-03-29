namespace AuthService.Configurations;

public class InfisicalOptions
{
    public const string Section = "Infisical";

    /// <summary>Habilita la integración con Infisical. Si false, usa configuración local.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>URL base del servidor Infisical self-hosted. Ej: http://infisical:8080</summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>Client ID de la Machine Identity creada en Infisical.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret de la Machine Identity creada en Infisical.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>ID del proyecto en Infisical donde viven los secretos de Kauthen.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Ambiente de secretos a cargar. Ej: prod, dev, staging.</summary>
    public string Environment { get; set; } = "prod";

    /// <summary>Path dentro del proyecto. Usa / para la raíz.</summary>
    public string SecretPath { get; set; } = "/";
}
