// Actualizar el año del footer.
const currentYear = document.querySelector("#current-year");

if (currentYear) {
    currentYear.textContent = new Date().getFullYear();
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