
// ========================================
// KNOVA HUB - GESTIÓN DE CATEGORÍAS
// ========================================

const sesionCategorias = App.setupShell();

const $ = (id) => document.getElementById(id);

const usuarioActual = App.getUser();

const rolActual = usuarioActual?.role || "";

const puedeAdministrar = rolActual === "AdminEmpresa";

let categoriasDisponibles = [];
let categoriaEnEdicion = null;


// ========================================
// MENSAJES
// ========================================

function mostrarError(mensaje) {
    $("error-categorias").textContent = mensaje;
    $("error-categorias").hidden = false;
}

function limpiarError() {
    $("error-categorias").textContent = "";
    $("error-categorias").hidden = true;
}

function mostrarMensajeFormulario(mensaje) {
    $("mensaje-formulario").textContent = mensaje;
    $("mensaje-formulario").hidden = false;
}

function limpiarMensajeFormulario() {
    $("mensaje-formulario").textContent = "";
    $("mensaje-formulario").hidden = true;
}


// ========================================
// LISTAR CATEGORÍAS
// ========================================

async function cargarCategorias() {
    limpiarError();

    $("conteo-categorias").textContent =
        "Cargando categorías…";

    try {
        const respuesta = await App.api("/api/categories");

        if (Array.isArray(respuesta)) {
            categoriasDisponibles = respuesta;
        } else if (Array.isArray(respuesta?.items)) {
            categoriasDisponibles = respuesta.items;
        } else {
            throw new Error(
                "La API devolvió un formato inesperado."
            );
        }

        filtrarCategorias();

    } catch (error) {

        categoriasDisponibles = [];

        filtrarCategorias();

        mostrarError(
            error.message || "No se pudieron cargar las categorías."
        );
    }
}


// ========================================
// FILTRAR CATEGORÍAS
// ========================================


function filtrarCategorias() {

    const busqueda = $("buscar-categoria")
        .value.trim().toLowerCase();

    const filtradas = categoriasDisponibles.filter(
        (categoria) =>
            (categoria.name || "")
                .toLowerCase()
                .includes(busqueda)
    );

    $("conteo-categorias").textContent =
        `${filtradas.length} categoría(s) de ` +
        `${categoriasDisponibles.length}.`;

    $("vacio-categorias").hidden = filtradas.length > 0;

    $("cuerpo-categorias").innerHTML = filtradas.map(
        (categoria) => {

            const estaActiva = categoria.isActive !== false;

            const estado = estaActiva
                ? `<span style="color:#15803d;font-weight:600;">
                       ● Activa
                   </span>`
                : `<span style="color:#9ca3af;font-weight:600;">
                       ● Inactiva
                   </span>`;

            const botonesAcciones = puedeAdministrar
                ? `
                    <div style="display:flex;gap:8px;flex-wrap:wrap;">

                        <button
                            type="button"
                            class="btn btn-sm"
                            data-editar="${Number(categoria.id)}">
                            Editar
                        </button>

                        ${estaActiva ? `
                            <button
                                type="button"
                                class="btn btn-sm btn-eliminar-categoria"
                                data-eliminar="${Number(categoria.id)}">
                                Eliminar
                            </button>
                        ` : ""}

                    </div>
                  `
                : '<span>Solo lectura</span>';

            return `
                <tr style="${!estaActiva ? 'opacity:0.65;' : ''}">

                    <td>${Number(categoria.id)}</td>

                    <td>
                        <strong>${App.esc(categoria.name)}</strong>
                    </td>

                    <td>${estado}</td>

                    <td>${botonesAcciones}</td>

                </tr>
            `;
        }
    ).join("");
}



// ========================================
// ABRIR FORMULARIO
// ========================================

function abrirFormulario(categoria = null) {

    if (!puedeAdministrar) {
        mostrarError(
            "No tienes permiso para administrar categorías."
        );
        return;
    }

    categoriaEnEdicion = categoria;

    $("form-categoria").reset();

    limpiarMensajeFormulario();

    if (categoria) {

        $("titulo-formulario").textContent =
            "Editar categoría";

        $("categoria-nombre").value =
            categoria.name || "";

    } else {

        $("titulo-formulario").textContent =
            "Nueva categoría";
    }

    $("seccion-formulario").hidden = false;

    $("seccion-formulario").scrollIntoView({
        behavior: "smooth",
        block: "start"
    });

    $("categoria-nombre").focus();
}


// ========================================
// CERRAR FORMULARIO
// ========================================

function cerrarFormulario() {

    $("seccion-formulario").hidden = true;

    $("form-categoria").reset();

    categoriaEnEdicion = null;

    limpiarMensajeFormulario();
}




function mostrarNotificacion(mensaje, tipo = "success") {

    let contenedor = document.querySelector(
        ".knova-toast-container"
    );

    if (!contenedor) {
        contenedor = document.createElement("div");
        contenedor.className = "knova-toast-container";
        contenedor.setAttribute("aria-live", "polite");
        document.body.appendChild(contenedor);
    }

    const toast = document.createElement("div");
    toast.className = tipo === "error"
        ? "knova-toast error"
        : "knova-toast";

    const icono = document.createElement("span");
    icono.className = "knova-toast-icono";
    icono.textContent = tipo === "error" ? "✕" : "✓";

    const contenido = document.createElement("div");
    contenido.className = "knova-toast-contenido";

    const titulo = document.createElement("div");
    titulo.className = "knova-toast-titulo";
    titulo.textContent = tipo === "error"
        ? "Ocurrió un error"
        : "¡Operación exitosa!";

    const descripcion = document.createElement("div");
    descripcion.className = "knova-toast-mensaje";
    descripcion.textContent = mensaje;

    const cerrar = document.createElement("button");
    cerrar.type = "button";
    cerrar.className = "knova-toast-cerrar";
    cerrar.textContent = "×";
    cerrar.setAttribute("aria-label", "Cerrar notificación");

    contenido.append(titulo, descripcion);
    toast.append(icono, contenido, cerrar);
    contenedor.appendChild(toast);

    const temporizador = setTimeout(() => {
        toast.remove();
        if (!contenedor.children.length) {
            contenedor.remove();
        }
    }, 4500);

    cerrar.addEventListener("click", () => {
        clearTimeout(temporizador);
        toast.remove();

        if (!contenedor.children.length) {
            contenedor.remove();
        }
    });
}



// ========================================
// GUARDAR CATEGORÍA
// ========================================

async function guardarCategoria(evento) {

    evento.preventDefault();

    if (!puedeAdministrar) {
        mostrarMensajeFormulario(
            "No tienes permiso para realizar esta operación."
        );
        return;
    }

    limpiarMensajeFormulario();

    const nombre = $("categoria-nombre").value.trim();

    if (!nombre) {
        mostrarMensajeFormulario(
            "Debes introducir el nombre de la categoría."
        );
        return;
    }

    const boton = $("btn-guardar");

    boton.disabled = true;
    boton.textContent = "Guardando…";

    try {

        const editando = categoriaEnEdicion !== null;

        const ruta = editando
            ? `/api/categories/${categoriaEnEdicion.id}`
            : "/api/categories";

        const metodo = editando ? "PUT" : "POST";

        await App.api(ruta, {
            method: metodo,
            body: {
                name: nombre
            }
        });

        cerrarFormulario();

        await cargarCategorias();

        mostrarNotificacion(
            editando
                ? "La categoría se actualizó correctamente."
                : "La categoría se creó correctamente."
        );

    } catch (error) {

        mostrarMensajeFormulario(
            error.message ||
            "No se pudo guardar la categoría."
        );

    } finally {

        boton.disabled = false;
        boton.textContent = "Guardar";
    }
}



async function eliminarCategoria(categoria) {

    if (!puedeAdministrar) {
        mostrarNotificacion(
            "No tienes permiso para eliminar categorías.",
            "error"
        );
        return;
    }

    const confirmado = window.confirm(
        `¿Deseas desactivar la categoría "${categoria.name}"?\n\n` +
        "La categoría dejará de estar disponible para " +
        "nuevos documentos, pero los documentos existentes " +
        "conservarán su categoría."
    );

    if (!confirmado) return;

    try {

        await App.api(`/api/categories/${categoria.id}`, {
            method: "DELETE"
        });

        await cargarCategorias();

        mostrarNotificacion(
            `La categoría "${categoria.name}" fue desactivada correctamente.`
        );

    } catch (error) {

        mostrarNotificacion(
            error.message ||
            "No se pudo desactivar la categoría.",
            "error"
        );
    }
}



// ========================================
// EVENTOS
// ========================================

function iniciarCategorias() {

    $("btn-nueva-categoria").hidden =
        !puedeAdministrar;

    $("btn-nueva-categoria").addEventListener(
        "click",
        () => abrirFormulario()
    );

    $("btn-recargar").addEventListener(
        "click",
        cargarCategorias
    );

    $("buscar-categoria").addEventListener(
        "input",
        filtrarCategorias
    );

    $("form-categoria").addEventListener(
        "submit",
        guardarCategoria
    );

    $("btn-cancelar").addEventListener(
        "click",
        cerrarFormulario
    );


    $("cuerpo-categorias").addEventListener(
        "click",
        async (evento) => {

            const botonEditar = evento.target.closest(
                "button[data-editar]"
            );

            const botonEliminar = evento.target.closest(
                "button[data-eliminar]"
            );

            if (botonEditar) {

                const id = Number(botonEditar.dataset.editar);

                const categoria = categoriasDisponibles.find(
                    (item) => Number(item.id) === id
                );

                if (categoria) {
                    abrirFormulario(categoria);
                }

                return;
            }

            if (botonEliminar) {

                const id = Number(botonEliminar.dataset.eliminar);

                const categoria = categoriasDisponibles.find(
                    (item) => Number(item.id) === id
                );

                if (categoria) {
                    await eliminarCategoria(categoria);
                }
            }
        }
    );


    cargarCategorias();
}


// ========================================
// INICIAR
// ========================================

if (sesionCategorias) {
    iniciarCategorias();
}
