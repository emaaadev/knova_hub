namespace KnovaHub.DomainLayer.Entities;

/// <summary>
/// Estado del texto que la IA puede leer de un archivo adjunto.
/// Es null cuando el documento no tiene archivo.
/// </summary>
public enum DocumentTextStatus
{
    /// <summary>El archivo se subió antes de la conversión a Markdown y aún no se procesa.</summary>
    Pendiente = 1,

    /// <summary>El texto se convirtió a Markdown y está guardado en DocumentTexts.</summary>
    Indexado = 2,

    /// <summary>No se pudo obtener texto (escaneado, vacío, dañado o protegido).</summary>
    NoIndexable = 3
}
