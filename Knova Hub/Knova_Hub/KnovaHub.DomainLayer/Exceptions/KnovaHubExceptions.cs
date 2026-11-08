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