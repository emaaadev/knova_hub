// Utilidades de interfaz del panel: avisos (toast), diálogo de confirmación,
// botones ocupados, filas de estado (cargando / vacío / error) y tablas legibles en móvil.
(function () {
    // Avisos
    function contenedorAvisos() {
        let contenedor = document.querySelector(".toast-container");
        if (!contenedor) {
            contenedor = document.createElement("div");
            contenedor.className = "toast-container";
            contenedor.setAttribute("role", "status");
            contenedor.setAttribute("aria-live", "polite");
            document.body.appendChild(contenedor);
        }
        return contenedor;
    }

    // tipo: "ok" | "error". Los avisos de error duran más para dar tiempo a leerlos.
    function aviso(mensaje, tipo = "ok", titulo) {
        const toast = document.createElement("div");
        toast.className = "toast" + (tipo === "error" ? " error" : "");

        const icono = document.createElement("div");
        icono.className = "toast-icono";
        icono.setAttribute("aria-hidden", "true");
        icono.textContent = tipo === "error" ? "!" : "✓";

        const contenido = document.createElement("div");
        contenido.className = "toast-contenido";
        const tituloEl = document.createElement("div");
        tituloEl.className = "toast-titulo";
        tituloEl.textContent = titulo || (tipo === "error" ? "No se pudo completar" : "Listo");
        const mensajeEl = document.createElement("div");
        mensajeEl.className = "toast-mensaje";
        mensajeEl.textContent = mensaje;
        contenido.append(tituloEl, mensajeEl);

        const cerrar = document.createElement("button");
        cerrar.type = "button";
        cerrar.className = "toast-cerrar";
        cerrar.setAttribute("aria-label", "Cerrar aviso");
        cerrar.innerHTML = "&times;";

        const quitar = () => toast.remove();
        cerrar.addEventListener("click", quitar);
        setTimeout(quitar, tipo === "error" ? 7000 : 4500);

        toast.append(icono, contenido, cerrar);
        contenedorAvisos().appendChild(toast);
    }

    // Confirmación
    // Devuelve una promesa: true si el usuario acepta; false si cancela, pulsa Escape o hace clic fuera.
    let secuenciaDialogos = 0;

    function confirmar({ titulo = "Confirmar", mensaje = "", aceptar = "Aceptar", cancelar = "Cancelar", peligro = false } = {}) {
        const dialogo = document.createElement("dialog");
        if (typeof dialogo.showModal !== "function") {
            return Promise.resolve(window.confirm(mensaje || titulo));
        }

        const id = `dialogo-confirmar-${++secuenciaDialogos}`;
        dialogo.className = "confirm-dialog";
        dialogo.setAttribute("aria-labelledby", `${id}-titulo`);
        dialogo.setAttribute("aria-describedby", `${id}-mensaje`);

        const cuerpo = document.createElement("div");
        cuerpo.className = "confirm-body";
        const tituloEl = document.createElement("h2");
        tituloEl.id = `${id}-titulo`;
        tituloEl.textContent = titulo;
        const mensajeEl = document.createElement("p");
        mensajeEl.id = `${id}-mensaje`;
        mensajeEl.textContent = mensaje;

        const acciones = document.createElement("div");
        acciones.className = "confirm-actions";
        const botonCancelar = document.createElement("button");
        botonCancelar.type = "button";
        botonCancelar.className = "btn";
        botonCancelar.dataset.confirmar = "cancelar";
        botonCancelar.textContent = cancelar;
        const botonAceptar = document.createElement("button");
        botonAceptar.type = "button";
        botonAceptar.className = "btn " + (peligro ? "btn-danger-solid" : "btn-primary");
        botonAceptar.dataset.confirmar = "aceptar";
        botonAceptar.textContent = aceptar;

        acciones.append(botonCancelar, botonAceptar);
        cuerpo.append(tituloEl, mensajeEl, acciones);
        dialogo.appendChild(cuerpo);

        return new Promise((resolve) => {
            let aceptado = false;
            botonAceptar.addEventListener("click", () => { aceptado = true; dialogo.close(); });
            botonCancelar.addEventListener("click", () => dialogo.close());
            // Clic en el fondo oscuro (el propio <dialog>, no su contenido) cancela.
            dialogo.addEventListener("click", (evento) => { if (evento.target === dialogo) dialogo.close(); });
            dialogo.addEventListener("close", () => { dialogo.remove(); resolve(aceptado); });

            document.body.appendChild(dialogo);
            dialogo.showModal();
            // En acciones destructivas el foco empieza en «Cancelar».
            (peligro ? botonCancelar : botonAceptar).focus();
        });
    }

    function ocupado(boton, activo, texto) {
        if (!boton) return;
        if (activo) {
            boton.dataset.etiqueta = boton.textContent;
            boton.dataset.deshabilitado = String(boton.disabled);
            boton.disabled = true;
            boton.classList.add("is-loading");
            boton.setAttribute("aria-busy", "true");
            if (texto) boton.textContent = texto;
        } else {
            if (boton.dataset.etiqueta !== undefined) boton.textContent = boton.dataset.etiqueta;
            boton.disabled = boton.dataset.deshabilitado === "true";
            boton.classList.remove("is-loading");
            boton.removeAttribute("aria-busy");
            delete boton.dataset.etiqueta;
            delete boton.dataset.deshabilitado;
        }
    }

    function columnasVisibles(tabla) {
        return Math.max(1, tabla.querySelectorAll("thead th:not([hidden])").length);
    }

    // tipo: "cargando" | "vacio" | "error". Con reintentar se agrega un botón «Reintentar».
    function filaMensaje(cuerpo, texto, tipo = "vacio", reintentar) {
        const fila = document.createElement("tr");
        fila.className = "estado-fila";
        const celda = document.createElement("td");
        celda.className = `estado-celda is-${tipo}`;
        celda.colSpan = columnasVisibles(cuerpo.closest("table"));

        if (tipo === "cargando") {
            const spinner = document.createElement("span");
            spinner.className = "spinner";
            spinner.setAttribute("aria-hidden", "true");
            celda.appendChild(spinner);
        }
        celda.append(document.createTextNode(texto));

        if (reintentar) {
            const boton = document.createElement("button");
            boton.type = "button";
            boton.className = "btn btn-sm";
            boton.textContent = "Reintentar";
            boton.addEventListener("click", reintentar);
            celda.append(" ", boton);
        }

        fila.appendChild(celda);
        cuerpo.replaceChildren(fila);
    }

    // Tablas en móvil
    // Copia el encabezado de cada columna al atributo data-label de sus celdas (ver ui.css).
    function etiquetarTabla(tabla) {
        const encabezados = [...tabla.querySelectorAll("thead th")]
            .filter((th) => !th.hidden)
            .map((th) => th.textContent.trim());

        tabla.querySelectorAll("tbody tr").forEach((fila) => {
            [...fila.children].forEach((celda, indice) => {
                if (celda.tagName !== "TD" || celda.colSpan > 1) return;
                if (encabezados[indice]) celda.dataset.label = encabezados[indice];
                celda.classList.toggle("celda-acciones", !!celda.querySelector(".row-actions"));
            });
        });
    }

    function prepararTablas() {
        document.querySelectorAll(".table").forEach((tabla) => {
            const cuerpo = tabla.tBodies[0];
            if (!cuerpo) return;
            etiquetarTabla(tabla);
            new MutationObserver(() => etiquetarTabla(tabla)).observe(cuerpo, { childList: true });
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", prepararTablas);
    } else {
        prepararTablas();
    }

    window.UI = { aviso, confirmar, ocupado, filaMensaje };
})();
