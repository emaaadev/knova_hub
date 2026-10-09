namespace KnovaHub.InfrastructureLayer.Exceptions;

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
    public DataValidationException(string message) : base(message)
    {
    }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Usuario o contraseña incorrectos.")
    {
    }
}