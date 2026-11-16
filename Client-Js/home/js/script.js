// Actualizar el año del footer.
const currentYear = document.querySelector("#current-year");

if (currentYear) {
    currentYear.textContent = new Date().getFullYear();
}

//login boton para redirigir login o dashboard

function tokenVigente(token) {
    if (!token) return false;

    try {
        const payload = token.split(".")[1];

        if (!payload) return false;

        const base64 = payload
            .replace(/-/g, "+")
            .replace(/_/g, "/");

        const datos = JSON.parse(atob(base64));

        // exp contiene la fecha de vencimiento en segundos.
        if (typeof datos.exp !== "number") return false;

        return Date.now() < datos.exp * 1000;
    } catch {
        return false;
    }
}

const authNavButton = document.getElementById("authNavButton");

if (authNavButton) {
    const token = localStorage.getItem("knovaToken");
    const usuario = localStorage.getItem("knovaUser");

    if (token && usuario && tokenVigente(token)) {
        authNavButton.textContent = "Ir al panel";
        authNavButton.href = "../dashboard/dashboard.html";
    } else {
        localStorage.removeItem("knovaToken");
        localStorage.removeItem("knovaUser");

        authNavButton.textContent = "Iniciar sesión";
        authNavButton.href = "../auth/login.html";
    }
}

// Animar las tarjetas una sola vez al entrar en pantalla.
// Sin JavaScript, las tarjetas permanecen visibles.
const cards = document.querySelectorAll(".feature");
const reducedMotion = window.matchMedia(
    "(prefers-reduced-motion: reduce)"
);

if (!reducedMotion.matches && "IntersectionObserver" in window) {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;

            const card = entry.target;

            card.classList.add("is-revealing");
            observer.unobserve(card);

            // Liberar el efecto para que el hover funcione normalmente.
            card.addEventListener("animationend", () => {
                card.classList.remove("is-revealing");
            }, { once: true });
        });
    }, {
        threshold: 0.15
    });

    cards.forEach((card) => {
        observer.observe(card);
    });

    // Detener la animación si cambia la preferencia del usuario.
    reducedMotion.addEventListener("change", (event) => {
        if (!event.matches) return;

        observer.disconnect();

        cards.forEach((card) => {
            card.classList.remove("is-revealing");
        });
    });
}