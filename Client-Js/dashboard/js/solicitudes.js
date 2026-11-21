// Módulo Solicitudes: el Supervisor solicita el alta; el AdminEmpresa aprueba o rechaza.
const ETIQUETAS_ESTADO = {
    Pending: { texto: "Pendiente", clase: "badge-warn" },
    Approved: { texto: "Aprobada", clase: "badge-ok" },
    Rejected: { texto: "Rechazada", clase: "badge-danger" }
};

let usuarioActual = null;

const session = App.setupShell();
if (!session) {
    // Redirige al login o a cambiar contraseña.
} else {
    usuarioActual = session.user;
    init();
}

async function init() {
    const esAdmin = usuarioActual.role === "AdminEmpresa";

    document.getElementById("btn-nueva").hidden = usuarioActual.role !== "Supervisor";
    document.getElementById("col-solicitado").hidden = !esAdmin;
    document.getElementById("col-acciones").hidden = !esAdmin;

    document.getElementById("titulo-solicitudes").textContent = esAdmin
        ? "Solicitudes de alta"
        : "Mis solicitudes de alta";

    document.getElementById("btn-nueva").addEventListener("click", abrirFormulario);
    document.getElementById("btn-cancelar").addEventListener("click", () => {
        document.getElementById("card-form").hidden = true;
    });
    document.getElementById("btn-enviar").addEventListener("click", enviarSolicitud);
    configurarCopiaContrasena();
    document.getElementById("btn-temp-ok").addEventListener("click", () => {
        document.getElementById("card-temp").hidden = true;
        cargarSolicitudes();
    });

    if (usuarioActual.role === "Supervisor") {
        await cargarDepartamentos();
    }

    await cargarSolicitudes();
}

async function cargarDepartamentos() {
    const select = document.getElementById("f-departamento");
    const aviso = document.getElementById("aviso-sin-deptos");
    const btnEnviar = document.getElementById("btn-enviar");

    try {
        const departamentos = await App.api("/api/departments");
        const activos = departamentos.filter((d) => d.isActive);
        select.innerHTML = activos
            .map((d) => `<option value="${d.id}">${App.esc(d.name)}</option>`)
            .join("");

        // Sin departamentos activos no se puede solicitar: avisamos y bloqueamos el envío.
        const sinDeptos = activos.length === 0;
        if (aviso) aviso.hidden = !sinDeptos;
        if (btnEnviar) btnEnviar.disabled = sinDeptos;
        select.disabled = sinDeptos;
    } catch {
        select.innerHTML = "";
        if (aviso) aviso.hidden = false;
        if (btnEnviar) btnEnviar.disabled = true;
        select.disabled = true;
    }
}

async function cargarSolicitudes() {
    const cuerpo = document.getElementById("cuerpo-solicitudes");
    const vacio = document.getElementById("vacio-solicitudes");
    const subtitulo = document.getElementById("list-subtitle");

    UI.filaMensaje(cuerpo, "Cargando solicitudes…", "cargando");
    vacio.hidden = true;

    let solicitudes;
    try {
        solicitudes = await App.api("/api/users/requests");
    } catch (err) {
        subtitulo.textContent = "No se pudieron cargar las solicitudes.";
        UI.filaMensaje(cuerpo, `${err.message}`, "error", cargarSolicitudes);
        return;
    }
    cuerpo.innerHTML = "";

    subtitulo.textContent = usuarioActual.role === "AdminEmpresa"
        ? `${solicitudes.filter((s) => s.status === "Pending").length} pendiente(s) de ${solicitudes.length} solicitud(es).`
        : `Has hecho ${solicitudes.length} solicitud(es).`;

    if (solicitudes.length === 0) {
        vacio.hidden = false;
        return;
    }

    const esAdmin = usuarioActual.role === "AdminEmpresa";

    cuerpo.innerHTML = solicitudes.map((s) => {
        const estado = ETIQUETAS_ESTADO[s.status] || { texto: s.status, clase: "badge-muted" };

        let acciones = "";
        if (esAdmin && s.status === "Pending") {
            acciones = `
                <div class="row-actions">
                    <button class="btn btn-sm btn-primary" data-accion="aprobar" data-id="${s.id}">Aprobar</button>
                    <button class="btn btn-sm btn-danger" data-accion="rechazar" data-id="${s.id}">Rechazar</button>
                </div>`;
        }

        const celdaSolicitadoPor = esAdmin ? `<td>${App.esc(s.requestedByName || "—")}</td>` : "";
        const celdaAcciones = esAdmin ? `<td>${acciones || "—"}</td>` : "";

        return `
            <tr>
                <td>${App.esc(s.fullName)}</td>
                <td>${App.esc(s.email)}</td>
                <td>${App.esc(s.departmentName || "—")}</td>
                ${celdaSolicitadoPor}
                <td><span class="badge ${estado.clase}">${estado.texto}</span></td>
                <td>${App.esc(new Date(s.createdAt).toLocaleDateString("es-DO"))}</td>
                ${celdaAcciones}
            </tr>`;
    }).join("");

    cuerpo.querySelectorAll("[data-accion]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = Number(btn.dataset.id);
            if (btn.dataset.accion === "aprobar") aprobar(id, btn);
            else rechazar(id, btn);
        });
    });
}

function abrirFormulario() {
    Validar.limpiar(document.getElementById("card-form"));
    document.getElementById("f-nombre").value = "";
    document.getElementById("f-email").value = "";
    document.getElementById("form-message").hidden = true;
    document.getElementById("card-form").hidden = false;
    document.getElementById("card-form").scrollIntoView({ behavior: "smooth" });
}

function mostrarMensaje(texto, esError) {
    const box = document.getElementById("form-message");
    box.textContent = texto;
    box.className = "form-message " + (esError ? "is-error" : "is-success");
    box.hidden = false;
}

async function enviarSolicitud() {
    const R = Validar.reglas;
    document.getElementById("form-message").hidden = true;

    const valido = Validar.revisar([
        { campo: "f-nombre", reglas: [R.obligatorio("El nombre completo es obligatorio.")] },
        { campo: "f-email", reglas: [R.obligatorio("El correo es obligatorio."), R.correo()] },
        { campo: "f-departamento", reglas: [R.obligatorio("Selecciona un departamento.")] }
    ]);
    if (!valido) return;

    const body = {
        fullName: document.getElementById("f-nombre").value.trim(),
        email: document.getElementById("f-email").value.trim(),
        departmentId: Number(document.getElementById("f-departamento").value)
    };

    const boton = document.getElementById("btn-enviar");
    UI.ocupado(boton, true, "Enviando…");

    try {
        await App.api("/api/users/requests", { method: "POST", body });
        document.getElementById("card-form").hidden = true;
        UI.aviso("Solicitud enviada. Queda pendiente de aprobación.");
        await cargarSolicitudes();
    } catch (err) {
        const camposApi = { email: "f-email", fullname: "f-nombre", departmentid: "f-departamento" };
        if (!Validar.errorDeServidor(err, camposApi)) mostrarMensaje(err.message, true);
    } finally {
        UI.ocupado(boton, false);
    }
}

async function aprobar(id, boton) {
    const confirmado = await UI.confirmar({
        titulo: "Aprobar solicitud",
        mensaje: "Se creará el usuario y se generará una contraseña temporal.",
        aceptar: "Aprobar"
    });
    if (!confirmado) return;

    UI.ocupado(boton, true, "Aprobando…");
    try {
        const aprobada = await App.api(`/api/users/requests/${id}/approve`, { method: "POST" });
        document.getElementById("temp-password").textContent = aprobada.temporaryPassword;
        document.getElementById("card-temp").hidden = false;
        document.getElementById("card-temp").scrollIntoView({ behavior: "smooth" });
        UI.aviso("Solicitud aprobada. Usuario creado.");
        await cargarSolicitudes();
    } catch (err) {
        UI.aviso(err.message, "error");
    } finally {
        UI.ocupado(boton, false);
    }
}

async function rechazar(id, boton) {
    const confirmado = await UI.confirmar({
        titulo: "Rechazar solicitud",
        mensaje: "La solicitud quedará rechazada y no se creará el usuario.",
        aceptar: "Rechazar",
        peligro: true
    });
    if (!confirmado) return;

    UI.ocupado(boton, true, "Rechazando…");
    try {
        await App.api(`/api/users/requests/${id}/reject`, { method: "POST" });
        UI.aviso("Solicitud rechazada.");
        await cargarSolicitudes();
    } catch (err) {
        UI.aviso(err.message, "error");
    } finally {
        UI.ocupado(boton, false);
    }
}
