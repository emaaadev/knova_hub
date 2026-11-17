// Validación de formularios del panel: reglas reutilizables y un mensaje debajo de cada campo.
// Si hay errores, quien llama no envía la petición. Los campos ocultos o deshabilitados se omiten.
(function () {
    const CORREO = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
    const EXTENSIONES = /\.(pdf|docx|txt|md)$/i;
    const LIMITE_ARCHIVO = 20 * 1024 * 1024; // 20 MB, igual que la API.

    // Cada regla recibe el valor del campo y devuelve "" si es válido o el mensaje de error.
    const texto = (valor) => String(valor ?? "").trim();

    const reglas = {
        obligatorio: (mensaje = "Este campo es obligatorio.") => (valor) => (texto(valor) ? "" : mensaje),

        maximo: (limite, mensaje) => (valor) =>
            texto(valor).length > limite ? mensaje || `No puede superar los ${limite} caracteres.` : "",

        correo: (mensaje = "Ingresa un correo válido.") => (valor) =>
            !texto(valor) || CORREO.test(texto(valor)) ? "" : mensaje,

        // Misma política que muestra el registro: mínimo 8 caracteres, un número y un carácter especial.
        contrasenaSegura: (mensaje = "Mínimo 8 caracteres, un número y un carácter especial.") => (valor) =>
            !valor || (valor.length >= 8 && /\d/.test(valor) && /[^A-Za-z0-9]/.test(valor)) ? "" : mensaje,

        igualA: (idOtro, mensaje) => (valor) =>
            valor === document.getElementById(idOtro).value ? "" : mensaje,

        distintaDe: (idOtro, mensaje) => (valor) =>
            !valor || valor !== document.getElementById(idOtro).value ? "" : mensaje,

        archivo: () => (archivo) => {
            if (!archivo) return "";
            if (!EXTENSIONES.test(archivo.name)) return "Formato no permitido. Usa PDF, DOCX, TXT o MD.";
            if (archivo.size === 0) return "El archivo está vacío.";
            if (archivo.size > LIMITE_ARCHIVO) return "El archivo supera el máximo de 20 MB.";
            return "";
        }
    };

    // Mostrar y limpiar errores
    function contenedorDe(campo) {
        return campo.closest(".field") || campo.parentElement;
    }

    function limpiarCampo(campo) {
        const error = document.getElementById(`${campo.id}-error`);
        if (error) {
            const restantes = (campo.getAttribute("aria-describedby") || "")
                .split(/\s+/).filter((id) => id && id !== error.id);
            if (restantes.length) campo.setAttribute("aria-describedby", restantes.join(" "));
            else campo.removeAttribute("aria-describedby");
            error.remove();
        }
        campo.removeAttribute("aria-invalid");
        campo.classList.remove("has-error");
    }

    function mostrar(campo, mensaje) {
        limpiarCampo(campo);
        const error = document.createElement("p");
        error.className = "field-error";
        error.id = `${campo.id}-error`;
        error.textContent = mensaje;
        contenedorDe(campo).appendChild(error);

        campo.setAttribute("aria-invalid", "true");
        campo.classList.add("has-error");
        const descripcion = (campo.getAttribute("aria-describedby") || "").trim();
        campo.setAttribute("aria-describedby", descripcion ? `${descripcion} ${error.id}` : error.id);
    }

    function limpiar(contenedor) {
        contenedor.querySelectorAll("[aria-invalid]").forEach(limpiarCampo);
        contenedor.querySelectorAll(".field-error").forEach((error) => error.remove());
    }

    function revisar(esquema) {
        let primero = null;

        for (const { campo, reglas: lista = [] } of esquema) {
            const elemento = typeof campo === "string" ? document.getElementById(campo) : campo;
            if (!elemento) continue;
            limpiarCampo(elemento);
            if (elemento.disabled || elemento.closest("[hidden]")) continue;

            const valor = elemento.type === "file" ? elemento.files[0] : elemento.value;
            for (const regla of lista) {
                const mensaje = regla(valor);
                if (mensaje) {
                    mostrar(elemento, mensaje);
                    primero ||= elemento;
                    break;
                }
            }
        }

        primero?.focus();
        return !primero;
    }

    // Si la API responde con el campo que falló (error.field), el mensaje se muestra en ese campo.
    // mapa: { nombreDelCampoEnLaApi(en minúsculas): "id-del-input" }. Devuelve true si lo pudo mostrar.
    function errorDeServidor(error, mapa = {}) {
        const id = mapa[String(error?.field || "").toLowerCase()];
        const campo = id && document.getElementById(id);
        if (!campo || campo.closest("[hidden]")) return false;
        mostrar(campo, error.message);
        campo.focus();
        return true;
    }

    // El error de un campo desaparece en cuanto la persona lo corrige.
    const alCorregir = (evento) => {
        if (evento.target.matches?.("[aria-invalid]")) limpiarCampo(evento.target);
    };
    document.addEventListener("input", alCorregir);
    document.addEventListener("change", alCorregir);

    window.Validar = { reglas, revisar, mostrar, limpiar, limpiarCampo, errorDeServidor, LIMITE_ARCHIVO };
})();
