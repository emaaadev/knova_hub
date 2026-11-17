// Módulo Departamentos: el AdminEmpresa los crea/edita/activa y asigna supervisores;
// el Supervisor solo ve los suyos.
let usuarioActual = null;
let modoFormulario = null; // 'create' | 'edit'
let departamentoEnEdicion = null;
let todosDepartamentos = [];
let supervisores = [];

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
    document.getElementById("col-acciones").hidden = !esAdmin;
    document.getElementById("card-supervisores").hidden = !esAdmin;

    document.getElementById("btn-nuevo").addEventListener("click", () => abrirFormulario("create"));
    document.getElementById("btn-cancelar").addEventListener("click", () => {
        document.getElementById("card-form").hidden = true;
    });
    document.getElementById("btn-guardar").addEventListener("click", guardarDepartamento);
    document.getElementById("s-supervisor").addEventListener("change", cargarAsignacion);
    document.getElementById("s-guardar").addEventListener("click", guardarAsignacion);

    await cargarDepartamentos();

    if (esAdmin) {
        await cargarSupervisores();
    }
}

async function cargarDepartamentos() {
    const cuerpo = document.getElementById("cuerpo-departamentos");
    const vacio = document.getElementById("vacio-departamentos");
    const subtitulo = document.getElementById("list-subtitle");

    UI.filaMensaje(cuerpo, "Cargando departamentos…", "cargando");
    vacio.hidden = true;

    try {
        todosDepartamentos = await App.api("/api/departments");
    } catch (err) {
        subtitulo.textContent = "No se pudieron cargar los departamentos.";
        UI.filaMensaje(cuerpo, `${err.message}`, "error", cargarDepartamentos);
        return;
    }
    cuerpo.innerHTML = "";

    subtitulo.textContent = usuarioActual.role === "AdminEmpresa"
        ? `${todosDepartamentos.length} departamento(s) en tu empresa.`
        : `Supervisas ${todosDepartamentos.length} departamento(s).`;

    if (todosDepartamentos.length === 0) {
        vacio.hidden = false;
        return;
    }

    const esAdmin = usuarioActual.role === "AdminEmpresa";

    cuerpo.innerHTML = todosDepartamentos.map((d) => {
        const estado = d.isActive
            ? '<span class="badge badge-ok">Activo</span>'
            : '<span class="badge badge-muted">Inactivo</span>';

        let acciones = "";
        if (esAdmin) {
            acciones = `
                <div class="row-actions">
                    <button class="btn btn-sm" data-accion="editar" data-id="${d.id}">Editar</button>
                    <button class="btn btn-sm ${d.isActive ? "btn-danger" : ""}" data-accion="activo" data-id="${d.id}" data-activo="${!d.isActive}">
                        ${d.isActive ? "Desactivar" : "Activar"}
                    </button>
                </div>`;
        }

        const celdaAcciones = esAdmin ? `<td>${acciones || "—"}</td>` : "";

        return `
            <tr>
                <td>${App.esc(d.name)}</td>
                <td>${estado}</td>
                ${celdaAcciones}
            </tr>`;
    }).join("");

    cuerpo.querySelectorAll("[data-accion]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = Number(btn.dataset.id);
            const depto = todosDepartamentos.find((x) => x.id === id);
            if (btn.dataset.accion === "editar") abrirFormulario("edit", depto);
            else cambiarEstado(depto, btn.dataset.activo === "true");
        });
    });
}

async function cambiarEstado(depto, isActive) {
    if (!isActive) {
        const confirmado = await UI.confirmar({
            titulo: "Desactivar departamento",
            mensaje: `¿Desactivar «${depto.name}»? Podrás volver a activarlo después.`,
            aceptar: "Desactivar",
            peligro: true
        });
        if (!confirmado) return;
    }

    try {
        await App.api(`/api/departments/${depto.id}`, { method: "PUT", body: { name: depto.name, isActive } });
        UI.aviso(isActive ? "Departamento activado." : "Departamento desactivado.");
        await cargarDepartamentos();
    } catch (err) {
        UI.aviso(err.message, "error");
    }
}

function abrirFormulario(modo, depto = null) {
    modoFormulario = modo;
    departamentoEnEdicion = depto;

    const card = document.getElementById("card-form");
    card.hidden = false;
    card.scrollIntoView({ behavior: "smooth" });

    Validar.limpiar(card);
    document.getElementById("form-titulo").textContent = modo === "create" ? "Crear departamento" : "Editar departamento";
    document.getElementById("form-message").hidden = true;
    document.getElementById("campo-activo").hidden = modo === "create";

    document.getElementById("f-nombre").value = modo === "edit" ? (depto.name || "") : "";
    if (modo === "edit") {
        document.getElementById("f-activo").value = String(depto.isActive);
    }
}

function mostrarMensaje(boxId, texto, esError) {
    const box = document.getElementById(boxId);
    box.textContent = texto;
    box.className = "form-message " + (esError ? "is-error" : "is-success");
    box.hidden = false;
}

async function guardarDepartamento() {
    const R = Validar.reglas;
    document.getElementById("form-message").hidden = true;

    if (!Validar.revisar([
        { campo: "f-nombre", reglas: [R.obligatorio("El nombre del departamento es obligatorio.")] }
    ])) return;

    const nombre = document.getElementById("f-nombre").value.trim();
    const boton = document.getElementById("btn-guardar");
    UI.ocupado(boton, true, "Guardando…");

    try {
        if (modoFormulario === "create") {
            await App.api("/api/departments", { method: "POST", body: { name: nombre } });
        } else {
            const isActive = document.getElementById("f-activo").value === "true";
            await App.api(`/api/departments/${departamentoEnEdicion.id}`, {
                method: "PUT",
                body: { name: nombre, isActive }
            });
        }
        document.getElementById("card-form").hidden = true;
        UI.aviso(modoFormulario === "create" ? "Departamento creado." : "Departamento actualizado.");
        await cargarDepartamentos();
    } catch (err) {
        // 409: ya existe un departamento con ese nombre en la empresa.
        if (err.status === 409) Validar.mostrar(document.getElementById("f-nombre"), err.message);
        else mostrarMensaje("form-message", err.message, true);
    } finally {
        UI.ocupado(boton, false);
    }
}

// ---------- Asignación de supervisores ----------

async function cargarSupervisores() {
    try {
        const usuarios = await App.api("/api/users");
        supervisores = usuarios.filter((u) => u.role === "Supervisor");

        const select = document.getElementById("s-supervisor");
        select.innerHTML = supervisores
            .map((s) => `<option value="${s.id}">${App.esc(s.fullName)}</option>`)
            .join("");

        const sinSupervisores = supervisores.length === 0;
        document.getElementById("s-guardar").disabled = sinSupervisores;
        if (sinSupervisores) {
            document.getElementById("s-departamentos").innerHTML =
                '<p class="empty-state">No hay supervisores en tu empresa. Crea un usuario con rol Supervisor para asignarle departamentos.</p>';
        } else {
            await cargarAsignacion();
        }
    } catch {
        supervisores = [];
        document.getElementById("s-guardar").disabled = true;
        document.getElementById("s-departamentos").innerHTML =
            '<p class="empty-state">No se pudieron cargar los supervisores.</p>';
    }
}

async function cargarAsignacion() {
    const contenedor = document.getElementById("s-departamentos");
    contenedor.innerHTML = "";

    const supervisorId = Number(document.getElementById("s-supervisor").value);
    if (!supervisorId) return;

    let asignados = [];
    try {
        asignados = await App.api(`/api/supervisors/${supervisorId}/departments`);
    } catch {
        /* sin asignaciones */
    }

    const idsAsignados = new Set(asignados.map((d) => d.id));

    contenedor.innerHTML = todosDepartamentos
        .filter((d) => d.isActive)
        .map((d) => `
            <label style="display:flex; align-items:center; gap:8px; padding:6px 0; font-size:14px; color:var(--heading);">
                <input type="checkbox" value="${d.id}" ${idsAsignados.has(d.id) ? "checked" : ""}>
                ${App.esc(d.name)}
            </label>`)
        .join("");
}

async function guardarAsignacion() {
    document.getElementById("s-message").hidden = true;

    const supervisorId = Number(document.getElementById("s-supervisor").value);
    if (!supervisorId) {
        mostrarMensaje("s-message", "Selecciona un supervisor.", true);
        return;
    }

    const departmentIds = Array.from(
        document.querySelectorAll("#s-departamentos input:checked")
    ).map((el) => Number(el.value));

    const boton = document.getElementById("s-guardar");
    UI.ocupado(boton, true, "Guardando…");

    try {
        await App.api(`/api/supervisors/${supervisorId}/departments`, {
            method: "PUT",
            body: { departmentIds }
        });
        mostrarMensaje("s-message", "Asignación guardada.", false);
        UI.aviso("Asignación de departamentos guardada.");
    } catch (err) {
        mostrarMensaje("s-message", err.message, true);
    } finally {
        UI.ocupado(boton, false);
    }
}
