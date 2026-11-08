namespace KnovaHub.DomainLayer.Exceptions;

public class DuplicateEntityException : Exception
{
    public string Field { get; }

    public DuplicateEntityException(string field, string message) : base(message)
    {
        Field = field;
    }
}

public class DataValidationException : Exception
{
    public string? Field { get; }

    
    public DataValidationException(string message) : base(message)
    {
    }

    // permite identificar el campo.
    public DataValidationException(string field, string message) : base(message)
    {
        Field = field;
    }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Usuario o contraseña incorrectos.")
    {
    }
}

/// <summary>El recurso no existe o no pertenece a la empresa del usuario (404).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>La operación rompe una regla de negocio, p. ej. dejar una empresa sin AdminEmpresa (409).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}