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


function tokenVigente(token) {
    if (!token) return false;

    try {
        const partes = token.split(".");
        if (partes.length !== 3) return false;

        const base64 = partes[1]
            .replace(/-/g, "+")
            .replace(/_/g, "/");

        const payload = JSON.parse(atob(base64));

        if (typeof payload.exp !== "number") {
            return false;
        }

        return Date.now() < payload.exp * 1000;
    } catch {
        return false;
    }
}


function logout() {
    localStorage.removeItem("knovaToken");
    localStorage.removeItem("knovaUser");
    try { sessionStorage.removeItem("knovaAiHistory"); } catch {}
    window.location.href = "../auth/login.html";
}

function manejarSesionVencida() {
    // Evitar múltiples alertas si varias peticiones
    if (window.sesionVencidaEnProceso) return;

    window.sesionVencidaEnProceso = true;

    // Eliminar los datos de autenticación.
    localStorage.removeItem("knovaToken");
    localStorage.removeItem("knovaUser");

    // Mostrar alerta y esperar a que el usuario
    // presione Aceptar.
    alert(
        "Tu sesión ha expirado.\n\n" +
        "Por seguridad, debes iniciar sesión nuevamente."
    );

    // Redirigir al login después de aceptar.
    window.location.replace("../auth/login.html?sesion=expirada");
}



// Roles que pueden ver los módulos de gestión (Usuarios, Solicitudes y Departamentos).
const ROLES_GESTION = ["AdminEmpresa", "Supervisor"];
const ROLE_LABELS = {
    AdminEmpresa: "Administrador de empresa",
    AdminPlataforma: "Administrador de plataforma",
    Supervisor: "Supervisor",
    Usuario: "Usuario"
};

// Páginas del panel restringidas a esos roles.
const PAGINAS_GESTION = ["usuarios", "solicitudes", "departamentos"];

// Devuelve el usuario o redirige al login. Lanza si debe cambiar la contraseña.

function requireSession() {
    const token = getToken();
    const user = getUser();

    // No existe una sesión guardada.
    if (!token || !user) {
        localStorage.removeItem("knovaToken");
        localStorage.removeItem("knovaUser");

        window.location.replace("../auth/login.html");
        return null;
    }

    // Existe un token, pero ya venció.
    if (!tokenVigente(token)) {
        manejarSesionVencida();
        return null;
    }

    // El usuario debe cambiar su contraseña.
    if (user.mustChangePassword) {
        window.location.replace("cambiar-contrasena.html");
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
    const { body, asBlob, omitirSesionVencida, ...request } = options;
    const token = getToken();
    const headers = { ...request.headers };
    let payload = body;

    if (body !== undefined && typeof body !== "string" && !(body instanceof FormData)) {
        headers["Content-Type"] = "application/json";
        payload = JSON.stringify(body);
    }
    if (token) headers["Authorization"] = `Bearer ${token}`;

    let response;
    try {
        response = await fetch(`${API_BASE_URL}${path}`, { ...request, headers, body: payload });
    } catch (error) {
        if (error.name === "AbortError") throw error;
        throw new Error("No se pudo conectar con la API. Intenta de nuevo.");
    }

    if (response.status === 401 && !omitirSesionVencida) {
        manejarSesionVencida();

        const error = new Error(
            "Tu sesión ha expirado. Inicia sesión nuevamente."
        );
        error.status = 401;
        throw error;
    }


    if (response.ok && asBlob) return response.blob();

    if (response.status >= 500) {
        const error = new Error("La API no pudo completar la operación. Intenta de nuevo.");
        error.status = response.status;
        throw error;
    }

    const contentType = response.headers.get("Content-Type") || "";
    const data = contentType.includes("json")
        ? await response.json().catch(() => null)
        : contentType.startsWith("text/plain") ? await response.text() : null;

    if (!response.ok) {
        const validation = Object.values(data?.errors || {}).flat().join(" ");
        const fallback = {
            401: "La sesión venció. Inicia sesión de nuevo.",
            403: "No tienes permiso para realizar esta operación.",
            404: "El recurso no está disponible.",
            413: "El archivo supera el límite permitido.",
            429: "Alcanzaste el límite de consultas a la IA. Intenta más tarde.",
        };
        const message = typeof data === "string" ? data : data?.message || validation;
        const error = new Error(message || fallback[response.status] || "No se pudo completar la operación.");
        error.status = response.status;
        error.field = data?.field;
        throw error;
    }

    return data;
}



function setupShell() {
    const session = requireSession();
    if (!session) return null;

    const { user } = session;
    const page = currentPage();
    const canManage = ROLES_GESTION.includes(user.role);

    if (PAGINAS_GESTION.includes(page) && !canManage) {
        window.location.href = "dashboard.html";
        return null;
    }

    renderAccount(user);
    setupNavigation(page, user.role, canManage);
    prepararMenuMovil();

    return session;
}

function renderAccount(user) {
    const name = document.getElementById("user-name");
    const role = document.getElementById("user-role");
    if (name) name.textContent = user.fullName || "";
    if (role) {
        role.textContent = ROLE_LABELS[user.role] || user.role || "";
        role.hidden = !role.textContent;
    }
    document.getElementById("logout")?.addEventListener("click", logout);
    document.getElementById("logout")?.addEventListener("click", logout);
    window.Tema?.montarBoton();
}

function setupNavigation(page, role, canManage) {
    const wikiLink = document.querySelector('.nav-item[href="documentos.html?tipo=wiki"]');
    if (wikiLink) {
        wikiLink.dataset.page = "wikis";
        if (role === "AdminEmpresa") addCatalogLinks(wikiLink);
    }

    const isWiki = page === "documentos" &&
        new URLSearchParams(location.search).get("tipo") === "wiki";
    const activePage = isWiki ? "wikis" : page;

    document.querySelectorAll(".nav-item[data-page]").forEach(link => {
        const active = link.dataset.page === activePage;
        link.classList.toggle("is-active", active);
        if (active) link.setAttribute("aria-current", "page");
        else link.removeAttribute("aria-current");
    });

    document.querySelectorAll("[data-role-gestion]").forEach(element => {
        element.hidden = !canManage;
    });
}

function addCatalogLinks(wikiLink) {
    const nextLink = wikiLink.nextElementSibling;
    for (const [page, title] of [["categorias", "Categorías"], ["etiquetas", "Etiquetas"]]) {
        const link = document.createElement("a");
        link.className = "nav-item";
        link.dataset.page = page;
        link.href = `${page}.html`;
        link.textContent = title;
        wikiLink.parentElement.insertBefore(link, nextLink);
    }
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

function esc(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}
function renderAiText(container, text){
    container.replaceChildren();
    let list = null;
    for (const rawLine of String(text ?? "").split(/\r?\n/)) {
        let line = rawLine.trim();
        if (!line) { list = null; continue; }

        const istItem = /^([-*•]|\d+\.)\s+/.test(line);
        const isHeading = /^#{1,6}\s+/.test(line);
        line = line.replace (/^#{1,6}\s+/, "").replace(/^([-*•])\s+/, "");
        
        const block = document.createElement(istItem ? "li" : "p");
        appendInline(block, line, isHeading);

        if (istItem) {
            if (!list) { list = document.createElement("ul"); container.append(list); }
            list.append(block);
        } else {
            list = null;
            container.append(block);
        }
    }
}

function appendInline(parent, line, forceBold) {
    line.split(/\*\*(.+?)\*\*/g).forEach((part, index) => {
        const clean = part.replace(/[*_`]/g, "");
        if (!clean) return;
        if (index % 2 === 1 || forceBold) {
            const strong = document.createElement("strong");
            strong.textContent = clean;
            parent.append(strong);
        } else {
            parent.append(document.createTextNode(clean));
        }
    });

}

function documentLink(id, type) {
    const query = new URLSearchParams({ id });
    if (type === 2 || type === 3) query.set("tipo", "wiki");
    return `documentos.html?${query}`;
}
// Escapa HTML para insertar datos del servidor de forma segura.

window.App = { API_BASE_URL, getToken, getUser, requireSession, setupShell, currentPage, api, logout, esc, renderAiText, documentLink };
