# Ticket 5 — Modelo inicial de conocimiento

## Entidades

- `Document`: contenido de tipo `Documento`, `Wiki` o `Minuta`, con empresa, autor, categoría opcional, visibilidad y departamento opcional.
- `Category`: catálogo de categorías aislado por empresa.
- `Tag`: catálogo de etiquetas aislado por empresa.
- `DocumentTag`: tabla de unión muchos-a-muchos entre documentos y etiquetas.

## Reglas persistidas

- `DocumentVisibility.General` requiere `DepartmentId = NULL`.
- `DocumentVisibility.Departamento` requiere `DepartmentId != NULL`.
- El tipo y la visibilidad se almacenan como enteros para mantener el modelo consistente con los enums de .NET.
- Los nombres de categorías y etiquetas son únicos dentro de cada empresa.
- Los metadatos del archivo son todos nulos cuando no existe adjunto. Si hay adjunto, deben existir nombre original, clave de almacenamiento, MIME, extensión y tamaño positivo.
- Las relaciones con empresa, autor, categoría y departamento usan `Restrict`; eliminar un documento elimina sus filas de `DocumentTags`.
- `StorageKey` es una clave interna generada por el servidor, no una ruta proporcionada por el cliente.

## Importante: validaciones de empresa

Las FK individuales aseguran la existencia de las entidades relacionadas, pero no garantizan por sí solas que `AuthorId`, `CategoryId` y `DepartmentId` pertenezcan al mismo `CompanyId` del documento. Esa validación debe realizarse obligatoriamente en la capa de aplicación/repositorio antes de guardar y al recuperar recursos. No confiar en identificadores enviados por el frontend.

## Crear la migración

Desde la carpeta `Knova Hub/Knova_Hub` de la solución, ejecutar en una terminal con .NET SDK 8 y EF Core Tools:

```bash
dotnet ef migrations add Ticket5_KnowledgeManagement \
  --project KnovaHub.InfrastructureLayer \
  --startup-project KnovaHub.API \
  --output-dir Migrations
```

Revisar el archivo generado y `KnovaHubDbContextModelSnapshot.cs` antes de aplicar:

```bash
dotnet ef migrations script \
  --project KnovaHub.InfrastructureLayer \
  --startup-project KnovaHub.API \
  --output ticket5-migration.sql
```

Luego, en un entorno de desarrollo:

```bash
dotnet ef database update \
  --project KnovaHub.InfrastructureLayer \
  --startup-project KnovaHub.API
```

No se incluyó una migración generada manualmente: debe producirla EF Core desde el modelo real para que el snapshot, el diseñador y el historial de migraciones queden sincronizados.

## Respaldo SQL Server (.bak)

El `.bak` debe generarse desde la instancia SQL Server que contiene la base de datos de Knova Hub, después de confirmar la migración aplicada. Ejemplo (reemplazar base de datos y ruta por las reales):

```sql
BACKUP DATABASE [KnovaHubDb]
TO DISK = N'C:\Backups\KnovaHub_Ticket5.bak'
WITH COPY_ONLY, INIT, CHECKSUM, STATS = 10;

RESTORE VERIFYONLY
FROM DISK = N'C:\Backups\KnovaHub_Ticket5.bak'
WITH CHECKSUM;
```

La cuenta del servicio SQL Server debe tener permiso de escritura en la carpeta de destino.
