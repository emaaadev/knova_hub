namespace KnovaHub.ApplicationLayer.DTOs;

/// <summary>Resultado de convertir un archivo adjunto a Markdown.</summary>
public class DocumentTextResult
{
    public bool IsIndexable { get; private init; }
    public string Markdown { get; private init; } = string.Empty;

    /// <summary>Motivo, para mostrar al usuario, cuando el archivo no es indexable.</summary>
    public string? Reason { get; private init; }

    public static DocumentTextResult Indexed(string markdown) =>
        new() { IsIndexable = true, Markdown = markdown };

    public static DocumentTextResult NotIndexable(string reason) =>
        new() { IsIndexable = false, Reason = reason };
}
