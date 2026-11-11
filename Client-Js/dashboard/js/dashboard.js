const session = App.setupShell();
const $ = id => document.getElementById(id);

const contentTypes = {
    1: "Documento",
    2: "Wiki",
    3: "Minuta"
};

function renderCompany(user) {
    $("greeting").textContent = `Hola, ${user.fullName || "usuario"}`;
    $("welcome-company").textContent = user.companyName || "";
    $("welcome-rnc").textContent = user.rnc || "";

    const fields = {
        name: user.companyName,
        rnc: user.rnc,
        email: user.email,
        phone: user.phone,
        address: user.address
    };

    for (const [field, value] of Object.entries(fields)) {
        $(`company-${field}`).textContent = value || "—";
    }
}

async function configureQuickActions(role) {
    if (role !== "AdminEmpresa" && role !== "Supervisor") return;

    if (role === "Supervisor") {
        const invite = $("quick-invitar");
        if (invite) {
            invite.href = "solicitudes.html";
            invite.textContent = "Solicitar alta de usuario";
        }

        // Sin un departamento activo, el Supervisor no puede publicar contenido.
        try {
            const departments = await App.api("/api/departments");
            if (!departments.some(department => department.isActive)) return;
        } catch {
            return;
        }
    }

    document.querySelectorAll("[data-create-content]").forEach(link => {
        link.hidden = false;
    });
}

function renderActivity(activities) {
    const status = $("activity-status");
    status.hidden = activities.length > 0;
    status.textContent = "Aún no hay actividad reciente.";

    const dateFormat = new Intl.DateTimeFormat("es-DO", {
        dateStyle: "medium",
        timeStyle: "short"
    });

    const items = activities.map(activity => {
        const item = document.createElement("li");
        const link = document.createElement("a");
        const query = new URLSearchParams({ id: activity.id });

        if (activity.type !== 1) query.set("tipo", "wiki");
        link.href = `documentos.html?${query}`;
        link.textContent = activity.title;

        const time = document.createElement("time");
        time.dateTime = activity.occurredAt;
        time.textContent = dateFormat.format(new Date(activity.occurredAt));

        const type = contentTypes[activity.type];
        const feminine = activity.type !== 1;
        const verb = activity.updated ? "actualizad" : "cread";
        item.append(link, ` · ${type} ${verb}${feminine ? "a" : "o"} · `, time);
        return item;
    });

    $("recent-activity").replaceChildren(...items);
}

async function loadStats() {
    const status = $("stats-status");

    try {
        const stats = await App.api("/api/dashboard/stats");
        renderActivity(stats.recentActivity);

        for (const field of ["documents", "wikis", "minutes", "users"]) {
            $(`stat-${field}`).textContent = stats[field] ?? "—";
        }

        $("users-card").hidden = stats.users == null;
        status.hidden = true;
    } catch (error) {
        status.textContent = error.message;
        $("activity-status").textContent = "No se pudo cargar la actividad.";
    }
}

if (session) {
    renderCompany(session.user);
    configureQuickActions(session.user.role);
    loadStats();
}
