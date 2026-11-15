# Ticket 2 (Avance 4) — Conversión de adjuntos a Markdown

Cada archivo que se sube a un documento se convierte a Markdown y se guarda en la base de datos.
La IA debe leer ese Markdown, nunca el archivo original: ocupa mucho menos y ahorra tokens.

## Librerías elegidas

| Formato | Librería | Motivo |
|---|---|---|
| PDF | PdfPig 0.1.16 | C# puro, licencia Apache 2.0, sin dependencias nativas. Descartada iText7 por su licencia AGPL. |
| DOCX | DocumentFormat.OpenXml 3.5.1 | Librería oficial de Microsoft, licencia MIT. |
| TXT, MD | Ninguna | Se guardan tal cual, normalizados. |

Las dos librerías solo se usan en `InfrastructureLayer` (`Services/DocumentTextExtractor.cs`).
Application solo conoce la interfaz `IDocumentTextExtractor`.

## Qué se convierte

- **PDF:** títulos (letra más grande que el texto normal) → `#` / `##`, párrafos unidos, palabras cortadas con guion reunidas, viñetas → `- `, filas alineadas en columnas → tabla Markdown.
- **DOCX:** estilos de título de Word → `#`, listas → `- `, tablas → tabla Markdown. Se descarta el formato visual.
- **Todos:** sin espacios al final de línea y como máximo una línea en blanco seguida.

**Limitación conocida:** un PDF a dos columnas se lee fila por fila, mezclando las columnas.

## Estados del texto (`Documents.TextStatus`)

| Valor | Estado | Significado |
|---|---|---|
| `NULL` | — | El documento no tiene archivo. |
| 1 | `Pendiente` | Archivo subido antes de esta migración. Se indexa al volver a subirlo. |
| 2 | `Indexado` | El Markdown está en `DocumentTexts`. |
| 3 | `NoIndexable` | PDF escaneado, vacío, dañado o protegido. `TextStatusReason` dice el motivo. |

Un archivo no indexable **se adjunta igual** y la API responde 200 con el estado y el motivo; nunca 500.
El frontend muestra el motivo en el detalle del documento.

## Ciclo de vida

- **Subir o reemplazar archivo:** se convierte y se guarda el texto en la misma transacción que el archivo nuevo. Si el archivo nuevo no es indexable, se borra el texto del anterior.
- **Quitar archivo:** se borra el texto y el estado vuelve a `NULL`.
- **Eliminar documento:** el texto se borra en cascada desde la base de datos.

## Para el Ticket 3 (consultas a la IA)

Usar `IDocumentRepository.GetIndexedTextsAsync(companyId, visibleDepartmentIds)`.
Devuelve `(DocumentId, Title, Markdown)` solo de documentos indexados de la empresa,
con la misma regla de visibilidad que el listado (`null` = todos, para AdminEmpresa).

## Migración `Ticket2Avance4_DocumentTexts`

- Agrega `TextStatus` y `TextStatusReason` a `Documents` y crea la tabla `DocumentTexts`.
- Los documentos que ya tienen archivo quedan en `Pendiente`, así que se aplica sobre una base de datos existente sin perder datos.
- Se aplica sola al ejecutar la API en Development, o con:

```bash
dotnet ef database update --project KnovaHub.InfrastructureLayer --startup-project KnovaHub.API
```

Después de aplicarla hay que regenerar el `.bak` de la base de datos para el equipo.
