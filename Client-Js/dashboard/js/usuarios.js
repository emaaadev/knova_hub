// Módulo Usuarios: el AdminEmpresa lista/crea/edita/activa/desactiva/cambia roles;
// el Supervisor solo ve a los usuarios de sus departamentos (y solicita altas desde Solicitudes).
const ROL_ADMIN = 1;
const ROL_SUPERVISOR = 2;
const ROL_USUARIO = 3;

const NOMBRES_ROL = { 1: "AdminEmpresa", 2: "Supervisor", 3: "Usuario" };

let usuarioActual = null;
let departamentos = [];
let modoFormulario = null; // 'create' | 'edit' | 'role'
let usuarioEnEdicion = null;

const session = App.setupShell();
if (!session) {
    // Redirige al login o a cambiar contraseña.
} else {
    usuarioActual = session.user;
    init();
}

async function init() {
    const esAdmin = usuarioActual.role === "AdminEmpresa";

    document.getElementById("btn-nuevo").hidden = !esAdmin;
    document.getElementById("btn-solicitar").hidden = esAdmin;
    document.getElementById("col-acciones").hidden = !esAdmin;

    document.getElementById("btn-nuevo").addEventListener("click", () => abrirFormulario("create"));
    document.getElementById("btn-cancelar").addEventListener("click", cerrarFormulario);
    document.getElementById("btn-guardar").addEventListener("click", guardar);
    configurarCopiaContrasena();
    document.getElementById("btn-temp-ok").addEventListener("click", () => {
        document.getElementById("card-temp").hidden = true;
        cargarUsuarios();
    });

    document.getElementById("f-rol").addEventListener("change", actualizarVisibilidadDepartamento);

    if (esAdmin) {
        await cargarDepartamentos();
    }

    await cargarUsuarios();
}

async function cargarDepartamentos() {
    try {
        departamentos = await App.api("/api/departments");
        const select = document.getElementById("f-departamento");
        select.innerHTML = departamentos
            .filter((d) => d.isActive)
            .map((d) => `<option value="${d.id}">${App.esc(d.name)}</option>`)
            .join("");
    } catch {
        departamentos = [];
    }
}

async function cargarUsuarios() {
    const cuerpo = document.getElementById("cuerpo-usuarios");
    const vacio = document.getElementById("vacio-usuarios");
    const subtitulo = document.getElementById("list-subtitle");

    UI.filaMensaje(cuerpo, "Cargando usuarios…", "cargando");
    vacio.hidden = true;

    let usuarios;
    try {
        usuarios = await App.api("/api/users");
    } catch (err) {
        subtitulo.textContent = "No se pudieron cargar los usuarios.";
        UI.filaMensaje(cuerpo, `${err.message}`, "error", cargarUsuarios);
        return;
    }
    cuerpo.innerHTML = "";

    if (usuarioActual.role === "AdminEmpresa") {
        subtitulo.textContent = `${usuarios.length} usuario(s) en tu empresa.`;
    } else {
        subtitulo.textContent = `Ves los usuarios de los departamentos que supervisas (${usuarios.length}).`;
    }

    if (usuarios.length === 0) {
        vacio.hidden = false;
        return;
    }

    const esAdmin = usuarioActual.role === "AdminEmpresa";

    cuerpo.innerHTML = usuarios.map((u) => {
        const estado = u.isActive
            ? '<span class="badge badge-ok">Activo</span>'
            : '<span class="badge badge-danger">Inactivo</span>';

        let acciones = "";
        if (esAdmin) {
            acciones = `
                <div class="row-actions">
                    <button class="btn btn-sm" data-accion="editar" data-id="${u.id}">Editar</button>
                    <button class="btn btn-sm" data-accion="rol" data-id="${u.id}">Cambiar rol</button>
                    <button class="btn btn-sm ${u.isActive ? "btn-danger" : ""}" data-accion="activo" data-id="${u.id}" data-activo="${!u.isActive}">
                        ${u.isActive ? "Desactivar" : "Activar"}
                    </button>
                </div>`;
        }

        const celdaAcciones = esAdmin ? `<td>${acciones || "—"}</td>` : "";

        return `
            <tr>
                <td>${App.esc(u.fullName)}</td>
                <td>${App.esc(u.email)}</td>
                <td>${App.esc(u.role)}</td>
                <td>${App.esc(u.departmentName || "—")}</td>
                <td>${estado}</td>
                ${celdaAcciones}
            </tr>`;
    }).join("");

    cuerpo.querySelectorAll("[data-accion]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const accion = btn.dataset.accion;
            const id = Number(btn.dataset.id);
            const user = usuarios.find((x) => x.id === id);
            if (accion === "editar") abrirFormulario("edit", user);
            else if (accion === "rol") abrirFormulario("role", user);
            else if (accion === "activo") cambiarEstado(user, btn.dataset.activo === "true");
        });
    });
}

async function cambiarEstado(user, isActive) {
    if (!isActive) {
        const confirmado = await UI.confirmar({
            titulo: "Desactivar usuario",
            mensaje: `¿Desactivar a «${user.fullName}»? Podrás volver a activarlo después.`,
            aceptar: "Desactivar",
            peligro: true
        });
        if (!confirmado) return;
    }

    try {
        await App.api(`/api/users/${user.id}/active`, { method: "PATCH", body: { isActive } });
        UI.aviso(isActive ? "Usuario activado." : "Usuario desactivado.");
        await cargarUsuarios();
    } catch (err) {
        UI.aviso(err.message, "error");
    }
}

function abrirFormulario(modo, user = null) {
    modoFormulario = modo;
    usuarioEnEdicion = user;

    const card = document.getElementById("card-form");
    card.hidden = false;
    card.scrollIntoView({ behavior: "smooth" });

    document.getElementById("form-message").hidden = true;
    document.getElementById("form-message").className = "form-message";

    const campoRol = document.getElementById("campo-rol");
    const campoDepto = document.getElementById("campo-departamento");

    const titulo = {
        create: "Crear usuario",
        edit: "Editar usuario",
        role: `Cambiar rol de ${user?.fullName || "usuario"}`
    }[modo];
    document.getElementById("form-titulo").textContent = titulo;

    // Al cambiar solo el rol no tiene sentido mostrar nombre y correo.
    Validar.limpiar(card);
    const soloRol = modo === "role";
    document.getElementById("f-nombre").closest(".field").hidden = soloRol;
    document.getElementById("f-email").closest(".field").hidden = soloRol;

    if (modo === "create") {
        campoRol.hidden = false;
        document.getElementById("f-nombre").value = "";
        document.getElementById("f-email").value = "";
        document.getElementById("f-rol").value = String(ROL_USUARIO);
    } else if (modo === "edit") {
        campoRol.hidden = true;
        campoDepto.hidden = true;
        document.getElementById("f-nombre").value = user.fullName || "";
        document.getElementById("f-email").value = user.email || "";
    } else if (modo === "role") {
        campoRol.hidden = false;
        document.getElementById("f-nombre").value = user.fullName || "";
        document.getElementById("f-email").value = user.email || "";
        document.getElementById("f-rol").value = String(user.roleId);
    }

    actualizarVisibilidadDepartamento();
}

function cerrarFormulario() {
    document.getElementById("card-form").hidden = true;
    modoFormulario = null;
    usuarioEnEdicion = null;
}

function actualizarVisibilidadDepartamento() {
    const campoDepto = document.getElementById("campo-departamento");
    const rol = Number(document.getElementById("f-rol").value);
    campoDepto.hidden = !(modoFormulario === "create" || modoFormulario === "role") || rol !== ROL_USUARIO;
}

function mostrarMensaje(texto, esError) {
    const box = document.getElementById("form-message");
    box.textContent = texto;
    box.className = "form-message " + (esError ? "is-error" : "is-success");
    box.hidden = false;
}

async function guardar() {
    const R = Validar.reglas;
    document.getElementById("form-message").hidden = true;

    const valido = Validar.revisar([
        { campo: "f-nombre", reglas: [R.obligatorio("El nombre completo es obligatorio.")] },
        { campo: "f-email", reglas: [R.obligatorio("El correo es obligatorio."), R.correo()] },
        {
            campo: "f-departamento",
            reglas: [(valor) => valor ? "" : (document.getElementById("f-departamento").options.length
                ? "Selecciona un departamento."
                : "No hay departamentos activos. Crea uno en la sección Departamentos.")]
        }
    ]);
    if (!valido) return;

    const nombre = document.getElementById("f-nombre").value.trim();
    const email = document.getElementById("f-email").value.trim();
    const rol = Number(document.getElementById("f-rol").value);
    const boton = document.getElementById("btn-guardar");
    UI.ocupado(boton, true, "Guardando…");

    try {
        if (modoFormulario === "create") {
            const depto = document.getElementById("campo-departamento").hidden
                ? null
                : Number(document.getElementById("f-departamento").value);

            const body = { fullName: nombre, email, roleId: rol, departmentId: depto };
            const creado = await App.api("/api/users", { method: "POST", body });

            cerrarFormulario();
            UI.aviso("Usuario creado.");
            document.getElementById("temp-password").textContent = creado.temporaryPassword;
            document.getElementById("card-temp").hidden = false;
            document.getElementById("card-temp").scrollIntoView({ behavior: "smooth" });
        } else if (modoFormulario === "edit") {
            const body = { fullName: nombre, email };
            await App.api(`/api/users/${usuarioEnEdicion.id}`, { method: "PUT", body });
            cerrarFormulario();
            UI.aviso("Usuario actualizado.");
            await cargarUsuarios();
        } else if (modoFormulario === "role") {
            const depto = document.getElementById("campo-departamento").hidden
                ? null
                : Number(document.getElementById("f-departamento").value);

            await App.api(`/api/users/${usuarioEnEdicion.id}/role`, {
                method: "PUT",
                body: { roleId: rol, departmentId: depto }
            });
            cerrarFormulario();
            UI.aviso("Rol actualizado.");
            await cargarUsuarios();
        }
    } catch (err) {
        const camposApi = { email: "f-email", fullname: "f-nombre", departmentid: "f-departamento" };
        if (!Validar.errorDeServidor(err, camposApi)) mostrarMensaje(err.message, true);
    } finally {
        UI.ocupado(boton, false);
    }
}
