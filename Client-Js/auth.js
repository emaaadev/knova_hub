const API_BASE_URL = "http://localhost:5114";

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

function setLoading(button, loading) {
    button.disabled = loading;
    button.textContent = loading ? "Procesando…" : button.dataset.label;
}

const registerForm = document.getElementById("register-form");

if (registerForm) {
    const submitButton = registerForm.querySelector("button[type=submit]");
    submitButton.dataset.label = submitButton.textContent;

    registerForm.addEventListener("submit", async (event) => {
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

        setLoading(submitButton, true);

        try {
            const response = await fetch(`${API_BASE_URL}/api/auth/register`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    rnc,
                    companyName: nombre,
                    email,
                    phone: telefono,
                    address: direccion,
                    contactName: contacto,
                    password,
                    confirmPassword: confirmar
                })
            });

            const data = await response.json();

            if (response.ok) {
                showMessage(registerForm, "Empresa registrada. Redirigiendo al inicio de sesión…", false);
                setTimeout(() => (window.location.href = "login.html"), 1500);
            } else if (response.status === 409 && data.field === "rnc") {
                showError(registerForm.rnc, data.message);
            } else if (response.status === 409 && data.field === "email") {
                showError(registerForm.email, data.message);
            } else {
                showMessage(registerForm, data.message || "No se pudo registrar la empresa.", true);
            }
        } catch {
            showMessage(registerForm, "No se pudo conectar con el servidor.", true);
        } finally {
            setLoading(submitButton, false);
        }
    });
}

const loginForm = document.getElementById("login-form");

if (loginForm) {
    const submitButton = loginForm.querySelector("button[type=submit]");
    submitButton.dataset.label = submitButton.textContent;

    loginForm.addEventListener("submit", async (event) => {
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

        setLoading(submitButton, true);

        try {
            const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ identifier: identificador, password })
            });

            const data = await response.json();

            if (response.ok) {
                localStorage.setItem("knovaToken", data.token);
                localStorage.setItem("knovaUser", JSON.stringify(data));
                window.location.href = "dashboard.html";
            } else {
                showMessage(loginForm, data.message || "Credenciales incorrectas.", true);
            }
        } catch {
            showMessage(loginForm, "No se pudo conectar con el servidor.", true);
        } finally {
            setLoading(submitButton, false);
        }
    });
}
