// Cambio de contraseña obligatorio (primer inicio de sesión con contraseña temporal).
const token = App.getToken();
const user = App.getUser();

if (!token || !user) {
    window.location.href = "../auth/login.html";
} else if (!user.mustChangePassword) {
    // Si ya no debe cambiarla, va directo al panel.
    window.location.href = "dashboard.html";
} else {
    document.getElementById("password-form").addEventListener("submit", cambiarContrasena);
}

async function cambiarContrasena(event) {
    event.preventDefault();
    const R = Validar.reglas;
    const box = document.getElementById("form-message");
    box.hidden = true;


    const valido = Validar.revisar([
        {campo: "f-actual", reglas: [R.obligatorio("Ingresa tu contraseña actual.")] },
        {
            campo: "f-nueva",
            reglas: [
                R.obligatorio("Ingresa la nueva contraseña."),
                R.contrasenaSegura(),
                R.distintaDe("f-actual", "La nueva contraseña debe ser diferente de la actual.")
            ]
        },
        {
            campo: "f-confirmar",
            reglas: [
                 R.obligatorio("Confirma la nueva contraseña."),
                R.igualA("f-nueva", "Las contraseñas no coinciden.")
            ]
        }
    ]);
    if (!valido) return;

    const body = {
        currentPassword: document.getElementById("f-actual").value,
        newPassword: document.getElementById("f-nueva").value,
        confirmPassword: document.getElementById("f-confirmar").value
    };

    const boton = document.getElementById("btn-guardar");
    UI.ocupado(boton, true, "Guardando…");

    try {
        // omitirSesionVencida: aquí un 401 significa «contraseña actual incorrecta», no sesión vencida.
        await App.api("/api/auth/change-password", { method: "POST", body, omitirSesionVencida: true });

        // La API devuelve 204: marcamos que ya no debe cambiarla y seguimos al panel.
        user.mustChangePassword = false;
        localStorage.setItem("knovaUser", JSON.stringify(user));
        window.location.href = "dashboard.html";
    } catch (err) {
        const camposApi = {
            currentpassword: "f-actual",
            newpassword: "f-nueva",
            confirmpassword: "f-confirmar"
        };
        if (err.status === 401) {
            Validar.mostrar(document.getElementById("f-actual"), "La contraseña actual no es correcta.");
            document.getElementById("f-actual").focus();
        } else if (!Validar.errorDeServidor(err, camposApi)) {
            box.textContent = err.message;
            box.className = "form-message is-error";
            box.hidden = false;
        }
        UI.ocupado(boton, false);
    }
}
