
const sesionDocumentos = App.setupShell();

const $ = (id) => document.getElementById(id);

const tiposDocumento = {
    1: "Documento",
    2: "Wiki",
    3: "Minuta"
};

let documentosDisponibles = [];
let documentoActual = null;
let documentoEnEdicion = null;

const usuarioActual = sesionDocumentos?.user;
const rolActual = usuarioActual?.role || "";

const puedeCrear = ["AdminEmpresa", "Supervisor"].includes(rolActual);


function obtenerIdDepartamentoSupervisor() {
    const usuario = App.getUser();

    return Number(
        usuario?.departmentId ??
        usuario?.DepartmentId ??
        0
    );
}


function puedeModificar(documento) {
    if (!documento) return false;

    // El administrador de empresa puede administrar
    // los documentos de su empresa.
    if (rolActual === "AdminEmpresa") {
        return true;
    }

    // El Supervisor puede intentar administrar
    // documentos departamentales.
    // La API verifica si realmente supervisa
    // el departamento correspondiente.
    if (rolActual === "Supervisor") {
        return Number(documento.visibility) === 2;
    }

    // Los usuarios normales son de solo lectura.
    return false;
}



function mostrarError(mensaje) {
    $("error-documentos").textContent = mensaje;
    $("error-documentos").hidden = false;
}

function limpiarError() {
    $("error-documentos").hidden = true;
    $("error-documentos").textContent = "";
}

function mostrarMensajeFormulario(mensaje) {
    $("doc-mensaje").textContent = mensaje;
    $("doc-mensaje").hidden = false;
}

function limpiarMensajeFormulario() {
    $("doc-mensaje").textContent = "";
    $("doc-mensaje").hidden = true;
}

function obtenerLista(respuesta) {
    if (Array.isArray(respuesta)) return respuesta;
    if (Array.isArray(respuesta?.items)) return respuesta.items;
    return [];
}

function llenarSelect(id, elementos, textoInicial) {
    const select = $(id);
    select.replaceChildren();

    if (textoInicial !== null) {
        select.add(new Option(textoInicial, ""));
    }

    elementos.forEach((elemento) => {
        if (elemento.isActive === false) return;

        select.add(new Option(elemento.name, String(elemento.id)));
    });
}

async function cargarCatalogos() {
    const [categorias, etiquetas, departamentos] =
        await Promise.all([
            App.api("/api/categories"),
            App.api("/api/tags"),
            App.api("/api/departments")
        ]);

    llenarSelect(
        "doc-categoria",
        obtenerLista(categorias),
        "Seleccionar categoría"
    );

    llenarSelect(
        "doc-etiquetas",
        obtenerLista(etiquetas),
        null
    );

    llenarSelect(
        "doc-departamento",
        obtenerLista(departamentos),
        "Seleccionar departamento"
    );
}

async function cargarDocumentos() {
    limpiarError();
    $("list-subtitle").textContent = "Cargando documentos…";

    try {
        const respuesta = await App.api("/api/documents");

        if (!Array.isArray(respuesta) &&
            !Array.isArray(respuesta?.items)) {
            throw new Error("Formato de respuesta inesperado.");
        }

        documentosDisponibles = obtenerLista(respuesta);
        filtrarDocumentos();
    } catch (error) {
        documentosDisponibles = [];
        filtrarDocumentos();
        mostrarError(error.message);
    }
}

function filtrarDocumentos() {
    const texto = $("buscar-documentos")
        .value.trim().toLowerCase();

    const tipo = $("filtrar-tipo").value;

    const filtrados = documentosDisponibles.filter((documento) => {
        const contenido = [
            documento.title,
            documento.description,
            documento.categoryName
        ].join(" ").toLowerCase();

        return contenido.includes(texto) &&
            (!tipo || String(documento.type) === tipo);
    });

    $("list-subtitle").textContent =
        `${filtrados.length} documento(s) visibles de ` +
        `${documentosDisponibles.length}.`;

    $("vacio-documentos").hidden = filtrados.length > 0;


    $("cuerpo-documentos").innerHTML = filtrados.map((d) => {

        const esSupervisor = rolActual === "Supervisor";

        const editable = puedeModificar(d);

        const soloLectura = esSupervisor && !editable;

        const estiloFila = soloLectura
            ? 'style="background-color: #f1f3f5; opacity: 0.8;"'
            : "";

        const estado = esSupervisor
            ? editable
                ? '<span style="color: #167347; font-weight: 600;">● Editable</span>'
                : '<span style="color: #667085; font-weight: 600;">● Solo lectura</span>'
            : "";

        return `
        <tr ${estiloFila}>

            <td>
                <strong>${App.esc(d.title)}</strong>

                ${estado
                ? `<div style="margin-top: 5px; font-size: 12px;">
                           ${estado}
                       </div>`
                : ""}
            </td>

            <td>
                ${App.esc(tiposDocumento[d.type] || "Desconocido")}
            </td>

            <td>
                ${d.visibility === 1 ? "General" : "Departamento"}
            </td>

            <td>
                ${App.esc(d.categoryName || "Sin categoría")}
            </td>

            <td>
                ${App.esc(d.authorName || "—")}
            </td>

            <td>
                <button
                    type="button"
                    class="btn btn-sm"
                    data-documento="${Number(d.id)}">
                    Ver
                </button>
            </td>

        </tr>
    `;
    }).join("");

}

async function verDocumento(id) {
    limpiarError();

    try {
        const documento = await App.api(`/api/documents/${id}`);
        documentoActual = documento;

        actualizarControlesArchivo(documento);

        $("detalle-titulo").textContent =
            documento.title || "Sin título";

        $("detalle-descripcion").textContent =
            documento.description || "";

        $("detalle-metadata").textContent =
            `${tiposDocumento[documento.type] || "Contenido"} · ` +
            `${documento.visibility === 1 ? "General" : "Departamento"} · ` +
            `${documento.categoryName || "Sin categoría"}` +
            (documento.departmentName
                ? ` · ${documento.departmentName}`
                : "");

        $("detalle-contenido").textContent =
            documento.content || "Sin contenido de texto.";

        $("detalle-archivo").textContent = documento.hasFile
            ? `Archivo adjunto: ${documento.originalFileName || "Archivo"}`
            : "Sin archivo adjunto.";

        const permitido = puedeModificar(documento);

        $("detalle-editar").hidden = !permitido;
        $("detalle-eliminar").hidden = !permitido;

        $("form-documento").hidden = true;
        $("detalle-documento").hidden = false;

        $("detalle-documento").scrollIntoView({
            behavior: "smooth",
            block: "start"
        });
    } catch (error) {
        mostrarError(error.message);
    }
}

function actualizarVisibilidad() {
    const esGeneral = $("doc-visibilidad").value === "1";
    const departamento = $("doc-departamento");

    departamento.disabled = esGeneral;
    departamento.required = !esGeneral;

    if (esGeneral) {
        departamento.value = "";
    }

    $("doc-aviso").textContent = esGeneral
        ? "Los documentos generales no tienen departamento."
        : "Selecciona el departamento al que pertenece el documento.";
}

async function abrirFormulario(documento = null) {
    limpiarMensajeFormulario();
    limpiarError();

    try {
        await cargarCatalogos();
    } catch (error) {
        mostrarError(
            "No se pudieron cargar los catálogos: " + error.message
        );
        return;
    }

    documentoEnEdicion = documento;

    $("form-documento-campos").reset();

    $("form-documento-titulo").textContent = documento
        ? "Editar documento"
        : "Nuevo documento";

    if (documento) {
        $("doc-titulo").value = documento.title || "";
        $("doc-descripcion").value = documento.description || "";
        $("doc-contenido").value = documento.content || "";
        $("doc-tipo").value = String(documento.type);
        $("doc-visibilidad").value = String(documento.visibility);

        $("doc-categoria").value =
            String(documento.categoryId ?? "");

        $("doc-departamento").value =
            String(documento.departmentId ?? "");

        const etiquetas = new Set(
            (documento.tags || []).map((etiqueta) =>
                String(etiqueta.id)
            )
        );

        Array.from($("doc-etiquetas").options).forEach((opcion) => {
            opcion.selected = etiquetas.has(opcion.value);
        });

    } else {

        $("doc-tipo").value = "1";

        if (rolActual === "Supervisor") {

            $("doc-visibilidad").value = "2";

            $("doc-departamento").value =
                String(obtenerIdDepartamentoSupervisor());

        } else {

            $("doc-visibilidad").value = "1";

        }
    }

    // El Supervisor no puede cambiar a visibilidad General.
    $("doc-visibilidad").disabled =
        rolActual === "Supervisor";


    actualizarVisibilidad();

    $("detalle-documento").hidden = true;
    $("form-documento").hidden = false;

    $("form-documento").scrollIntoView({
        behavior: "smooth",
        block: "start"
    });
}

function cerrarFormulario() {
    $("form-documento").hidden = true;
    documentoEnEdicion = null;
    limpiarMensajeFormulario();
}

function obtenerDatosFormulario() {
    const visibilidad = Number($("doc-visibilidad").value);

    const departamentoId = $("doc-departamento").value
        ? Number($("doc-departamento").value)
        : null;

    const categoriaId = $("doc-categoria").value
        ? Number($("doc-categoria").value)
        : null;

    const tagIds = Array.from(
        $("doc-etiquetas").selectedOptions
    ).map((opcion) => Number(opcion.value));

    return {
        title: $("doc-titulo").value.trim(),
        description: $("doc-descripcion").value.trim(),
        content: $("doc-contenido").value.trim(),
        type: Number($("doc-tipo").value),
        visibility: visibilidad,
        categoryId: categoriaId,
        departmentId: visibilidad === 1 ? null : departamentoId,
        tagIds: tagIds
    };
}

async function guardarDocumento(evento) {
    evento.preventDefault();

    limpiarMensajeFormulario();

    const datos = obtenerDatosFormulario();

    if (!datos.title) {
        mostrarMensajeFormulario("El título es obligatorio.");
        return;
    }

    if (!datos.categoryId) {
        mostrarMensajeFormulario("Selecciona una categoría.");
        return;
    }

    if (datos.visibility === 2 && !datos.departmentId) {
        mostrarMensajeFormulario(
            "Selecciona un departamento para este documento."
        );
        return;
    }

    const boton = $("doc-guardar");
    boton.disabled = true;
    boton.textContent = "Guardando…";

    try {
        const editando = documentoEnEdicion !== null;

        const ruta = editando
            ? `/api/documents/${documentoEnEdicion.id}`
            : "/api/documents";

        const metodo = editando ? "PUT" : "POST";

        await App.api(ruta, {
            method: metodo,
            body: datos
        });

        cerrarFormulario();
        $("detalle-documento").hidden = true;
        documentoActual = null;

        await cargarDocumentos();

        window.alert(
            editando
                ? "Documento actualizado correctamente."
                : "Documento creado correctamente."
        );
    } catch (error) {
        mostrarMensajeFormulario(error.message);
    } finally {
        boton.disabled = false;
        boton.textContent = "Guardar";
    }
}

async function eliminarDocumento() {
    if (!documentoActual) return;

    if (!puedeModificar(documentoActual)) {
        mostrarError("No tienes permiso para eliminar este documento.");
        return;
    }

    if (documentoActual.hasFile) {
        window.alert(
            "Este documento tiene un archivo adjunto. " +
            "Primero debes eliminar el archivo adjunto."
        );
        return;
    }

    const confirmado = window.confirm(
        `¿Seguro que deseas eliminar "${documentoActual.title}"? ` +
        "Esta acción no se puede deshacer."
    );

    if (!confirmado) return;

    try {
        await App.api(`/api/documents/${documentoActual.id}`, {
            method: "DELETE"
        });

        documentoActual = null;
        $("detalle-documento").hidden = true;

        await cargarDocumentos();

        window.alert("Documento eliminado correctamente.");
    } catch (error) {
        mostrarError(error.message);
    }
}

async function iniciarDocumentos() {
    const tipoInicial = new URLSearchParams(
        window.location.search
    ).get("tipo");

    if (tipoInicial === "wiki") {
        $("filtrar-tipo").value = "2";
    }

    $("btn-nuevo-documento").hidden = !puedeCrear;
    $("btn-categorias").hidden = rolActual !== "AdminEmpresa";



    $("btn-nuevo-documento").addEventListener("click", async () => {

        if (rolActual === "Supervisor") {
            try {
                const respuesta = await App.api("/api/departments");
                const departamentos = obtenerLista(respuesta);

                if (departamentos.length === 0) {
                    window.alert(
                        "No hay departamentos disponibles.\n\n" +
                        "Solicita al administrador que verifique " +
                        "tu asignación como Supervisor."
                    );
                    return;
                }
            } catch (error) {
                mostrarError(
                    "No se pudieron consultar los departamentos: " +
                    error.message
                );
                return;
            }
        }

        abrirFormulario();
    });



    $("btn-recargar").addEventListener(
        "click",
        cargarDocumentos
    );

    $("buscar-documentos").addEventListener(
        "input",
        filtrarDocumentos
    );

    $("filtrar-tipo").addEventListener(
        "change",
        filtrarDocumentos
    );

    $("doc-visibilidad").addEventListener(
        "change",
        actualizarVisibilidad
    );

    $("form-documento-campos").addEventListener(
        "submit",
        guardarDocumento
    );

    $("doc-cancelar").addEventListener(
        "click",
        cerrarFormulario
    );

    $("cerrar-detalle").addEventListener("click", () => {
        $("detalle-documento").hidden = true;
    });

    $("detalle-editar").addEventListener("click", () => {
        if (documentoActual && puedeModificar(documentoActual)) {
            abrirFormulario(documentoActual);
        }
    });

    $("detalle-eliminar").addEventListener(
        "click",
        eliminarDocumento
    );

    $("cuerpo-documentos").addEventListener(
        "click",
        async (evento) => {
            const boton = evento.target.closest(
                "button[data-documento]"
            );

            if (boton) {
                await verDocumento(Number(boton.dataset.documento));
            }
        }
    );

    await cargarDocumentos();
}

if (sesionDocumentos) {
    iniciarDocumentos();
}


// ========================================
// GESTIÓN DE ARCHIVOS ADJUNTOS
// ========================================

const FORMATOS_PERMITIDOS = [".pdf", ".docx", ".txt", ".md"];
const TAMANO_MAXIMO = 20 * 1024 * 1024;

// Actualiza los controles según el documento y los permisos.
function actualizarControlesArchivo(documento) {
    const tieneArchivo = Boolean(documento.hasFile);
    const autorizado = puedeModificar(documento);

    $("archivo-informacion").textContent = tieneArchivo
        ? `Archivo: ${documento.originalFileName || "Adjunto"}`
        : "Este documento no tiene archivos adjuntos.";

    $("archivo-subida").hidden = !autorizado;
    $("btn-subir-archivo").hidden = !autorizado;

    // Descargar está permitido para quienes pueden leer el documento.
    $("btn-descargar-archivo").hidden = !tieneArchivo;

    $("btn-eliminar-archivo").hidden =
        !tieneArchivo || !autorizado;

    $("archivo-mensaje").hidden = true;
    $("archivo-documento").value = "";
}

function mensajeArchivo(texto, esError = false) {
    const elemento = $("archivo-mensaje");

    elemento.textContent = texto;
    elemento.hidden = false;
    elemento.style.color = esError ? "#b42318" : "#167347";
}

// Solicitud autenticada para operaciones con archivos.
// No usamos App.api porque convierte los objetos a JSON.
async function solicitarArchivo(ruta, opciones = {}) {
    const token = App.getToken();

    const headers = {
        ...(opciones.headers || {}),
        Authorization: `Bearer ${token}`
    };

    const respuesta = await fetch(
        `${App.API_BASE_URL}${ruta}`,
        {
            ...opciones,
            headers
        }
    );

    if (!respuesta.ok) {
        let mensaje = `Error HTTP ${respuesta.status}`;

        try {
            const datos = await respuesta.json();
            mensaje = datos.message || mensaje;
        } catch {
            // El servidor no devolvió JSON.
        }

        throw new Error(mensaje);
    }

    return respuesta;
}

async function subirArchivoDocumento() {
    if (!documentoActual) return;

    const input = $("archivo-documento");
    const archivo = input.files[0];

    if (!archivo) {
        mensajeArchivo("Selecciona un archivo.", true);
        return;
    }

    const extension = "." + archivo.name.split(".").pop().toLowerCase();

    if (!FORMATOS_PERMITIDOS.includes(extension)) {
        mensajeArchivo("Formato de archivo no permitido.", true);
        return;
    }

    if (archivo.size === 0 || archivo.size > TAMANO_MAXIMO) {
        mensajeArchivo(
            "El archivo debe tener contenido y no superar los 20 MB.",
            true
        );
        return;
    }

    const boton = $("btn-subir-archivo");
    boton.disabled = true;
    boton.textContent = "Subiendo…";

    try {
        const datos = new FormData();

        // El campo debe coincidir con el parámetro del endpoint.
        datos.append("file", archivo, archivo.name);

        const respuesta = await solicitarArchivo(
            `/api/documents/${documentoActual.id}/file`,
            {
                method: "POST",
                body: datos
            }
        );

        const actualizado = await respuesta.json();

        documentoActual = actualizado;
        actualizarControlesArchivo(actualizado);

        mensajeArchivo("Archivo subido correctamente.");

        await cargarDocumentos();
    } catch (error) {
        mensajeArchivo(error.message, true);
    } finally {
        boton.disabled = false;
        boton.textContent = "Subir archivo";
    }
}

async function descargarArchivoDocumento() {
    if (!documentoActual?.hasFile) return;

    const boton = $("btn-descargar-archivo");
    boton.disabled = true;

    try {
        const respuesta = await solicitarArchivo(
            `/api/documents/${documentoActual.id}/file`,
            { method: "GET" }
        );

        const blob = await respuesta.blob();
        const url = URL.createObjectURL(blob);

        const enlace = document.createElement("a");
        enlace.href = url;
        enlace.download =
            documentoActual.originalFileName || "documento";

        document.body.appendChild(enlace);
        enlace.click();
        enlace.remove();

        setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) {
        mensajeArchivo(error.message, true);
    } finally {
        boton.disabled = false;
    }
}

async function eliminarArchivoDocumento() {
    if (!documentoActual?.hasFile) return;

    const confirmado = window.confirm(
        "¿Deseas eliminar el archivo adjunto? " +
        "El documento permanecerá registrado."
    );

    if (!confirmado) return;

    const boton = $("btn-eliminar-archivo");
    boton.disabled = true;

    try {
        await solicitarArchivo(
            `/api/documents/${documentoActual.id}/file`,
            { method: "DELETE" }
        );

        // Consultar el estado actualizado después del 204.
        const actualizado = await App.api(
            `/api/documents/${documentoActual.id}`
        );

        documentoActual = actualizado;
        actualizarControlesArchivo(actualizado);

        mensajeArchivo("Archivo eliminado correctamente.");

        await cargarDocumentos();
    } catch (error) {
        mensajeArchivo(error.message, true);
    } finally {
        boton.disabled = false;
    }
}

// Eventos de los botones
$("btn-subir-archivo").addEventListener(
    "click",
    subirArchivoDocumento
);

$("btn-descargar-archivo").addEventListener(
    "click",
    descargarArchivoDocumento
);

$("btn-eliminar-archivo").addEventListener(
    "click",
    eliminarArchivoDocumento
);
