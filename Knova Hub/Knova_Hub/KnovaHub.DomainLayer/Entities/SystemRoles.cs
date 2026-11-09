namespace KnovaHub.DomainLayer.Entities
{
    /// <summary>
    /// Roles fijos del sistema. Se crean por seed en la migración y no se pueden crear desde la API.
    /// Los Id 1-3 se mantienen de la versión anterior (Admin, Editor, User) para no mover datos existentes.
    /// </summary>
    public static class SystemRoles
    {
        public const int AdminEmpresaRoleId = 1;
        public const int SupervisorRoleId = 2;
        public const int UsuarioRoleId = 3;
        public const int AdminPlataformaRoleId = 4;

        public const string AdminEmpresaRoleName = "AdminEmpresa";
        public const string SupervisorRoleName = "Supervisor";
        public const string UsuarioRoleName = "Usuario";
        public const string AdminPlataformaRoleName = "AdminPlataforma";

        /// <summary>Roles que un AdminEmpresa puede asignar. AdminPlataforma solo se crea por seed.</summary>
        public static readonly IReadOnlyCollection<int> AsignablesPorEmpresa =
            new[] { AdminEmpresaRoleId, SupervisorRoleId, UsuarioRoleId };
    }
}
