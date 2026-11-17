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

async function loadRecommendations() {
    const status = $("rec-status");
    try {
        const items = await App.api("/api/documents/recommendations");
        status.hidden = items.length > 0;
        status.textContent = "Aún no hay recomendaciones. Abre algunos documentos o haz una consulta.";
        $("recommendations").replaceChildren(...items.map(item => {
            const li = document.createElement("li");
            const a = document.createElement("a");
            a.href = App.documentLink(item.id, item.type);
            a.textContent = item.title;
            li.append(a, ` · ${contentTypes[item.type] || "Documento"}`);
            return li;
        }));
    } catch (error) {
        status.hidden = false;
        status.textContent = `No se pudieron cargar las recomendaciones. ${error.message}`;
    }
}
const HISTORY_KEY = "knovaAiHistory";

function readHistory() {
    try { return JSON.parse(sessionStorage.getItem(HISTORY_KEY) || "[]"); }
    catch { return []; }
}

function saveHistory(history) {
    try { sessionStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(0, 20))); } catch {}
}

function showAiMessage(id, text = "") {
    $(id).textContent = text;
    $(id).hidden = !text;
}

function renderAnswer(entry) {
    App.renderAiText($("ai-answer-text"), entry.answer || "La IA no devolvió una respuesta para esta pregunta.");
    const sources = entry.sources || [];
    $("ai-no-sources").hidden = sources.length > 0;
    $("ai-sources").replaceChildren(...sources.map(source => {
        const li = document.createElement("li");
        const a = document.createElement("a");
        a.href = App.documentLink(source.documentId);  
        a.textContent = source.title;
        li.append(a);
        return li;
    }));
    $("ai-answer").hidden = false;
}

function renderHistory() {
    const history = readHistory();
    $("ai-history-box").hidden = history.length === 0;
    $("ai-history").replaceChildren(...history.map((entry, index) => {
        const li = document.createElement("li");
        const button = document.createElement("button");
        button.type = "button";
        button.className = "btn btn-sm";
        button.textContent = "Ver";
        button.addEventListener("click", () => {
            $("ai-question").value = entry.question;
            renderAnswer(entry);
        });
        li.append(`${entry.question} `, button);
        return li;
    }));
}

async function askAi(event) {
    event.preventDefault();
    const question = $("ai-question").value.trim();
    if (!question) return showAiMessage("ai-error", "Escribe una pregunta.");

    showAiMessage("ai-error");
    showAiMessage("ai-limit");
    $("ai-answer").hidden = true;
    $("ai-loading").hidden = false;
    $("ai-submit").disabled = true;

    try {
        const data = await App.api("/api/ai/ask", { method: "POST", body: { question } });
        const entry = { question, answer: data?.answer || "", sources: data?.sources || [] };
        renderAnswer(entry);
        saveHistory([entry, ...readHistory()]);
        renderHistory();
    } catch (error) {
        if (error.status === 429) {
            showAiMessage("ai-limit", error.message ||
                "Alcanzaste el límite de consultas a la IA. Intenta de nuevo más tarde.");
        } else {
            showAiMessage("ai-error", error.message || "No se pudo consultar la IA.");
        }
    } finally {
        $("ai-loading").hidden = true;
        $("ai-submit").disabled = false;
    }
}

function setupAi() {
    $("ai-form").addEventListener("submit", askAi);
    $("ai-question").addEventListener("input", () => {
        $("ai-counter").textContent = `${$("ai-question").value.length} / 1000`;
    });
    renderHistory();
}

if (session) {
    renderCompany(session.user);
    configureQuickActions(session.user.role);
    loadStats();
    loadRecommendations();
    setupAi();
}
