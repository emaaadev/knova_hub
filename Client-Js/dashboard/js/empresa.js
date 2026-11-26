// Editar los datos de la empresa desde «Tu empresa» (solo AdminEmpresa).
// GET /api/company trae los datos reales de la empresa; PUT /api/company los actualiza.
// Si el GET falla se siguen usando los datos que llegaron con el inicio de sesión.
(function () {
    const $ = (id) => document.getElementById(id);
    const ROL_EDITOR = "AdminEmpresa";
    const CLAVE_USUARIO = "knovaUser";

    // Campo que devuelve la API (en minúsculas) -> input del modal donde se muestra su error.
    const CAMPOS_API = {
        name: "co-name",
        companyname: "co-name",
        rnc: "co-rnc",
        email: "co-email",
        phone: "co-phone",
        address: "co-address",
        sector: "co-sector",
        activity: "co-activity"
    };

    const RNC = /^\d{9}$|^\d{11}$/;
    const TELEFONO = /^[0-9+() -]{7,20}$/;

    let empresa = null;       // datos actuales de la empresa
    let pintar = () => {};    // renderCompany() de dashboard.js

    const regla = (patron, mensaje) => (valor) =>
        patron.test(String(valor ?? "").trim()) ? "" : mensaje;

    function desdeUsuario(user) {
        return {
            name: user.companyName || "",
            rnc: user.rnc || "",
            email: user.companyEmail || user.email || "",
            phone: user.phone || "",
            address: user.address || "",
            sector: user.sector || "",
            activity: user.activity || ""
        };
    }

    function comoUsuario(emp) {
        return {
            ...App.getUser(),
            companyName: emp.name,
            rnc: emp.rnc,
            companyEmail: emp.email,
            phone: emp.phone,
            address: emp.address,
            sector: emp.sector,
            activity: emp.activity
        };
    }

    // Guarda la empresa, actualiza la sesión local y repinta la tarjeta sin recargar la página.
    function aplicar(emp) {
        empresa = emp;
        const usuario = comoUsuario(emp);
        try { localStorage.setItem(CLAVE_USUARIO, JSON.stringify(usuario)); } catch {}
        pintar(usuario);
    }

    async function cargar() {
        try {
            const datos = await App.api("/api/company");
            if (datos) aplicar(datos);
        } catch {
            // Se mantienen los datos del inicio de sesión.
        }
    }

    function esquema() {
        const R = Validar.reglas;
        return [
            { campo: "co-name", reglas: [R.obligatorio("El nombre de la empresa es obligatorio."), R.maximo(150)] },
            { campo: "co-rnc", reglas: [regla(RNC, "El RNC debe tener 9 u 11 dígitos.")] },
            { campo: "co-email", reglas: [R.obligatorio("El correo es obligatorio."), R.correo(), R.maximo(150)] },
            { campo: "co-phone", reglas: [R.obligatorio("El teléfono es obligatorio."), regla(TELEFONO, "Ingresa un teléfono válido (ej.: 809-555-1234).")] },
            { campo: "co-address", reglas: [R.obligatorio("La dirección es obligatoria."), R.maximo(200)] },
            { campo: "co-sector", reglas: [R.obligatorio("Debes seleccionar un sector.")] },
            { campo: "co-activity", reglas: [R.obligatorio("La actividad de la empresa es obligatoria."), R.maximo(250, "La actividad no puede superar los 250 caracteres.")] }
        ];
    }

    function leer() {
        return {
            name: $("co-name").value.trim(),
            rnc: $("co-rnc").value.trim(),
            email: $("co-email").value.trim(),
            phone: $("co-phone").value.trim(),
            address: $("co-address").value.trim(),
            sector: $("co-sector").value,
            activity: $("co-activity").value.trim()
        };
    }

    function mostrarError(mensaje = "") {
        const caja = $("company-error");
        caja.textContent = mensaje;
        caja.hidden = !mensaje;
    }

    function abrir() {
        $("co-name").value = empresa.name ?? "";
        $("co-rnc").value = empresa.rnc ?? "";
        $("co-email").value = empresa.email ?? "";
        $("co-phone").value = empresa.phone ?? "";
        $("co-address").value = empresa.address ?? "";
        $("co-sector").value = empresa.sector ?? "";
        $("co-activity").value = empresa.activity ?? "";

        Validar.limpiar($("company-form"));
        mostrarError();
        $("company-dialog").showModal();
        $("co-name").focus();
    }

    async function guardar(evento) {
        evento.preventDefault();
        mostrarError();
        if (!Validar.revisar(esquema())) return;

        const cuerpo = leer();
        const boton = $("company-save");
        UI.ocupado(boton, true, "Guardando…");

        try {
            const actualizada = await App.api("/api/company", { method: "PUT", body: cuerpo });
            aplicar({ ...empresa, ...cuerpo, ...(actualizada || {}) });
            $("company-dialog").close();
            UI.aviso("Los datos de la empresa se actualizaron correctamente.", "ok", "Empresa actualizada");
        } catch (error) {
            // Si la API indica el campo (por ejemplo, RNC duplicado), el error sale debajo de ese campo.
            if (!Validar.errorDeServidor(error, CAMPOS_API)) {
                mostrarError(error.message || "No se pudieron guardar los cambios.");
            }
        } finally {
            UI.ocupado(boton, false);
        }
    }

    function configurarModal() {
        $("company-edit").hidden = false;
        $("company-edit").addEventListener("click", abrir);
        $("company-cancel").addEventListener("click", () => $("company-dialog").close());
        $("company-form").addEventListener("submit", guardar);
    }

    function iniciar(user, renderizar) {
        pintar = renderizar;
        empresa = desdeUsuario(user);
        // Solo AdminEmpresa ve «Editar»; la API también lo exige (403 para los demás roles).
        if (user.role === ROL_EDITOR) configurarModal();
        cargar();
    }

    window.Empresa = { iniciar };
})();
