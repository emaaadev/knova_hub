// Utilidades compartidas del panel: sesión, navegación por rol y llamadas a la API.
const API_BASE_URL = "http://localhost:5114";

function getToken() {
    return localStorage.getItem("knovaToken");
}

function getUser() {
    try {
        return JSON.parse(localStorage.getItem("knovaUser") || "null");
    } catch {
        return null;
    }
}

function logout() {
    localStorage.removeItem("knovaToken");
    localStorage.removeItem("knovaUser");
    window.location.href = "../auth/login.html";
}

// Roles que pueden ver los módulos de gestión (Usuarios, Solicitudes y Departamentos).
const ROLES_GESTION = ["AdminEmpresa", "Supervisor"];

// Páginas del panel restringidas a esos roles.
const PAGINAS_GESTION = ["usuarios", "solicitudes", "departamentos"];

// Devuelve el usuario o redirige al login. Lanza si debe cambiar la contraseña.
function requireSession() {
    const token = getToken();
    const user = getUser();

    if (!token || !user) {
        window.location.href = "../auth/login.html";
        return null;
    }

    if (user.mustChangePassword) {
        window.location.href = "cambiar-contrasena.html";
        return null;
    }

    return { token, user };
}

// Página actual: nombre del archivo sin extensión. Sirve para marcar el nav activo.
function currentPage() {
    const match = window.location.pathname.match(/([^/]+)\.html$/);
    return match ? match[1] : "";
}

async function api(path, options = {}) {
    const token = getToken();
    const headers = { ...(options.headers || {}) };

    if (options.body !== undefined && typeof options.body !== "string") {
        headers["Content-Type"] = "application/json";
        options.body = JSON.stringify(options.body);
    }

    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }

    const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers });

    let data = null;
    try {
        data = await response.json();
    } catch {
        /* sin cuerpo */
    }

    if (!response.ok) {
        const error = new Error((data && data.message) || "No se pudo completar la operación.");
        error.status = response.status;
        error.field = data && data.field;
        throw error;
    }

    return data;
}

// Rellena el topbar (nombre y rol), oculta el nav según el rol y conecta el logout.
function setupShell() {
    const session = requireSession();
    if (!session) return null;

    const { user } = session;

    // Un Usuario no tiene módulos de gestión: lo mandamos al inicio.
    if (PAGINAS_GESTION.includes(currentPage()) && !ROLES_GESTION.includes(user.role)) {
        window.location.href = "dashboard.html";
        return null;
    }

    const nameEl = document.getElementById("user-name");
    const roleEl = document.getElementById("user-role");
    if (nameEl) nameEl.textContent = user.fullName || "";
    if (roleEl) roleEl.textContent = user.role || "";

    const logoutBtn = document.getElementById("logout");
    if (logoutBtn) logoutBtn.addEventListener("click", logout);

    // Marca el ítem activo del nav.
    document.querySelectorAll(".nav-item[data-page]").forEach((link) => {
        link.classList.toggle("is-active", link.dataset.page === currentPage());
    });

    // Muestra solo los módulos de gestión a AdminEmpresa y Supervisor.
    document.querySelectorAll("[data-role-gestion]").forEach((el) => {
        el.hidden = !ROLES_GESTION.includes(user.role);
    });

    prepararMenuMovil();

    return session;
}

// Menú hamburguesa: en pantallas pequeñas el sidebar se vuelve un panel deslizante.
function prepararMenuMovil() {
    const topbar = document.querySelector(".topbar");
    const sidebar = document.querySelector(".sidebar");
    if (!topbar || !sidebar || document.querySelector(".menu-toggle")) return;

    const toggle = document.createElement("button");
    toggle.type = "button";
    toggle.className = "menu-toggle";
    toggle.setAttribute("aria-label", "Abrir menú de navegación");
    toggle.innerHTML = "&#9776;";
    topbar.prepend(toggle);

    const backdrop = document.createElement("div");
    backdrop.className = "sidebar-backdrop";
    backdrop.hidden = true;
    document.body.appendChild(backdrop);

    const cerrar = () => {
        sidebar.classList.remove("is-open");
        backdrop.hidden = true;
    };

    toggle.addEventListener("click", () => {
        const abierto = sidebar.classList.toggle("is-open");
        backdrop.hidden = !abierto;
    });

    backdrop.addEventListener("click", cerrar);
    sidebar.querySelectorAll(".nav-item").forEach((item) => item.addEventListener("click", cerrar));
}

// Escapa HTML para insertar datos del servidor de forma segura.
function esc(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

window.App = { API_BASE_URL, getToken, getUser, requireSession, setupShell, currentPage, api, logout, esc };
