function SynchronizerServer(browserId) {
    var disconnectTimeoutHandle;
    var hub = $.connection.synchronizerHub;
    var currentServerUrl = "http://localhost:31711/";
    var that = this;

    this.connect = function() {
        clearTimeout(disconnectTimeoutHandle);

        $.connection.hub.qs = { "browserId": browserId };
        $.connection.hub.url = currentServerUrl + "/signalR";
        $.connection.hub.logging = true;

        return $.connection.hub.start()
            .done(function() {
                console.log("Connected to the server at " + currentServerUrl + ".");

                // TODO It' passed on every request, so maybe we do not need to pass it manually for every method?
                $.connection.hub.qs = {};

                // TODO It's needed only for first time, optimize it for android?
                var allTabsPromise = tabManager.getAllTabsWithUrls();
                var allChangesPromise = changeTracker.getAllChanges();

                return Promise.all([allTabsPromise, allChangesPromise])
                    .then(function(results) {
                        var allTabs = results[0];
                        var allChanges = results[1];

                        return hub.server.synchronize(browserId, allChanges, allTabs)
                            .done(function() {
                                // TODO Is there possibility of something lost if something was added between connect and done?
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
        disconnectTimeoutHandle = setTimeout(that.connect, 10000);
    });

    this.changeServerUrl = function(newServerUrl) {
        if (newServerUrl != currentServerUrl) {
            currentServerUrl = newServerUrl;

            $.connection.hub.stop();
            return this.connect();
        }
    };

    this.addTab = function(tabId, index, url, createInBackground) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.addTab(browserId, tabId, index, url, createInBackground);
        } else
            changeTracker.addTab(tabId, index, url, createInBackground);
    }

    this.changeTabUrl = function(tabId, url) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.changeTabUrl(browserId, tabId, url);
        } else
            changeTracker.changeTabUrl(tabId, url);
    }

    this.closeTab = function(tabId) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.closeTab(browserId, tabId);
        } else
            changeTracker.closeTab(tabId);
    }

    this.activateTab = function(tabId) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.activateTab(browserId, tabId);
        } else
            changeTracker.activateTab(tabId);
    }

    hub.client.addTab = function(requestId, tabIndex, url, createInBackground) {
        console.log("addTab(" + tabIndex + ", " + url + ", " + createInBackground);

        // tabManager.addTab(tabIndex, url, createInBackground).then(function(tabId, index) {
        //     // TODO What if we lose connection here?
        //     hub.server.acknowledgeTabAdded(requestId, tabId);
        // });
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