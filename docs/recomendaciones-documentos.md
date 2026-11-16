# Documentos relacionados y recomendaciones

Ambos endpoints requieren un JWT de usuario activo. La empresa y los departamentos visibles se resuelven con las mismas reglas que el listado de documentos. El administrador de empresa puede ver los documentos de su empresa, un supervisor puede ver los generales y los de sus departamentos asignados, y un usuario puede ver los generales y los de su departamento.

| Solicitud | Resultado |
| --- | --- |
| `GET /api/documents/{id}/related` | Hasta 10 documentos visibles con coincidencias. Devuelve 404 cuando el documento base no existe, pertenece a otra empresa o queda fuera del alcance del usuario. |
| `GET /api/documents/recommendations` | Hasta 10 documentos visibles. Primero devuelve coincidencias con el historial y completa con los más consultados. Sin coincidencias o sin historial, todo el orden es por popularidad. |

Cada resultado contiene `id`, `title`, `description`, `type` y `categoryId`. Si el catálogo visible está vacío, las recomendaciones devuelven `[]`.

## Método y costo

Elegimos palabras clave. Se extraen términos únicos del título, la descripción, el contenido y el Markdown indexado del adjunto. Se normalizan acentos y mayúsculas, y se omiten palabras frecuentes en español e inglés. Para limitar la transferencia y el procesamiento, se leen hasta 8 000 caracteres de cada campo largo.

- **Relacionados.** Se suman 3 puntos por categoría compartida, 2 por cada etiqueta compartida hasta tres y hasta 6 por similitud coseno entre los conjuntos de palabras. Se excluye el documento base y se conservan resultados con puntaje positivo.
- **Recomendaciones.** Se toman las 10 preguntas más recientes a la IA y los últimos 10 documentos distintos abiertos por el usuario en los últimos 30 días. Una apertura reciente solo aporta contexto si el documento sigue visible. Para cada candidato se suma su mejor puntaje de relación con esas aperturas y hasta 6 puntos por similitud con las preguntas. Las aperturas recientes pueden volver a aparecer al completar la lista por popularidad.
- **Popularidad.** Cada apertura autorizada mediante `GET /api/documents/{id}` registra un evento desde la migración `AddDocumentReads`. El orden usa el total acumulado de aperturas por documento dentro de la empresa. Cuando todos tienen cero aperturas o quedan empatados, se prioriza el documento actualizado o creado más recientemente y luego el ID ascendente. Los ID del contexto enviado a la IA representan fuentes potenciales y quedan fuera del conteo.

El costo externo estimado es **USD 0 por solicitud**, sin tokens ni almacenamiento vectorial. Los costos de infraestructura son el almacenamiento de un evento por apertura, las lecturas de SQL Server y el procesamiento del catálogo visible por solicitud. El método está pensado para el catálogo actual y los costos de infraestructura crecen con su tamaño.

## Verificación

Aplicar las migraciones de la API y ejecutar `dotnet test` desde la carpeta de la solución. Las pruebas de `DocumentDiscoveryEndpointTests` usan solicitudes HTTP, JWT y el servicio de documentos real, con catálogos e historiales de prueba. Estas rutas funcionan sin clave de Anthropic. El endpoint de consultas de IA existente sigue usando su configuración de User Secrets para las pruebas manuales.

Para reproducir los casos en Swagger con una base de desarrollo:

1. Crear documentos de una empresa sobre vacaciones. Darles categoría, etiquetas o texto en común. Consultar los relacionados del documento de referencia y comprobar el orden y la ausencia del propio documento.
2. Abrir un documento varias veces con un usuario de la empresa y otro documento menos veces. Autenticarse como un segundo usuario sin historial. Verificar que las recomendaciones muestran primero el documento más abierto que puede ver.
3. Crear un documento en otra empresa y otro en un departamento al que el segundo usuario no tiene acceso. Verificar que no aparecen en las listas y que consultar sus relacionados, o los de un ID inexistente, devuelve 404.
