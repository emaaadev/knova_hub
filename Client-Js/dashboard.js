const token = localStorage.getItem("knovaToken");
const rawUser = localStorage.getItem("knovaUser");

if (!token || !rawUser) {
    window.location.href = "login.html";
}

const user = JSON.parse(rawUser);

document.getElementById("user-name").textContent = user.fullName || "";
document.getElementById("user-role").textContent = user.role || "";
document.getElementById("greeting").textContent = `Hola, ${user.fullName || "usuario"}`;
document.getElementById("company-name").textContent = user.companyName || "";

document.getElementById("logout").addEventListener("click", () => {
    localStorage.removeItem("knovaToken");
    localStorage.removeItem("knovaUser");
    window.location.href = "login.html";
});
