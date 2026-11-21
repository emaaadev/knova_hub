
function configurarCopiaContrasena() {
    const boton = document.getElementById("btn-copy-temp");
    const contrasena = document.getElementById("temp-password");

    if (!boton || !contrasena) return;

    let temporizador;

    boton.addEventListener("click", async (evento) => {
        evento.preventDefault();
        evento.stopPropagation();

        const texto = contrasena.textContent.trim();

        if (!texto) return;

        try {
            await navigator.clipboard.writeText(texto);

            clearTimeout(temporizador);
            boton.textContent = "✓ Copiado";

            temporizador = setTimeout(() => {
                boton.textContent = "Copiar";
            }, 2000);

        } catch (error) {
            console.error("Error al copiar contraseña:", error);
            boton.textContent = "Error al copiar";

            clearTimeout(temporizador);
            temporizador = setTimeout(() => {
                boton.textContent = "Copiar";
            }, 2000);
        }
    });
}
