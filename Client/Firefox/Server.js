function SynchronizerServer(browserId) {
    var disconnectTimeoutHandle;
    var hub = $.connection.synchronizerHub;
    var mHubQueuePromise = Promise.resolve();
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

                return synchronizeWithServer();
            })
            .fail(function() {
                console.error("Failed to connect to the server at " + currentServerUrl + ".");
            })
    }

    var synchronizeWithServer = function() {
        return tabManager.getAllTabsWithUrls().then(function(allTabs) {
            return tabManager.handlerQueuePromise.thenEvenIfError(function() {
                return changeTracker.getAllChanges().then(function(allChanges) {
                    return hub.server.synchronize(browserId, allChanges, allTabs)
                        .done(function() {
                            changeTracker.remove(allChanges)
                                .then(function() {
                                    initialized = true;

                                    replayAllEventsFromOfflineChangeTracker();
                                });
                        })
                        .fail(function(error) {
                            console.error("Failed to synchronize: " + error);
                            return $.connection.hub.stop();
                        })

                });
            });
        });
    }

    var replayAllEventsFromOfflineChangeTracker = function() {
        return changeTracker.getAllChanges()
            .then(function(changesDoneDuringSynchronization) {
                mHubQueuePromise = mHubQueuePromise.catch(function() {});
                for (var i = 0; i < changesDoneDuringSynchronization.length; ++i) {
                    (function(changeToReplay) {
                        switch (changeToReplay.type) {
                            case "createTab":
                                mHubQueuePromise = mHubQueuePromise.then(function() {
                                    return hub.server.addTab(
                                        browserId,
                                        changeToReplay.tabId,
                                        changeToReplay.index,
                                        changeToReplay.url,
                                        changeToReplay.createInBackground)
                                });
                                break;
                            case "moveTab":
                                mHubQueuePromise = mHubQueuePromise.then(function() {
                                    return hub.server.moveTab(
                                        browserId,
                                        changeToReplay.tabId,
                                        changeToReplay.newIndex)
                                });
                                break;
                            case "closeTab":
                                mHubQueuePromise = mHubQueuePromise.then(function() {
                                    return hub.server.closeTab(
                                        browserId,
                                        changeToReplay.tabId)
                                });
                                break;
                            case "changeTabUrl":
                                mHubQueuePromise = mHubQueuePromise.then(function() {
                                    return hub.server.changeTabUrl(
                                        browserId,
                                        changeToReplay.tabId,
                                        changeToReplay.newUrl)
                                });
                                break;
                            default:
                                throw new Error("Invalid change type: " + changeToReplay.type);
                        }

                        mHubQueuePromise.then(function() { changeTracker.remove(changeToReplay) });
                    })(changesDoneDuringSynchronization[i]);
                }
            });
    }

    $.connection.hub.disconnected(function() {
        initialized = false;
        disconnectTimeoutHandle = setTimeout(that.connect, 10000);
    });

    $.connection.hub.reconnected(synchronizeWithServer);

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
        return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
            if (canTalkWithServer()) {
                return hub.server.addTab(browserId, tabId, index, url, createInBackground)
                    .catch(function() {
                        return changeTracker.addTab(tabId, index, url, createInBackground);
                    });
            } else {
                return changeTracker.addTab(tabId, index, url, createInBackground);
            }
        });
    }

    this.changeTabUrl = function(tabId, tabIndex, url) {
        return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
            if (canTalkWithServer()) {
                return hub.server.changeTabUrl(browserId, tabId, url)
                    .catch(function() {
                        return changeTracker.changeTabUrl(tabId, tabIndex, url);
                    });
            } else {
                return changeTracker.changeTabUrl(tabId, tabIndex, url);
            }
        });
    }

    this.moveTab = function(tabId, fromIndex, newIndex) {
        return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
            if (canTalkWithServer()) {
                return hub.server.moveTab(browserId, tabId, newIndex)
                    .catch(function() {
                        return changeTracker.moveTab(tabId, fromIndex, newIndex);
                    });
            } else {
                return changeTracker.moveTab(tabId, fromIndex, newIndex);
            }
        });
    }

    this.closeTab = function(tabId) {
        return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
            if (canTalkWithServer()) {
                return hub.server.closeTab(browserId, tabId)
                    .catch(function() {
                        return tabManager.getTabIndexByTabId(tabId).then(function(index) {
                            changeTracker.closeTab(tabId, index);
                        });
                    });
            } else {
                return tabManager.getTabIndexByTabId(tabId).then(function(index) {
                    changeTracker.closeTab(tabId, index);
                });
            }
        });
    }

    this.activateTab = function(tabId) {
        var body = function() {
            if (canTalkWithServer()) {
                return hub.server.activateTab(browserId, tabId)
                    .catch(function() {
                        return tabManager.getTabIndexByTabId(tabId).then(function(index) {
                            changeTracker.activateTab(tabId, index);
                        });
                    });
            } else {
                return tabManager.getTabIndexByTabId(tabId).then(function(index) {
                    changeTracker.activateTab(tabId, index);
                });
            }
        };

        return mHubQueuePromise = mHubQueuePromise.then(body, body);
    }

    var canTalkWithServer = function() {
        return ($.connection.hub.state !== $.signalR.connectionState.disconnected &&
            $.connection.hub.state !== $.signalR.connectionState.reconnecting &&
            initialized);
    }

    var mTabManagerCallQueue = Promise.resolve();
    hub.client.addTab = function(requestId, tabIndex, url, createInBackground) {
        return mTabManagerCallQueue = mTabManagerCallQueue.thenEvenIfError(function() {
            console.log("addTab(" + tabIndex + ", " + url + ", " + createInBackground);

            return tabManager.addTab(tabIndex, url, createInBackground).then(function(tabInfo) {
                // TODO What if we lose connection here?
                return hub.server.acknowledgeTabAdded(requestId, tabInfo.tabId, tabInfo.index);
            });
        });
    };

    hub.client.moveTab = function(tabId, newIndex) {
        return mTabManagerCallQueue = mTabManagerCallQueue.thenEvenIfError(function() {
            console.log("moveTab(" + tabId + ", " + newIndex + ")");

            return tabManager.moveTab(tabId, newIndex);
        });
    };

    hub.client.closeTab = function(tabId) {
        return mTabManagerCallQueue = mTabManagerCallQueue.thenEvenIfError(function() {
            console.log("closeTab(" + tabId + ")");

            return tabManager.closeTab(tabId);
        });
    };

    hub.client.changeTabUrl = function(tabId, newUrl) {
        return mTabManagerCallQueue = mTabManagerCallQueue.thenEvenIfError(function() {
            console.log("changeTabUrl(" + tabId + ", " + newUrl + ")");

            return tabManager.changeTabUrl(tabId, newUrl);
        });
    };

    hub.client.activateTab = function(tabId) {
        return mTabManagerCallQueue = mTabManagerCallQueue.thenEvenIfError(function() {
            console.log("activateTab(" + tabId + ")");

            return tabManager.activateTab(tabId, newUrl);
        });
    };
};