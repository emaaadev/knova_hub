using System.Text;

namespace KnovaHub.InfrastructureLayer.Services.TextExtraction;

internal static class MarkdownText
{
    /// <summary>
    /// Los elementos de lista terminan en un solo salto de línea. Antes de cualquier otro bloque
    /// se agrega la línea en blanco que cierra la lista; sin ella, Markdown uniría el bloque
    /// siguiente al último elemento.
    /// </summary>
    public static void EndList(StringBuilder markdown)
    {
        if (markdown.Length > 0 && markdown[^1] == '\n' &&
            (markdown.Length < 2 || markdown[^2] != '\n'))
        {
            markdown.Append('\n');
        }
    }
}
