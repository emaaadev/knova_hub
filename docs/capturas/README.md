# Capturas: consulta a la IA, recomendaciones y relacionados

Tomadas el 16/11/2026 con la API local (perfil `http`), la base `KnovaHub_Demo_Capturas` y el frontend servido en `http://127.0.0.1:5500`.

| Captura | Rol | Qué demuestra | Origen |
| --- | --- | --- | --- |
| 01-adminempresa-consulta-fuentes-recomendaciones | AdminEmpresa | Respuesta real de la IA con negritas y sin asteriscos, fuentes como enlaces, recomendaciones reales | Real |
| 02-adminempresa-detalle-relacionados | AdminEmpresa | Clic en una fuente abre el documento; sección "Relacionados" | Real |
| 03-estado-cargando | AdminEmpresa | Estado de carga ("Consultando a la IA…", botón desactivado) | Respuesta retrasada 5 s |
| 04-estado-error | AdminEmpresa | Estado de error sin romper la pantalla | Conexión rechazada simulada |
| 05-estado-sin-resultados | AdminEmpresa | Estado sin resultados ("No se encontraron documentos…") | Respuesta simulada sin fuentes |
| 06-limite-429 | AdminEmpresa | Aviso del límite de consultas | Respuesta 429 simulada con el mismo mensaje de la API |
| 07-respuesta-sin-asteriscos-y-html-escapado | AdminEmpresa | Markdown convertido y `<img onerror>` / `<script>` mostrados como texto, sin ejecutarse | Respuesta simulada |
| 08-supervisor-consulta-recomendaciones | Supervisor (Ventas) | Fuentes y recomendaciones sin la guía de Soporte | Real |
| 09-supervisor-detalle-relacionados | Supervisor | Relacionados solo con documentos visibles | Real |
| 10-usuario-consulta-sin-documentos-ajenos | Usuario (Ventas) | La IA no recibe ni muestra la guía de Soporte | Real |
| 11-usuario-detalle-relacionados | Usuario | Relacionados en el detalle | Real |
| 12-adminplataforma-mensajes-claros | AdminPlataforma | Sin empresa: mensajes de error claros, la pantalla no se rompe | Real |

Las capturas marcadas como simuladas usan el frontend real; solo se intercepta la respuesta de `POST /api/ai/ask` en el navegador, porque esos estados no se pueden forzar sin apagar la API o cambiar su configuración.
