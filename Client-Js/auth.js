const toggles = document.querySelectorAll(".password-toggle");

toggles.forEach((toggle) => {
    toggle.addEventListener("click", () => {
        const input = document.getElementById(toggle.dataset.toggle);

        if (!input) return;

        const isPassword = input.type === "password";
        input.type = isPassword ? "text" : "password";

        toggle.textContent = isPassword ? "Ocultar" : "Mostrar";
        toggle.setAttribute(
            "aria-label",
            isPassword ? "Ocultar contraseña" : "Mostrar contraseña"
        );
    });
});
