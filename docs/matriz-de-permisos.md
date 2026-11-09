# Matriz de permisos — Knova Hub

Ticket 3 (Avance 3): roles, departamentos y autorización.

## Roles

| Id | Rol | Empresa | Cómo se crea |
|----|-----|---------|--------------|
| 1 | **AdminEmpresa** | Obligatoria | Al registrar la empresa (el contacto principal) o asignado por otro AdminEmpresa |
| 2 | **Supervisor** (antes *Editor*) | Obligatoria | Asignado por un AdminEmpresa |
| 3 | **Usuario** | Obligatoria, y pertenece a **un** departamento | Asignado por un AdminEmpresa |
| 4 | **AdminPlataforma** | Opcional (`CompanyId` puede ser `NULL`) | **Solo por seed.** La API no permite asignarlo. Todavía no tiene panel |

Los Id 1–3 son los mismos de antes (`Admin`, `Editor`, `User`), así que la migración solo cambia los nombres y no mueve datos.

La base de datos lo refuerza con `CK_Users_CompanyId_Role`: solo un usuario con `RoleId = 4` puede tener `CompanyId` en `NULL`.

## Departamentos

- Cada empresa tiene sus departamentos. El nombre no se repite dentro de la misma empresa (`IX_Departments_CompanyId_Name`).
- El **AdminEmpresa** los crea, los edita y los activa o desactiva.
- Un **Supervisor** puede tener asignados uno o varios departamentos (tabla `SupervisorDepartments`).
- Cada **Usuario** pertenece a un departamento (`Users.DepartmentId`). La columna admite `NULL` porque AdminEmpresa y AdminPlataforma no tienen departamento, y la API lo exige cuando el rol es Usuario.
- Desactivar un departamento no mueve a sus Usuarios ni quita a sus Supervisores: el Supervisor sigue viéndolo y puede mover usuarios fuera de él, pero no se le pueden asignar usuarios nuevos ni asignarlo a otro Supervisor.

## Matriz por endpoint

✅ = permitido · 🟡 = solo sobre sus departamentos · ❌ = 403

| Endpoint | AdminPlataforma | AdminEmpresa | Supervisor | Usuario |
|----------|:---:|:---:|:---:|:---:|
| `GET /api/departments` (listar) | ❌ | ✅ todos los de su empresa | 🟡 solo los suyos | ❌ |
| `GET /api/departments/{id}` | ❌ | ✅ | 🟡 | ❌ |
| `POST /api/departments` (crear) | ❌ | ✅ | ❌ | ❌ |
| `PUT /api/departments/{id}` (editar o desactivar) | ❌ | ✅ | ❌ | ❌ |
| `PUT /api/supervisors/{id}/departments` (asignar departamentos a un Supervisor) | ❌ | ✅ | ❌ | ❌ |
| `PUT /api/users/{id}/role` (cambiar rol) | ❌ | ✅ | ❌ | ❌ |
| `PUT /api/users/{id}/department` (mover a un Usuario de departamento) | ❌ | ✅ | 🟡 debe supervisar el departamento actual **y** el nuevo | ❌ |
| `GET /api/auth/me` | ✅ | ✅ | ✅ | ✅ |

Sin token, o con un token vencido o inválido, la respuesta es **401**. Con un token válido pero sin permiso, la respuesta es **403**. Si el recurso es de otra empresa, la respuesta es **404**, para no revelar que existe.

## Reglas de negocio

| Regla | Dónde se valida | Respuesta |
|-------|-----------------|-----------|
| No se puede asignar AdminPlataforma (ni un rol que no existe) | `UserRoleService` | 400 |
| El AdminEmpresa no puede quitarse su propio rol | `UserRoleService` | 409 |
| Cada empresa mantiene al menos un AdminEmpresa activo | `UserRepository.ChangeRoleKeepingCompanyAdminAsync`, en una transacción que bloquea la fila de la empresa (`UPDLOCK`) para que dos cambios simultáneos no la dejen sin admin | 409 |
| Para pasar a alguien a Usuario hay que indicar un departamento activo de la misma empresa | `UserRoleService` | 400 |
| Al dejar de ser Supervisor se le quitan los departamentos supervisados | `UserRepository` | — |
| Solo se asignan a un Supervisor departamentos activos de su empresa | `DepartmentService` | 400 |
| Nombre de departamento repetido en la empresa | Índice único en la BD | 409 |

## Cómo está implementado

- Las políticas están en `KnovaHub.API/Authorization/Policies.cs`: `AdminPlataforma`, `AdminEmpresa`, `GestionDepartamentos` (AdminEmpresa o Supervisor) y `AccesoDepartamento`.
- `AccesoDepartamento` es una política **por recurso** (`DepartmentAccessHandler`). Consulta la base de datos en cada petición, así que si a un Supervisor le quitan un departamento, pierde el acceso en ese momento.
- `KnovaExceptionFilter` convierte las excepciones de negocio en 400, 404 o 409 con `{ message }`.

## Limitación conocida

El rol viaja dentro del JWT, que dura 8 horas. Si a un AdminEmpresa le cambian el rol, **conserva los permisos de su rol anterior hasta que su token vence**. Aun así, no puede dejar la empresa sin AdminEmpresa, porque esa regla se valida contra la base de datos. Hay dos formas de cerrarlo en otro ticket: validar el rol contra la base de datos en cada petición (`OnTokenValidated`) o acortar la duración del token. Lo mismo ocurre con un usuario desactivado: su token sigue siendo válido hasta que venza.

---

# Módulo Usuarios y solicitudes de alta — Ticket 4 (Avance 3)

## Roles implicados

- **AdminEmpresa**: lista, crea, ve, edita y activa/desactiva usuarios (además de cambiar roles, del Ticket 3), siempre dentro de su empresa.
- **Supervisor**: solo ve a los usuarios de los departamentos que supervisa y solicita el alta de empleados de esos departamentos.
- **Usuario**: sin acceso al módulo (solo puede cambiar su propia contraseña).

## Solicitudes de alta

1. El **Supervisor** crea una solicitud con los datos del empleado (nombre, correo, teléfono y departamento). La solicitud queda **Pendiente** y el usuario **no existe ni puede entrar**.
2. El **AdminEmpresa** aprueba o rechaza la solicitud.
3. Al **aprobar**, el sistema crea el `Usuario` (rol Usuario, en el departamento indicado) con una **contraseña temporal** generada con BCrypt y marca `MustChangePassword = true`.
4. El empleado entra con la contraseña temporal y **debe cambiarla** en su primer inicio de sesión.

## Matriz por endpoint (nuevos)

| Endpoint | AdminPlataforma | AdminEmpresa | Supervisor | Usuario |
|----------|:---:|:---:|:---:|:---:|
| `GET /api/users` (listar) | ❌ | ✅ todos los de su empresa | 🟡 solo los de sus departamentos | ❌ |
| `GET /api/users/{id}` | ❌ | ✅ | 🟡 | ❌ |
| `POST /api/users` (crear) | ❌ | ✅ | ❌ | ❌ |
| `PUT /api/users/{id}` (editar) | ❌ | ✅ | ❌ | ❌ |
| `PATCH /api/users/{id}/active` | ❌ | ✅ | ❌ | ❌ |
| `GET /api/users/requests` | ❌ | ✅ todas las de su empresa | 🟡 las propias | ❌ |
| `POST /api/users/requests` | ❌ | ❌ | ✅ solo en sus departamentos | ❌ |
| `POST /api/users/requests/{id}/approve` | ❌ | ✅ | ❌ | ❌ |
| `POST /api/users/requests/{id}/reject` | ❌ | ✅ | ❌ | ❌ |
| `GET /api/supervisors/{id}/departments` | ❌ | ✅ | ❌ | ❌ |
| `POST /api/auth/change-password` | ✅ | ✅ | ✅ | ✅ (la propia) |

## Reglas de negocio

| Regla | Dónde se valida | Respuesta |
|-------|-----------------|-----------|
| El Supervisor solo solicita altas en departamentos activos que supervisa | `RegistrationRequestService` | 400 / 409 |
| El correo no puede estar en uso por un usuario ni por otra solicitud pendiente | `RegistrationRequestService` | 409 |
| Al aprobar se revalida que el correo siga libre y el departamento siga activo | `RegistrationRequestService` | 409 |
| Solo se aprueban/rechazan solicitudes Pendientes | `RegistrationRequestService` | 409 |
| La contraseña temporal se genera con BCrypt y obliga a cambiarla (`MustChangePassword`) | `PasswordPolicy` + `AuthService` | — |
| El AdminEmpresa no puede desactivarse a sí mismo | `UserService` | 409 |
| El cambio de contraseña exige la contraseña actual y una nueva que cumpla la política | `AuthService` | 401 / 400 |

## Cómo está implementado

- `UserService` e `IRegistrationRequestService` siguen el patrón de capas (contrato en ApplicationLayer, implementación en Services).
- `RegistrationRequestRepository.ApproveAsync` crea el `User` y actualiza la solicitud en la **misma transacción**.
- Políticas nuevas en `Policies.cs`: `GestionUsuarios` (AdminEmpresa o Supervisor) y `Supervisor`.
- El login devuelve `mustChangePassword`; el frontend redirige a `cambiar-contrasena.html` y la API expone `POST /api/auth/change-password`.
