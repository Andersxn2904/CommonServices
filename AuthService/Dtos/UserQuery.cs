namespace AuthService.Dtos;

public class UserQuery
{
    /// <summary>Búsqueda parcial en email o displayName</summary>
    public string? Search { get; set; }

    /// <summary>Filtrar por estado activo/inactivo</summary>
    public bool? IsActive { get; set; }

    /// <summary>Filtrar usuarios que tengan este rol</summary>
    public Guid? RoleId { get; set; }

    /// <summary>Filtrar usuarios con roles asociados a esta aplicación</summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>Filtrar usuarios con roles asociados al código de aplicación</summary>
    public string? ApplicationCode { get; set; }
}
