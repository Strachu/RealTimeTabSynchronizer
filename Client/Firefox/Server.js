function SynchronizerServer(browserId) {
    var disconnectTimeoutHandle;
    var hub = $.connection.synchronizerHub;
    var currentServerUrl = "http://localhost:31711/";
    var that = this;

    this.connect = function() {
        clearTimeout(disconnectTimeoutHandle);

        $.connection.hub.url = currentServerUrl + "/signalR";
        $.connection.hub.logging = true;

        return $.connection.hub.start()
            .done(function() {
                console.log("Connected to the server at " + currentServerUrl + ".");

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

    this.addTab = function(tabIndex, url, createInBackground) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.addTab(browserId, tabIndex, url, createInBackground);
        } else
            changeTracker.addTab(tabIndex, url, createInBackground);
    }

    this.changeTabUrl = function(tabIndex, url) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.changeTabUrl(browserId, tabIndex, url);
        } else
            changeTracker.changeTabUrl(tabIndex, url);
    }

    this.closeTab = function(tabIndex) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.closeTab(browserId, tabIndex);
        } else
            changeTracker.closeTab(tabIndex);
    }

    this.activateTab = function(tabIndex) {
        if ($.connection.hub.state !== $.signalR.connectionState.disconnected) {
            hub.server.activateTab(browserId, tabIndex);
        } else
            changeTracker.activateTab(tabIndex);
    }

    hub.client.addTab = function(tabIndex, url, createInBackground) {
        console.log("addTab(" + tabIndex + ", " + url + ", " + createInBackground);

        tabManager.addTab(tabIndex, url, createInBackground);

        // TODO Encapsulate ACK here
    };

    hub.client.moveTab = function(oldTabIndex, newTabIndex) {
        console.log("moveTab(" + oldTabIndex + ", " + newTabIndex + ")");

        tabManager.moveTab(oldTabIndex, newTabIndex);
    };

    hub.client.closeTab = function(tabIndex) {
        console.log("closeTab(" + tabIndex + ")");

        tabManager.closeTab(tabIndex);
    };

    hub.client.changeTabUrl = function(tabIndex, newUrl) {
        console.log("changeTabUrl(" + tabIndex + ", " + newUrl + ")");

        tabManager.changeTabUrl(tabIndex, newUrl);
    };

    hub.client.activateTab = function(tabIndex) {
        console.log("activateTab(" + tabIndex + ")");

        tabManager.activateTab(tabIndex, newUrl);
    };
};