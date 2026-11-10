// Cambio de contraseña obligatorio (primer inicio de sesión con contraseña temporal).
const token = App.getToken();
const user = App.getUser();

if (!token || !user) {
    window.location.href = "../auth/login.html";
} else if (!user.mustChangePassword) {
    // Si ya no debe cambiarla, va directo al panel.
    window.location.href = "dashboard.html";
} else {
    document.getElementById("btn-guardar").addEventListener("click", cambiarContrasena);
}

async function cambiarContrasena() {
    const box = document.getElementById("form-message");
    box.hidden = true;

    const body = {
        currentPassword: document.getElementById("f-actual").value,
        newPassword: document.getElementById("f-nueva").value,
        confirmPassword: document.getElementById("f-confirmar").value
    };

    try {
        await App.api("/api/auth/change-password", { method: "POST", body });

        // La API devuelve 204: marcamos que ya no debe cambiarla y seguimos al panel.
        user.mustChangePassword = false;
        localStorage.setItem("knovaUser", JSON.stringify(user));
        window.location.href = "dashboard.html";
    } catch (err) {
        box.textContent = err.message;
        box.className = "form-message is-error";
        box.hidden = false;
    }
}
