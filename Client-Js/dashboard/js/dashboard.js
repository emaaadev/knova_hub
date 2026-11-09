// Dashboard (Inicio): saludo, datos de la empresa y accesos rápidos.
const session = App.setupShell();

if (session) {
    const user = session.user;

    document.getElementById("greeting").textContent = `Hola, ${user.fullName || "usuario"}`;
    document.getElementById("welcome-company").textContent = user.companyName || "";
    document.getElementById("welcome-rnc").textContent = user.rnc || "";

    document.getElementById("company-name").textContent = user.companyName || "";
    document.getElementById("company-rnc").textContent = user.rnc || "";
    document.getElementById("company-email").textContent = user.email || "";
    document.getElementById("company-phone").textContent = user.phone || "—";
    document.getElementById("company-address").textContent = user.address || "—";

    // "Invitar usuario": el AdminEmpresa crea directo; el Supervisor solicita el alta.
    const invitar = document.getElementById("quick-invitar");
    if (invitar) {
        invitar.href = user.role === "AdminEmpresa" ? "usuarios.html" : "solicitudes.html";
    }
}
