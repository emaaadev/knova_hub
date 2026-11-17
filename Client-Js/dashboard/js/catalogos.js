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
                    ${item.isActive ? "Desactivar" : "Activar"}
                </button>
            </div></td>` : ""}
        </tr>
    `).join("");
    
    if (!filtered.length) {
        UI.filaMensaje($("items"), term
            ? "No hay resultados para esa búsqueda."
            : `No hay ${isCategory ? "categorías" : "etiquetas"} para mostrar.`);
    }
}

async function load() {
    $("catalog-status").textContent = "Cargando…";
    if (!$("items").children.length) UI.filaMensaje($("items"), "Cargando…", "cargando");
    try {
        items = await App.api(endpoint);
    } catch (error) {
        $("catalog-status").textContent = "No se pudo cargar el listado.";
        UI.filaMensaje($("items"), "No se pudo cargar el listado.", "error", () => run("catalog-error", load));
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
    Validar.limpiar($("editor"));
    $("editor").hidden = false;
    $("name").focus();
}

async function save() {
    const R = Validar.reglas;
    const campos = [{
        campo: "name",
        reglas: [R.obligatorio("El nombre es obligatorio."), R.maximo(isCategory ? 100 : 50)]
    }];
    if (isCategory) campos.push({ campo: "description", reglas: [R.maximo(500)] });
    if (!Validar.revisar(campos)) return;

    const name = $("name").value.trim();
    const body = { name };
    if (isCategory) body.description = $("description").value.trim();
    $("editor").inert = true;
    UI.ocupado($("save"), true, "Guardando…");
    try {
        await App.api(editing ? `${endpoint}/${editing.id}` : endpoint, {
            method: editing ? "PUT" : "POST", body
        });
        $("editor").hidden = true;
        showError("catalog-error");
        UI.aviso(`${isCategory ? "Categoría" : "Etiqueta"} ${editing ? "actualizada" : "creada"}.`);
        await load();
    } catch (error) {
        // 409: ya existe un elemento con ese nombre en la empresa.
        if (error.status !== 409) throw error;
        $("editor").inert = false;
        Validar.mostrar($("name"), error.message);
        $("name").focus();
    } finally {
        $("editor").inert = false;
        UI.ocupado($("save"), false);
    }
}

async function toggle(item) {
    if (item.isActive) {
        const confirmado = await UI.confirmar({
            titulo: `Desactivar ${noun}`,
            mensaje: `¿Desactivar «${item.name}»? Se conservará en los recursos donde ya se usa.`,
            aceptar: "Desactivar",
            peligro: true
        });
        if (!confirmado) return;
    }

    const path = `${endpoint}/${item.id}`;
    if (isCategory && item.isActive) {
        await App.api(path, { method: "DELETE" });
    } else {
        const body = { name: item.name, isActive: !item.isActive };
        if (isCategory) body.description = item.description;
        await App.api(path, { method: "PUT", body });
    }
    UI.aviso(`${isCategory ? "Categoría" : "Etiqueta"} ${item.isActive ? "desactivada" : "activada"}.`);
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
