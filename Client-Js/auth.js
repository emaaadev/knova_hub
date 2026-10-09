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

function showError(input, message) {
    const field = input.closest(".field");
    const existing = field.querySelector(".field-error");

    if (existing) existing.remove();

    const error = document.createElement("p");
    error.className = "field-error";
    error.textContent = message;
    field.appendChild(error);
    input.classList.add("has-error");
}

function clearErrors(form) {
    form.querySelectorAll(".field-error").forEach((el) => el.remove());
    form.querySelectorAll(".has-error").forEach((el) => el.classList.remove("has-error"));
}

function showMessage(form, message, isError) {
    const box = form.querySelector(".form-message");
    box.textContent = message;
    box.classList.toggle("is-error", isError);
    box.hidden = false;
}

const registerForm = document.getElementById("register-form");

if (registerForm) {
    registerForm.addEventListener("submit", (event) => {
        event.preventDefault();
        clearErrors(registerForm);

        const rnc = registerForm.rnc.value.trim();
        const nombre = registerForm.nombre.value.trim();
        const email = registerForm.email.value.trim();
        const telefono = registerForm.telefono.value.trim();
        const direccion = registerForm.direccion.value.trim();
        const contacto = registerForm.contacto.value.trim();
        const password = registerForm.password.value;
        const confirmar = registerForm.confirmar.value;

        let valid = true;

        if (!/^\d{9}$|^\d{11}$/.test(rnc)) {
            showError(registerForm.rnc, "El RNC debe tener 9 u 11 dígitos.");
            valid = false;
        }
        if (!nombre) {
            showError(registerForm.nombre, "El nombre de la empresa es obligatorio.");
            valid = false;
        }
        if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
            showError(registerForm.email, "Ingresa un correo válido.");
            valid = false;
        }
        if (!telefono) {
            showError(registerForm.telefono, "El teléfono es obligatorio.");
            valid = false;
        }
        if (!direccion) {
            showError(registerForm.direccion, "La dirección es obligatoria.");
            valid = false;
        }
        if (!contacto) {
            showError(registerForm.contacto, "El contacto principal es obligatorio.");
            valid = false;
        }
        if (password.length < 8 || !/\d/.test(password) || !/[^A-Za-z0-9]/.test(password)) {
            showError(registerForm.password, "Mínimo 8 caracteres, un número y un carácter especial.");
            valid = false;
        }
        if (password !== confirmar) {
            showError(registerForm.confirmar, "Las contraseñas no coinciden.");
            valid = false;
        }

        if (!valid) return;

        showMessage(registerForm, "Registro completado correctamente.", false);
    });
}

const loginForm = document.getElementById("login-form");

if (loginForm) {
    loginForm.addEventListener("submit", (event) => {
        event.preventDefault();
        clearErrors(loginForm);

        const identificador = loginForm.identificador.value.trim();
        const password = loginForm.password.value;

        let valid = true;

        if (!identificador) {
            showError(loginForm.identificador, "Ingresa tu RNC o correo.");
            valid = false;
        }
        if (!password) {
            showError(loginForm.password, "La contraseña es obligatoria.");
            valid = false;
        }

        if (!valid) return;

        showMessage(loginForm, "Inicio de sesión exitoso.", false);
    });
}
