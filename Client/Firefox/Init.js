var clientIdStorageKey = "clientId";
var synchronizerServer;
var tabManager;

browser.runtime.onInstalled.addListener(function(details) {
    // Initialized here instead during retrieval if not exists because we want to regenerate
    // the UUID after the user uninstalls and reinstalls the addon.
    if (details.reason === "install") {
        clientId = generateUUID();

        browser.storage.local.set({
            [clientIdStorageKey]: clientId
        });

        startAddon();
    }
});

function startAddon() {

    tabManager = new TabManager();

    browser.storage.local.get(clientIdStorageKey)
        .then(function(config) {
            var clientId = config[clientIdStorageKey];
            if (clientId) {
                synchronizerServer = new SynchronizerServer(clientId);

                return browser.storage.local.get("server_url").then(function(config) {
                    if (config.hasOwnProperty("server_url")) {
                        synchronizerServer.changeServerUrl(config.server_url);
                    } else {
                        browser.runtime.openOptionsPage();
                    }
                });
            }
        })
}

startAddon();