// Modo oscuro del panel.
// Se carga en el <head> SIN defer para aplicar el tema guardado antes de pintar la página (sin parpadeo).
// Modo claro = sin atributo data-tema, así el CSS original queda intacto.
(function () {
    const CLAVE = "knovaTema";
    const raiz = document.documentElement;

    const ICONO_LUNA =
        '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" ' +
        'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">' +
        '<path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>';

    const ICONO_SOL =
        '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" ' +
        'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">' +
        '<circle cx="12" cy="12" r="5"/>' +
        '<line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/>' +
        '<line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>' +
        '<line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/>' +
        '<line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/></svg>';

    function guardado() {
        try {
            return localStorage.getItem(CLAVE) === "oscuro" ? "oscuro" : "claro";
        } catch {
            return "claro";
        }
    }

    function actual() {
        return raiz.dataset.tema === "oscuro" ? "oscuro" : "claro";
    }

    // En modo oscuro la barra lateral usa la variante clara del logo.
    function actualizarLogo() {
        const oscuro = actual() === "oscuro";
        document.querySelectorAll(".sidebar-logo img").forEach((img) => {
            const base = (img.getAttribute("src") || "").replace("logo-claro.png", "logo.png");
            img.setAttribute("src", oscuro ? base.replace("logo.png", "logo-claro.png") : base);
        });
    }

    function actualizarBoton() {
        const boton = document.getElementById("theme-toggle");
        if (!boton) return;
        const oscuro = actual() === "oscuro";
        const etiqueta = oscuro ? "Activar modo claro" : "Activar modo oscuro";
        boton.innerHTML = oscuro ? ICONO_SOL : ICONO_LUNA;
        boton.setAttribute("aria-label", etiqueta);
        boton.title = etiqueta;
    }

    function aplicar(tema) {
        if (tema === "oscuro") raiz.dataset.tema = "oscuro";
        else delete raiz.dataset.tema;
        actualizarLogo();
        actualizarBoton();
    }

    // Lo llama App.renderAccount() cuando ya hay sesión: el botón nunca aparece en login ni registro.
    function montarBoton() {
        const menu = document.querySelector(".user-menu");
        if (!menu || document.getElementById("theme-toggle")) return;

        const boton = document.createElement("button");
        boton.type = "button";
        boton.id = "theme-toggle";
        boton.className = "theme-toggle";
        boton.addEventListener("click", () => {
            const nuevo = actual() === "oscuro" ? "claro" : "oscuro";
            try { localStorage.setItem(CLAVE, nuevo); } catch {}
            aplicar(nuevo);
        });

        menu.prepend(boton);
        actualizarBoton();
    }

    // Aplica el tema guardado de inmediato.
    if (guardado() === "oscuro") raiz.dataset.tema = "oscuro";

    // El logo está en el <body>: se ajusta cuando el DOM está listo.
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", actualizarLogo);
    } else {
        actualizarLogo();
    }

    // Si se cambia el tema en otra pestaña, esta también se actualiza.
    window.addEventListener("storage", (evento) => {
        if (evento.key === CLAVE) aplicar(guardado());
    });

    window.Tema = { montarBoton };
})();
