document.addEventListener("DOMContentLoaded", function() {

    browser.storage.local.get("server_url").then(function(config) {
        if (config.hasOwnProperty("server_url")) {
            document.querySelector("#server_url").value = config.server_url;
        }
    });
});

document.querySelector("form").addEventListener("submit", function(event) {
    event.preventDefault();

    // TODO Url validation would be nice.

    browser.storage.local.set({
        server_url: document.querySelector("#server_url").value
    });
});