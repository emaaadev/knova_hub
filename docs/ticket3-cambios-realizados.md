# Ticket 3 — Consultas a la IA

Endpoint para preguntar en lenguaje natural sobre los documentos indexados de la empresa del usuario. La IA recibe solo los fragmentos relevantes y responde con sus fuentes.

## Flujo

`POST /api/ai/ask` → `AiController` → `QueryService` → `IDocumentRepository.GetIndexedTextsAsync` → `IAiService` (Claude) → respuesta con fuentes → registro en `QueryLogs`.

Solo se envían documentos indexados de la empresa y departamentos visibles para el usuario (`null = todos` para `AdminEmpresa`).

## Archivos creados

| Archivo | Propósito |
|---------|-----------|
| `ApplicationLayer/DTOs/AiAskRequestDto.cs` | Request del endpoint (solo `question`) |
| `ApplicationLayer/DTOs/AiAskResponseDto.cs` | Response (`answer` + `sources` con `documentId` y `title`) |
| `DomainLayer/Entities/QueryLog.cs` | Entidad de registro de consultas |
| `DomainLayer/Repository/IQueryLogRepository.cs` | Interfaz del repositorio de logs |
| `InfrastructureLayer/Repositories/QueryLogRepository.cs` | Implementación EF Core |
| `ApplicationLayer/Contract/IQueryService.cs` | Contrato del servicio de aplicación |
| `ApplicationLayer/Services/QueryService.cs` | Coordinación: textos indexados → IA → log |
| `API/Controllers/AiController.cs` | Endpoint `POST /api/ai/ask` |
| `Tests/Services/QueryServiceTests.cs` | Pruebas unitarias con Moq |
| `Tests/Endpoints/AiEndpointTests.cs` | Pruebas de endpoint (200, 400, 401, 403, 404, 429) |

## Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `API/Program.cs` | Registro de `IQueryLogRepository` e `IQueryService` |
| `InfrastructureLayer/DbContext/KnovaHubDbContext.cs` | `DbSet<QueryLog> QueryLogs` |

## Migración de base de datos

**Archivo:** `20261115235221_AddQueryLogsTable.cs`

**Tabla creada:** `QueryLogs`

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `Id` | int (PK, Identity) | Identificador |
| `UserId` | int (FK → Users) | Usuario que consulta |
| `CompanyId` | int (FK → Companies) | Empresa del usuario |
| `Question` | nvarchar(max) | Pregunta enviada |
| `DocumentIds` | nvarchar(max) | IDs de documentos fuentes (CSV) |
| `AskedAt` | datetime2 | Fecha y hora (UTC) |

**Claves foráneas:**
- `FK_QueryLogs_Users_UserId` → `Users.Id` (Restrict)
- `FK_QueryLogs_Companies_CompanyId` → `Companies.Id` (Restrict)

**Aplicar:**
```bash
dotnet ef database update --project KnovaHub.InfrastructureLayer --startup-project KnovaHub.API
```

## Reglas de negocio

- **Aislamiento por empresa y departamento:** `GetIndexedTextsAsync(companyId, visibleDepartmentIds)` filtra por empresa y departamentos visibles según el rol. Un documento de otro departamento nunca entra en el contexto.
- **Registro de consulta:** Cada consulta se guarda en `QueryLogs` incluso si la IA falla (`try/finally`).
- **Límites de uso:** `InMemoryAiUsageLimiter` controla 30 consultas/usuario/día y 300/empresa/día (429 al exceder).
- **Sin información:** Si los documentos no contienen la respuesta, la IA indica claramente que no la encontró (no inventa).

## Respuestas del endpoint

| Código | Significado |
|--------|-------------|
| 200 | Respuesta con fuentes |
| 400 | Pregunta vacía |
| 401 | Sin token o token expirado |
| 403 | Acceso denegado (otra empresa/departamento) |
| 404 | Ruta inexistente |
| 429 | Límite de uso superado |

## Pruebas

Total: **281 pruebas, 0 fallos**.

- **Unitarias (11):** validan aislamiento por rol, construcción del `AiRequestDto`, guardado de `QueryLog`, devolución de fuentes.
- **Endpoint (7):** validan 200, 400, 401 (sin token y expirado), 403, 404, 429.