function TabManager() {
    var that = this;
    var pendingOnUpdatedForTabId = {};
    var onUpdatedEventsToIgnoreForTabId = [];
    var nextRequestId = 1;

    browser.tabs.onActivated.addListener(function(activeInfo) {
        return invokeOrInterceptHandler(function() { return that.onTabActivated(activeInfo); });
    });
    browser.tabs.onCreated.addListener(function(createdTab) {
        return invokeOrInterceptHandler(function() { return that.onTabCreated(createdTab); });
    });
    browser.tabs.onRemoved.addListener(function(tabId) {
        return invokeOrInterceptHandler(function() { return that.onTabRemoved(tabId); });
    });
    browser.tabs.onUpdated.addListener(function(tabId, changeInfo, tabInfo) {
        var requestId = nextRequestId++;
        if (pendingOnUpdatedForTabId.hasOwnProperty(tabId)) {
            pendingOnUpdatedForTabId[tabId].push(requestId);
        } else {
            pendingOnUpdatedForTabId[tabId] = [requestId];
        }

        return invokeOrInterceptHandler(function() { return that.onTabUpdated(requestId, tabId, changeInfo, tabInfo); });
    });
    browser.tabs.onMoved.addListener(function(tabId, moveInfo) {
        return invokeOrInterceptHandler(function() { return that.onTabMoved(tabId, moveInfo); });
    });

    this.onUrlChangeCommit = function(tabId, requestId) {
        pendingOnUpdatedForTabId[tabId].remove(requestId);

        return !onUpdatedEventsToIgnoreForTabId.remove(requestId);
    }

    // Used to queue the event handlers, without queing the event handlers can finish out of order
    // when different type of event handlers uses different amount of async calls.
    this.handlerQueuePromise = Promise.resolve();
    var tabsWithOnCreatedCalled = {};

    // When opening browser with previous session no on created event is expected
    Promise.resolve()
        .then(function() {
            return that.getAllTabsWithUrls().then(function(tabs) {
                for (var i = 0; i < tabs.length; ++i) {
                    tabsWithOnCreatedCalled[tabs[i].id] = true;
                }
            })
        })

    // Temporary variables to prevent invoking server.addTab() for tabs created by addon.
    // capturedEventHandlers and createTabResultPendingCount are strictly needed only on
    // Android due to onCreated event firing first (not anymore in 56.0) but is nonetheless
    // left also on desktop in case Mozilla will change the order to be consistent.
    var tabsCreatedBySynchronizer = {}
    var capturedEventHandlers = [];
    var createTabResultPendingCount = 0;
    var invokeOrInterceptHandler = function(handler) {
        if (createTabResultPendingCount == 0) {
            return that.handlerQueuePromise = that.handlerQueuePromise.thenEvenIfError(handler);
        } else {
            capturedEventHandlers.push(handler);
        }
    }

    var getTabCount = function() {
        return browser.tabs.query({}).then(function(tabs) {
            return tabs.length;
        });
    }
    this.addTab = function(tabIndex, url, createInBackground) {
        createTabResultPendingCount++;

        return getTabCount()
            .then(function(tabCount) {
                var newTabProperties = {
                    index: Math.min(tabIndex, tabCount),
                    url: url,
                    active: !createInBackground
                };

                return browser.tabs.create(newTabProperties)
                    .then(onTabCreated,
                        function() {
                            // Firefox can refuse to create a tabs with some urls, in this case we try again to 
                            // create (this time empty) tab so that the browser and server is still consistent with
                            // the tabs except its url.
                            delete newTabProperties.url;

                            console.log("Failed to create a tab with url \"" + url + "\". Will try to create an empty one...");

                            return browser.tabs.create(newTabProperties)
                                .then(
                                    onTabCreated,
                                    function(error) {
                                        invokeAllCapturedHandlers();
                                        return Promise.reject(error);
                                    });
                        });

            })
    };

    var onTabCreated = function(tab) {
        tabsCreatedBySynchronizer[tab.id] = true;

        // TODO Should we await promises?
        invokeAllCapturedHandlers();

        return { tabId: tab.id, index: tab.index };
    }

    var invokeAllCapturedHandlers = function() {
        createTabResultPendingCount--;

        for (var i = 0; i < capturedEventHandlers.length; ++i) {
            capturedEventHandlers[i]();
        }

        capturedEventHandlers = [];
    }

    // Needed to notify the server whether the event is the result of a call from server
    // to break cycles in updating by not propagating events created by the server to other
    // browsers.
    var eventsCausedByServerCount = {};
    var getEventsCausedByServerCount = function(tabId) {
        if (!eventsCausedByServerCount.hasOwnProperty(tabId)) {
            eventsCausedByServerCount[tabId] = {
                onUrlChanged: 0,
                onTabMoved: 0,
                onTabActivated: 0
            };
        }

        return eventsCausedByServerCount[tabId];
    }

    this.moveTab = function(tabId, index) {
        // TODO The browser can refuse to move the tab, needs to do something with it?
        return browser.tabs.move(tabId, { index: index }).then(function() {
            getEventsCausedByServerCount(tabId).onTabMoved++;
        });
    };

    this.changeTabUrl = function(tabId, newUrl) {
        // To prevent the situation in which a tab refresh in browser makes a changeTabUrl
        // event while in meantime an initializer tells the browser to change url causing
        // the already started event to overwrite the initializer call on the server.
        onUpdatedEventsToIgnoreForTabId = onUpdatedEventsToIgnoreForTabId.concat(pendingOnUpdatedForTabId[tabId]);

        return browser.tabs.update(tabId, { url: newUrl }).then(function() {
            getEventsCausedByServerCount(tabId).onUrlChanged++;
        });
    };

    this.closeTab = function(tabId) {
        return browser.tabs.remove(tabId);
    };

    this.activateTab = function(tabId) {
        return browser.tabs.update(tabId, { active: true }).then(function() {
            getEventsCausedByServerCount(tabId).onTabActivated++;
        });
    };

    this.getAllTabs = function(urlsRequired) {
        return browser.tabs.query({});
    }

    this.getTabIndexByTabId = function(tabId) {
        return browser.tabs.get(tabId).then(function(tabInfo) {
                return tabInfo.index;
            })
            .catch(function(error) {
                // A hack to allow catching of tab closed event for a ghost tab in firefox 55.0.
                // This tab is always created at then beginning and then immediately removed.
                // No call to browser.tabs.query() can detect it.
                // Caution: this tab id is used when browser is opened by clicking on a link.
                if (tabId == 1) {
                    return 0;
                }

                throw error;
            });
    }

    this.onTabCreated = function(createdTab) {
        console.log("OnCreated:");
        console.log(createdTab);

        tabsWithOnCreatedCalled[createdTab.id] = true;

        if (!tabsCreatedBySynchronizer.hasOwnProperty(createdTab.id)) {
            return synchronizerServer.addTab(
                createdTab.id,
                createdTab.index,
                createdTab.url, !createdTab.active);
        }
    }

    this.onTabRemoved = function(tabId) {
        console.log("OnRemoved tabId: " + tabId);

        return synchronizerServer.closeTab(tabId);
    }

    this.onTabUpdated = function(requestId, tabId, changeInfo, tabInfo) {
        console.log("OnUpdated tabId " + tabId);
        console.log("Changed attributes: ");
        console.log(changeInfo);
        console.log(tabInfo);

        // When opening a tab with "Open Link in new tab" a update event with about:blank url
        // is triggered before onCreated - it is useless as update with correct url comes later.
        // ChangeTabUrl with about:blank url are ignored as they are pointless and are triggered
        // on Firefox Desktop when first time activating a tab and then updating it with original
        // url...
        if (changeInfo.url &&
            changeInfo.url !== "about:blank" &&
            tabsWithOnCreatedCalled.hasOwnProperty(tabId)) {

            var isCausedByServer = false;
            if (getEventsCausedByServerCount(tabId).onUrlChanged > 0) {
                getEventsCausedByServerCount(tabId).onUrlChanged--;
                isCausedByServer = true;
            }

            console.log("OnUpdated for url " + changeInfo.url + " for tab " + tabId);
            return synchronizerServer.changeTabUrl(requestId, tabId, tabInfo.index, changeInfo.url, isCausedByServer);
        } else {
            that.onUrlChangeCommit(tabId, requestId);
        }
    }

    this.onTabMoved = function onTabMoved(tabId, moveInfo) {
        console.log("onMoved tabId " + tabId);
        console.log("Move Info: ");
        console.log(moveInfo);

        var isCausedByServer = false;
        if (getEventsCausedByServerCount(tabId).onTabMoved > 0) {
            getEventsCausedByServerCount(tabId).onTabMoved--;
            isCausedByServer = true;
        }

        return synchronizerServer.moveTab(tabId, moveInfo.fromIndex, moveInfo.toIndex, isCausedByServer);
    }

    this.onTabActivated = function onTabActivated(activeInfo) {
        console.log("OnActivated tabId: " + activeInfo.tabId);

        var isCausedByServer = false;
        if (getEventsCausedByServerCount(activeInfo.tabId).onTabActivated > 0) {
            getEventsCausedByServerCount(activeInfo.tabId).onTabActivated--;
            isCausedByServer = true;
        }

        return synchronizerServer.activateTab(activeInfo.tabId, isCausedByServer);
    }
};