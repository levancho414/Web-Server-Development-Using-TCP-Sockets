
document.addEventListener("DOMContentLoaded", function () {
    const btn = document.getElementById("greetBtn");
    if (btn) {
        btn.addEventListener("click", function () {
            alert("Hello from your JavaScript file!");
        });
    }

    console.log("script.js has been loaded successfully.");
});
