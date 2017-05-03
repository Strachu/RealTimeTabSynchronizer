browser.storage.onChanged.addListener(function(changes) {
    if (changes.server_url) {
        synchronizerServer.changeServerUrl(changes.server_url.newValue);
    }
});