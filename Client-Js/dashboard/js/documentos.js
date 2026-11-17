const session = App.setupShell();
const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);
const isWiki = params.get("tipo") === "wiki";
const typeNames = { 1: "Documento", 2: "Wiki", 3: "Minuta" };
const isAdmin = session?.user.role === "AdminEmpresa";
const isSupervisor = session?.user.role === "Supervisor";
const documentsPath = "/api/documents";

let listRequest = null;
let needsFilterOptions = true;
let searchTimer;
let departments = [];
let selected = null;
let editing = null;
let busy = false;
let relatedRequest = null;

function message(id, text = "") {
    $(id).textContent = text;
    $(id).hidden = !text;
}

async function run(errorId, operation) {
    if (busy) return;
    busy = true;
    message(errorId);
    try {
        await operation();
    } catch (error) {
        message(errorId, error.message);
    } finally {
        busy = false;
    }
}

function setOptions(id, options, placeholder) {
    const select = $(id);
    const previous = select.value;
    select.replaceChildren();
    if (placeholder) select.add(new Option(placeholder, ""));
    for (const option of options) {
        const name = option.isActive === false ? `${option.name} (inactivo)` : option.name;
        select.add(new Option(name, option.id));
    }
    if ([...select.options].some(option => option.value === previous)) {
        select.value = previous;
    }
}

function optionsFrom(entries) {
    return [...new Map(entries.filter(entry => entry.id).map(entry => [entry.id, entry])).values()]
        .sort((a, b) => a.name.localeCompare(b.name, "es"));
}

function canModify(item) {
    return isAdmin || (isSupervisor && item.visibility === 2 &&
        departments.some(department => department.id === item.departmentId));
}

function renderList(items) {
    $("list-status").textContent = items.length
        ? `${items.length} resultado${items.length === 1 ? "" : "s"}`
        : "No hay resultados para mostrar.";

    $("items").innerHTML = items.map(item => `
        <tr>
            <td><strong>${App.esc(item.title)}</strong></td>
            <td>${typeNames[item.type]}</td>
            <td>${item.visibility === 1 ? "General" : App.esc(item.departmentName || "Departamento")}</td>
            <td>${App.esc(item.categoryName || "—")}</td>
            <td>${App.esc(item.authorName || "—")}</td>
            <td><button class="btn btn-sm" type="button" data-id="${Number(item.id)}">Ver</button></td>
        </tr>
    `).join("");

    if (!items.length) {
        UI.filaMensaje($("items"), `No hay ${isWiki ? "wikis ni minutas" : "documentos"} para mostrar.`);
    }
}

function getListFilters() {
    const fields = {
        search: $("search").value.trim(),
        departmentId: $("filter-department").value,
        categoryId: $("filter-category").value,
        tagId: $("filter-tag").value
    };
    return new URLSearchParams(Object.entries(fields).filter(([, value]) => value));
}

async function fetchList(filters, type, signal) {
    const types = isWiki && !type ? ["2", "3"] : [isWiki ? type : "1"];
    const results = await Promise.all(types.map(value => {
        const query = new URLSearchParams(filters);
        query.set("type", value);
        return App.api(`${documentsPath}?${query}`, { signal });
    }));
    return results.flat().sort((a, b) => b.createdAt.localeCompare(a.createdAt) || b.id - a.id);
}

function fillListFilters(items) {
    setOptions("filter-department", optionsFrom([
        ...items.map(item => ({ id: item.departmentId, name: item.departmentName })),
        ...departments
    ]), "Todos los visibles");
    setOptions("filter-category", optionsFrom(
        items.map(item => ({ id: item.categoryId, name: item.categoryName }))
    ), "Todas");
    setOptions("filter-tag", optionsFrom(items.flatMap(item => item.tags)), "Todas");
}

async function loadList({ reloadOptions = false } = {}) {
    clearTimeout(searchTimer);
    listRequest?.abort();
    needsFilterOptions ||= reloadOptions;

    const request = new AbortController();
    listRequest = request;
    const { signal } = request;

    $("list-status").textContent = "Cargando…";
    // Con filtros o búsqueda se conservan las filas anteriores para evitar parpadeos.
    if (!$("items").children.length || $("items").querySelector(".estado-fila")) {
        UI.filaMensaje($("items"), "Cargando…", "cargando");
    }
    message("list-error");

    try {
        let allItems;
        if (needsFilterOptions) {
            allItems = await fetchList(new URLSearchParams(), "", signal);
            if (signal.aborted) return;
            fillListFilters(allItems);
            needsFilterOptions = false;
        }

        const filters = getListFilters();
        const type = $("filter-type").value;
        const results = allItems && !filters.size && !type
            ? allItems
            : await fetchList(filters, type, signal);

        if (!signal.aborted) renderList(results);
    } catch (error) {
        if (signal.aborted) return;
        UI.filaMensaje($("items"), "No se pudo cargar el listado.", "error", () => loadList());
        $("list-status").textContent = "No se pudo cargar el listado.";
        message("list-error", error.message);
    }
}

// textStatus: 1 = Pendiente, 2 = Indexado, 3 = NoIndexable (la IA no puede leer el archivo).
function textStatusNote(item) {
    if (item.textStatus === 3) return `. La IA no podrá leer este archivo: ${item.textStatusReason}`;
    if (item.textStatus === 1) return ". Vuelve a subir el archivo para que la IA pueda leerlo.";
    return "";
}

async function loadRelated(id) {
    relatedRequest?.abort();
    const request = new AbortController();
    relatedRequest = request;
    $("related-status").hidden = false;
    $("related-status").textContent = "Cargando relacionados…";
    $("related").replaceChildren();
    try {
        const items = await App.api(`${documentsPath}/${id}/related`, { signal: request.signal });
        if (request.signal.aborted) return;
        $("related-status").hidden = items.length > 0;
        $("related-status").textContent = "No hay documentos relacionados.";
        $("related").replaceChildren(...items.map(item => {
            const li = document.createElement("li");
            const a = document.createElement("a");
            a.href = App.documentLink(item.id, item.type);
            a.textContent = item.title;
            li.append(a, ` · ${typeNames[item.type] || ""}`);
            return li;
        }));
    } catch (error) {
        if (request.signal.aborted) return;
        $("related-status").textContent = `No se pudieron cargar los relacionados. ${error.message}`;
    }
}

function showDetail(item) {
    selected = item;
    $("detail-title").textContent = item.title;
    const visibility = item.visibility === 1 ? "General" : item.departmentName || "Departamento";
    $("detail-meta").textContent = [
        typeNames[item.type], visibility, item.categoryName, item.authorName,
        item.tags.map(tag => tag.name).join(", ")
    ].filter(Boolean).join(" · ");
    $("detail-description").textContent = item.description || "";
    $("detail-content").textContent = item.content || "";
    $("detail-file").textContent = item.hasFile
        ? `Archivo adjunto: ${item.originalFileName}${textStatusNote(item)}`
        : "";
    $("download").hidden = !item.hasFile;
    $("remove-file").hidden = !item.hasFile || !canModify(item);
    $("edit").hidden = !canModify(item);
    $("delete").hidden = !canModify(item);
    $("editor").hidden = true;
    $("detail").hidden = false;
    message("detail-error");
    $("detail-title").focus();
    loadRelated(item.id);
}

async function openDetail(id) {
    const item = await App.api(`${documentsPath}/${id}`);
    const belongsHere = isWiki ? item.type === 2 || item.type === 3 : item.type === 1;
        if (!belongsHere) { location.replace(App.documentLink(item.id, item.type));
        return;
    }
    showDetail(item);
}

function updateVisibility() {
    const department = $("department");
    department.disabled = $("visibility").value === "1";
    department.required = !department.disabled;
}

function activeOptions(catalog, retained) {
    const values = catalog.filter(option => option.isActive || retained.some(item => item.id === option.id));
    for (const option of retained) {
        if (!values.some(value => value.id === option.id)) values.push({ ...option, isActive: false });
    }
    return values;
}

function updateFileNote() {
    $("file-note").textContent = editing?.hasFile
        ? `Archivo actual: ${editing.originalFileName}. Selecciona otro para reemplazarlo.`
        : "Formatos PDF, DOCX, TXT y MD. Máximo 20 MB.";
}

async function openEditor(item = null) {
    const [categories, tags] = await Promise.all([App.api("/api/categories"), App.api("/api/tags")]);
    editing = item;
    $("editor-form").reset();
    Validar.limpiar($("editor"));
    const createTitle = isWiki ? "Crear wiki o minuta" : "Crear documento";
    $("editor-title").textContent = item
        ? `Editar ${typeNames[item.type].toLowerCase()}`
        : createTitle;

    const availableDepartments = departments.filter(department =>
        department.isActive || department.id === item?.departmentId
    );
    const retainedCategory = item?.categoryId
        ? [{ id: item.categoryId, name: item.categoryName }]
        : [];

    setOptions("department", availableDepartments, "Seleccionar departamento");
    setOptions("category", activeOptions(categories, retainedCategory), "Sin categoría");
    setOptions("tags", activeOptions(tags, item?.tags || []));
    for (const option of $("tags").options) {
        option.selected = !!item?.tags.some(tag => tag.id === Number(option.value));
    }

    $("title").value = item?.title || "";
    $("description").value = item?.description || "";
    $("content").value = item?.content || "";
    $("type").value = item?.type || $("filter-type").value || "2";
    $("visibility").value = item?.visibility || (isSupervisor ? "2" : "1");
    $("visibility").disabled = isSupervisor;
    $("department").value = item?.departmentId || "";
    $("category").value = item?.categoryId || "";
    $("file-field").hidden = isWiki && !item?.hasFile;
    updateVisibility();
    updateFileNote();
    message("editor-error");
    $("detail").hidden = true;
    $("editor").hidden = false;
    $("title").focus();
}

async function saveItem() {
    const R = Validar.reglas;
    const valido = Validar.revisar([
        { campo: "title", reglas: [R.obligatorio("El título es obligatorio."), R.maximo(200)] },
        { campo: "department", reglas: [R.obligatorio("Selecciona el departamento al que pertenece.")] },
        { campo: "description", reglas: [R.maximo(1000)] },
        { campo: "file", reglas: [R.archivo()] }
    ]);
    if (!valido) return;

    const file = $("file-field").hidden ? null : $("file").files[0];
    const title = $("title").value.trim();
    const creando = !editing;
    const visibility = Number($("visibility").value);
    const body = {
        title,
        description: $("description").value,
        content: $("content").value,
        type: isWiki ? Number($("type").value) : 1,
        visibility,
        departmentId: visibility === 2 ? Number($("department").value) : null,
        categoryId: Number($("category").value) || null,
        tagIds: [...$("tags").selectedOptions].map(option => Number(option.value))
    };

    $("editor").inert = true;
    UI.ocupado($("save"), true, "Guardando…");
    try {
        const path = editing ? `${documentsPath}/${editing.id}` : documentsPath;
        editing = await App.api(path, { method: editing ? "PUT" : "POST", body });
        if (file) {
            const data = new FormData();
            data.append("file", file);
            try {
                editing = await App.api(`${documentsPath}/${editing.id}/file`, { method: "POST", body: data });
            } catch (error) {
                $("editor-title").textContent = `Editar ${typeNames[editing.type].toLowerCase()}`;
                updateFileNote();
                await loadList({ reloadOptions: true });
                throw new Error(`Se guardó el contenido. Puedes reintentar la subida. ${error.message}`);
            }
        }
        showDetail(editing);
        UI.aviso(creando ? "Creado correctamente." : "Cambios guardados.");
        await loadList({ reloadOptions: true });
    } finally {
        $("editor").inert = false;
        UI.ocupado($("save"), false);
    }
}

async function removeItem() {
    if (!selected || !canModify(selected)) return;
    const confirmado = await UI.confirmar({
        titulo: `Eliminar ${typeNames[selected.type].toLowerCase()}`,
        mensaje: `¿Eliminar «${selected.title}»${selected.hasFile ? " y su archivo adjunto" : ""}? Esta acción no se puede deshacer.`,
        aceptar: "Eliminar",
        peligro: true
    });
    if (!confirmado) return;

    UI.ocupado($("delete"), true, "Eliminando…");
    try {
        await App.api(`${documentsPath}/${selected.id}`, { method: "DELETE" });
        const titulo = selected.title;
        $("detail").hidden = true;
        selected = null;
        UI.aviso(`«${titulo}» eliminado.`);
        await loadList({ reloadOptions: true });
    } finally {
        UI.ocupado($("delete"), false);
    }
}

async function removeFile() {
    if (!selected || !canModify(selected)) return;
    const confirmado = await UI.confirmar({
        titulo: "Quitar archivo adjunto",
        mensaje: `¿Quitar «${selected.originalFileName}» de este recurso?`,
        aceptar: "Quitar archivo",
        peligro: true
    });
    if (!confirmado) return;

    UI.ocupado($("remove-file"), true, "Quitando…");
    try {
        await App.api(`${documentsPath}/${selected.id}/file`, { method: "DELETE" });
        await openDetail(selected.id);
        UI.aviso("Archivo quitado.");
    } finally {
        UI.ocupado($("remove-file"), false);
    }
}

async function downloadFile() {
    const blob = await App.api(`${documentsPath}/${selected.id}/file`, { asBlob: true });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = selected.originalFileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function bindPageActions() {
    $("search").addEventListener("input", () => {
        clearTimeout(searchTimer);
        listRequest?.abort();
        searchTimer = setTimeout(loadList, 250);
    });
    for (const id of ["filter-type", "filter-department", "filter-category", "filter-tag"]) {
        $(id).addEventListener("input", () => loadList());
    }
    $("items").addEventListener("click", event => {
        const button = event.target.closest("[data-id]");
        if (button) run("list-error", () => openDetail(button.dataset.id));
    });
    $("new-item").addEventListener("click", () => run("list-error", () => openEditor()));
    $("edit").addEventListener("click", () => run("detail-error", () => openEditor(selected)));
    $("delete").addEventListener("click", () => run("detail-error", removeItem));
    $("remove-file").addEventListener("click", () => run("detail-error", removeFile));
    $("download").addEventListener("click", () => run("detail-error", downloadFile));
    $("visibility").addEventListener("change", updateVisibility);
    $("cancel").addEventListener("click", () => {
        if (!busy) $("editor").hidden = true;
    });
    $("close-detail").addEventListener("click", () => {
        if (!busy) $("detail").hidden = true;
    });
    $("editor-form").addEventListener("submit", event => {
        event.preventDefault();
        run("editor-error", saveItem);
    });
}

async function start() {
    const moduleTitle = isWiki ? "Wikis y minutas" : "Documentos";
    document.title = `${moduleTitle} — Knova Hub`;
    $("module-title").textContent = moduleTitle;
    $("page-title").textContent = moduleTitle;
    $("type-field").hidden = !isWiki;
    $("type-filter").hidden = !isWiki;
    if (isWiki && params.get("clase") === "2") $("filter-type").value = "2";

    bindPageActions();

    await run("list-error", async () => {
        let departmentError;
        if (isAdmin || isSupervisor) {
            try {
                departments = await App.api("/api/departments");
            } catch (error) {
                departmentError = `No se pudieron cargar los departamentos. ${error.message}`;
            }
        }
        const canCreate = isAdmin || (isSupervisor && departments.some(department => department.isActive));
        $("new-item").hidden = !canCreate;
        $("new-item").textContent = isWiki ? "Crear wiki o minuta" : "Crear documento";
        await loadList({ reloadOptions: true });
        if (departmentError && $("list-error").hidden) message("list-error", departmentError);

        const id = params.get("id");
        if (/^[1-9]\d*$/.test(id || "")) await openDetail(id);
        if (params.get("nuevo") === "1" && $("detail").hidden && canCreate) {
            await openEditor();
        }
    });
}

if (session) start();
