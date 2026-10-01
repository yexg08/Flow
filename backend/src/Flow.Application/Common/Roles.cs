namespace Flow.Application.Common;

public static class Roles
{
    /// <summary>Dueño de la plataforma: ve todos los negocios. No pertenece a ninguno.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Dueño de un negocio: lo administra por completo.</summary>
    public const string Owner = "Owner";

    /// <summary>Empleado de un negocio.</summary>
    public const string Staff = "Staff";

    public static readonly string[] TenantMembers = [Owner, Staff];
    public static readonly string[] All = [SuperAdmin, Owner, Staff];
}
