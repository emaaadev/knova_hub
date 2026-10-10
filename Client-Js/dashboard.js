const token = localStorage.getItem("knovaToken");
const rawUser = localStorage.getItem("knovaUser");

if (!token || !rawUser) {
    window.location.href = "login.html";
}

const user = JSON.parse(rawUser);

document.getElementById("user-name").textContent = user.fullName || "";
document.getElementById("user-role").textContent = user.role || "";

document.getElementById("greeting").textContent = `Hola, ${user.fullName || "usuario"}`;
document.getElementById("welcome-company").textContent = user.companyName || "";
document.getElementById("welcome-rnc").textContent = user.rnc || "";

document.getElementById("company-name").textContent = user.companyName || "";
document.getElementById("company-rnc").textContent = user.rnc || "";
document.getElementById("company-email").textContent = user.email || "";
document.getElementById("company-phone").textContent = user.phone || "—";
document.getElementById("company-address").textContent = user.address || "—";

if (user.role === "Admin") {
    document.getElementById("nav-usuarios").hidden = false;
}

document.getElementById("logout").addEventListener("click", () => {
    localStorage.removeItem("knovaToken");
    localStorage.removeItem("knovaUser");
    window.location.href = "login.html";
});
