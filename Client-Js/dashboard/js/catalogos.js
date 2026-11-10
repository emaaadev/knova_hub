const session = App.setupShell();
const $ = (id) => document.getElementById(id);
const isCategory = document.body.dataset.catalog === "categories";
const canManage = session?.user.role === "AdminEmpresa";
const endpoint = `/api/${document.body.dataset.catalog}`;
const noun = isCategory ? "categoría" : "etiqueta";

let items = [];
let editing = null;
let busy = false;

function showError(id, text = "") {
    $(id).textContent = text;
    $(id).hidden = !text;
}

async function run(errorId, operation) {
    if (busy) return;
    busy = true;
    showError(errorId);
    try {
        await operation();
    } catch (error) {
        showError(errorId, error.message);
    } finally {
        busy = false;
    }
}

function render() {
    const term = $("search").value.trim().toLocaleLowerCase("es");
    const filtered = items.filter(item => [item.name, item.description]
        .some(value => value?.toLocaleLowerCase("es").includes(term)));

    $("catalog-status").textContent = filtered.length
        ? `${filtered.length} resultado${filtered.length === 1 ? "" : "s"}`
        : "No hay resultados para mostrar.";

    $("items").innerHTML = filtered.map(item => `
        <tr>
            <td><strong>${App.esc(item.name)}</strong></td>
            ${isCategory ? `<td>${App.esc(item.description || "—")}</td>` : ""}
            <td><span class="badge ${item.isActive ? "badge-ok" : "badge-muted"}">
                ${item.isActive ? "Activa" : "Inactiva"}
            </span></td>
            ${canManage ? `<td><div class="row-actions">
                <button type="button" class="btn btn-sm" data-action="edit" data-id="${Number(item.id)}">
                    Editar
                </button>
                <button type="button" class="btn btn-sm ${item.isActive ? "btn-danger" : ""}"
                    data-action="toggle" data-id="${Number(item.id)}">
                    ${item.isActive ? "Desactivar" : "Reactivar"}
                </button>
            </div></td>` : ""}
        </tr>
    `).join("");
}

async function load() {
    $("catalog-status").textContent = "Cargando…";
    try {
        items = await App.api(endpoint);
    } catch (error) {
        $("catalog-status").textContent = "No se pudo cargar el listado.";
        showError("catalog-error", error.message);
        return;
    }
    render();
}

function openEditor(item = null) {
    if (busy) return;
    editing = item;
    $("editor-title").textContent = `${item ? "Editar" : "Crear"} ${noun}`;
    $("name").value = item?.name || "";
    if (isCategory) $("description").value = item?.description || "";
    showError("editor-error");
    $("editor").hidden = false;
    $("name").focus();
}

async function save() {
    const name = $("name").value.trim();
    if (!name) throw new Error("El nombre es obligatorio.");

    const body = { name };
    if (isCategory) body.description = $("description").value.trim();
    $("editor").inert = true;
    try {
        await App.api(editing ? `${endpoint}/${editing.id}` : endpoint, {
            method: editing ? "PUT" : "POST", body
        });
        $("editor").hidden = true;
        showError("catalog-error");
        await load();
    } finally {
        $("editor").inert = false;
    }
}

async function toggle(item) {
    if (item.isActive) {
        const confirmed = confirm(`¿Desactivar «${item.name}»? Se conservará en los recursos donde ya se usa.`);
        if (!confirmed) return;
    }

    const path = `${endpoint}/${item.id}`;
    if (isCategory && item.isActive) {
        await App.api(path, { method: "DELETE" });
    } else {
        const body = { name: item.name, isActive: !item.isActive };
        if (isCategory) body.description = item.description;
        await App.api(path, { method: "PUT", body });
    }
    await load();
}

if (session) {
    const title = isCategory ? "Categorías" : "Etiquetas";
    $("module-title").textContent = title;
    $("search").addEventListener("input", render);
    $("new-item").hidden = !canManage;
    $("actions-heading").hidden = !canManage;

    if (canManage) {
        $("new-item").textContent = `Crear ${noun}`;
        $("new-item").addEventListener("click", () => openEditor());
        $("cancel").addEventListener("click", () => {
            if (!busy) $("editor").hidden = true;
        });
        $("editor-form").addEventListener("submit", event => {
            event.preventDefault();
            run("editor-error", save);
        });
        $("items").addEventListener("click", event => {
            const button = event.target.closest("[data-action]");
            if (!button) return;
            const item = items.find(entry => entry.id === Number(button.dataset.id));
            if (!item) return;
            if (button.dataset.action === "edit") openEditor(item);
            else run("catalog-error", () => toggle(item));
        });
    }
    run("catalog-error", load);
}
