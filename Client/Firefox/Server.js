function SynchronizerServer(browserId) {
    var disconnectTimeoutHandle;
    var hub = $.connection.synchronizerHub;
    var currentServerUrl = "http://localhost:31711/";
    var initialized = false;
    var that = this;

    this.connect = function() {
        clearTimeout(disconnectTimeoutHandle);

        $.connection.hub.qs = { "browserId": browserId };
        $.connection.hub.url = currentServerUrl + "/signalR";
        $.connection.hub.logging = true;

        return $.connection.hub.start()
            .done(function() {
                console.log("Connected to the server at " + currentServerUrl + ".");

                $.connection.hub.qs = {};

                // TODO It's needed only for first time, optimize it for android?
                var allTabsPromise = tabManager.getAllTabsWithUrls();
                var allChangesPromise = changeTracker.getAllChanges();

                // TODO There is a small probability of some events being generated between getAllChanges
                // and setting initialized to true. These events will be lost, to prevent it they should
                // be saved to a queue and replayed after successful synchronization.

                return Promise.all([allTabsPromise, allChangesPromise])
                    .then(function(results) {
                        var allTabs = results[0];
                        var allChanges = results[1];

                        return hub.server.synchronize(browserId, allChanges, allTabs)
                            .done(function() {
                                initialized = true;

                                return changeTracker.clear();
                            })
                            .fail(function(error) {
                                console.error("Failed to synchronize: " + error);
                                return $.connection.hub.stop();
                            })

                    });
            })
            .fail(function() {
                console.error("Failed to connect to the server at " + currentServerUrl + ".");
            })
    }

    $.connection.hub.disconnected(function() {
        initialized = false;
        disconnectTimeoutHandle = setTimeout(that.connect, 10000);
    });

    this.changeServerUrl = function(newServerUrl) {
        if (newServerUrl != currentServerUrl) {
            currentServerUrl = trimTrailingSlashes(newServerUrl);

            $.connection.hub.stop();
            initialized = false;
            return this.connect();
        }
    };

    var trimTrailingSlashes = function(url) {
        return url.trim().replace(/[\\/]+$/, "");
    }

    this.addTab = function(tabId, index, url, createInBackground) {
        if (canTalkWithServer()) {
            hub.server.addTab(browserId, tabId, index, url, createInBackground);
        } else {
            changeTracker.addTab(index, url, createInBackground);
        }
    }

    this.changeTabUrl = function(tabId, url) {
        if (canTalkWithServer()) {
            hub.server.changeTabUrl(browserId, tabId, url);
        } else {
            tabManager.getTabIndexByTabId(tabId).then(function(tabIndex) {
                changeTracker.changeTabUrl(tabIndex, url);
            });
        }
    }

    this.moveTab = function(tabId, fromIndex, newIndex) {
        if (canTalkWithServer()) {
            return hub.server.moveTab(browserId, tabId, newIndex);
        } else {
            return changeTracker.moveTab(fromIndex, newIndex);
        }
    }

    this.closeTab = function(tabId) {
        if (canTalkWithServer()) {
            hub.server.closeTab(browserId, tabId);
        } else {
            tabManager.getTabIndexByTabId(tabId).then(function(tabIndex) {
                changeTracker.closeTab(tabIndex);
            });
        }
    }

    this.activateTab = function(tabId) {
        if (canTalkWithServer()) {
            hub.server.activateTab(browserId, tabId);
        } else {
            tabManager.getTabIndexByTabId(tabId).then(function(tabIndex) {
                changeTracker.activateTab(tabIndex);
            });
        }
    }

    var canTalkWithServer = function() {
        return ($.connection.hub.state !== $.signalR.connectionState.disconnected && initialized);
    }

    hub.client.addTab = function(requestId, tabIndex, url, createInBackground) {
        console.log("addTab(" + tabIndex + ", " + url + ", " + createInBackground);

        tabManager.addTab(tabIndex, url, createInBackground).then(function(tabInfo) {
            // TODO What if we lose connection here?
            hub.server.acknowledgeTabAdded(requestId, tabInfo.tabId, tabInfo.index);
        });
    };

    hub.client.moveTab = function(tabId, oldIndex, newIndex) {
        console.log("moveTab(" + tabId + ", " + oldIndex + ", " + newIndex + ")");

        tabManager.moveTab(tabId, oldIndex, newIndex);
    };

    hub.client.closeTab = function(tabId) {
        console.log("closeTab(" + tabId + ")");

        tabManager.closeTab(tabId);
    };

    hub.client.changeTabUrl = function(tabId, newUrl) {
        console.log("changeTabUrl(" + tabId + ", " + newUrl + ")");

        tabManager.changeTabUrl(tabId, newUrl);
    };

    hub.client.activateTab = function(tabId) {
        console.log("activateTab(" + tabId + ")");

        tabManager.activateTab(tabId, newUrl);
    };
};