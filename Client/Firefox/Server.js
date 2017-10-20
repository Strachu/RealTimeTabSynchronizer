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
                                        changeToReplay.createInBackground,
                                        false);
                                });
                                break;
                            case "moveTab":
                                mHubQueuePromise = mHubQueuePromise.then(function() {
                                    return hub.server.moveTab(
                                        browserId,
                                        changeToReplay.tabId,
                                        changeToReplay.newIndex,
                                        false);
                                });
                                break;
                            case "closeTab":
                                mHubQueuePromise = mHubQueuePromise.then(function() {
                                    return hub.server.closeTab(
                                        browserId,
                                        changeToReplay.tabId,
                                        false);
                                });
                                break;
                            case "changeTabUrl":
                                mHubQueuePromise = mHubQueuePromise.then(function() {
                                    var shouldCommit = tabManager.onUrlChangeCommit(changeToReplay.tabId, changeToReplay.browserRequestId);
                                    if (!shouldCommit) {
                                        console.log("Changing url tab of " + changeToReplay.tabId + " cancelled");
                                        return;
                                    }

                                    return hub.server.changeTabUrl(
                                        browserId,
                                        changeToReplay.tabId,
                                        changeToReplay.newUrl,
                                        false);
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

    $.connection.hub.reconnected(function() {
        initialized = false;
        synchronizeWithServer();
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
        return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
            if (canTalkWithServer()) {
                console.log("hub.server.addTab" + tabId);
                return hub.server.addTab(browserId, tabId, index, url, createInBackground)
                    .catch(function() {
                        return changeTracker.addTab(tabId, index, url, createInBackground);
                    });
            } else {
                return changeTracker.addTab(tabId, index, url, createInBackground);
            }
        });
    }

    this.changeTabUrl = function(requestId, tabId, tabIndex, url, isCausedByServer) {
        return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
            if (canTalkWithServer()) {
                console.log("hub.server.changeTabUrl" + tabId);

                var shouldCommit = tabManager.onUrlChangeCommit(tabId, requestId);
                if (!shouldCommit) {
                    console.log("Changing url tab of " + tabId + " cancelled");
                    return;
                }

                return hub.server.changeTabUrl(browserId, tabId, url, isCausedByServer)
                    .catch(function() {
                        return changeTracker.changeTabUrl(tabId, tabIndex, url);
                    });
            } else {
                return changeTracker.changeTabUrl(requestId, tabId, tabIndex, url);
            }
        });
    }

    this.moveTab = function(tabId, fromIndex, newIndex, isCausedByServer) {
        return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
            if (canTalkWithServer()) {
                console.log("hub.server.moveTab" + tabId);
                return hub.server.moveTab(browserId, tabId, newIndex, isCausedByServer)
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

    this.activateTab = function(tabId, isCausedByServer) {
        var body = function() {
            if (canTalkWithServer()) {
                console.log("hub.server.activateTab" + tabId);
                return hub.server.activateTab(browserId, tabId, isCausedByServer)
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
    var handleCallFromServer = function(isFromInitializer, action) {
        // Handle the action only if client has been already synchronized otherwise
        // all actions done by other browsers will be duplicated if it's synchronization
        // happen between connection and finishing our synchronization.
        if (initialized || isFromInitializer) {
            return mTabManagerCallQueue = mTabManagerCallQueue.thenEvenIfError(action);
        }
    }

    hub.client.addTab = function(requestId, tabIndex, url, createInBackground, isFromInitializer) {
        return handleCallFromServer(isFromInitializer, function() {
            console.log("addTab(" + tabIndex + ", " + url + ", " + createInBackground);

            return tabManager.addTab(tabIndex, url, createInBackground).then(function(tabInfo) {
                return tabManager.handlerQueuePromise.thenEvenIfError(function() {
                    // TODO What if we lose connection here?
                    return mHubQueuePromise = mHubQueuePromise.thenEvenIfError(function() {
                        return hub.server.acknowledgeTabAdded(requestId, tabInfo.tabId, tabInfo.index)
                    });
                });
            });
        });
    };

    hub.client.moveTab = function(tabId, newIndex, isFromInitializer) {
        return handleCallFromServer(isFromInitializer, function() {
            console.log("moveTab(" + tabId + ", " + newIndex + ")");

            return tabManager.moveTab(tabId, newIndex);
        });
    };

    hub.client.closeTab = function(tabId, isFromInitializer) {
        return handleCallFromServer(isFromInitializer, function() {
            console.log("closeTab(" + tabId + ")");

            return tabManager.closeTab(tabId);
        });
    };

    hub.client.changeTabUrl = function(tabId, newUrl, isFromInitializer) {
        return handleCallFromServer(isFromInitializer, function() {
            console.log("changeTabUrl(" + tabId + ", " + newUrl + ")");

            return tabManager.changeTabUrl(tabId, newUrl);
        });
    };

    hub.client.activateTab = function(tabId, isFromInitializer) {
        return handleCallFromServer(isFromInitializer, function() {
            console.log("activateTab(" + tabId + ")");

            return tabManager.activateTab(tabId);
        });
    };
};