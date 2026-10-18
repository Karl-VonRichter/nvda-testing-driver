"use strict";
document.getElementById("example-form").addEventListener("submit", function (event) {
    event.preventDefault();
    const email = document.getElementById("example-email");
    const invalid = !email.validity.valid;
    email.setAttribute("aria-invalid", String(invalid));
    document.getElementById("form-result").textContent = invalid
        ? "Enter a valid email address."
        : "Example form complete. No data was sent.";
});
document.getElementById("open-dialog").addEventListener("click", function () {
    document.getElementById("example-dialog").showModal();
});
