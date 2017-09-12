function TabManager() {

    browser.tabs.onActivated.addListener(onTabActivated);
    browser.tabs.onCreated.addListener(onTabCreated);
    browser.tabs.onRemoved.addListener(onTabRemoved);
    browser.tabs.onUpdated.addListener(onTabUpdated);
    browser.tabs.onMoved.addListener(onTabMoved);

    // Used to queue the event handlers, without queing the event handlers can finish out of order
    // when different type of event handlers uses different amount of async calls.
    var handlerQueuePromise = Promise.resolve();
    var tabsWithOnCreatedCalled = {};

    // Temporary variables to prevent invoking server.addTab() for tabs created by addon.
    // capturedEventHandlers and createTabResultPendingCount are strictly needed only on
    // Android due to onCreated event firing first but is nonetheless left also on desktop
    // in case Mozilla will change the order to be consistent.
    var tabsCreatedBySynchronizer = {}
    var capturedEventHandlers = [];
    var createTabResultPendingCount = 0;
    var invokeOrInterceptHandler = function(handler) {
        if (createTabResultPendingCount == 0) {
            return handlerQueuePromise = handlerQueuePromise.then(handler, handler);
        } else {
            capturedEventHandlers.push(handler);
        }
    }

    this.addTab = function(tabIndex, url, createInBackground) {
        var newTabProperties = {
            index: tabIndex,
            url: url,
            active: !createInBackground
        };

        createTabResultPendingCount++;

        return browser.tabs.create(newTabProperties)
            .then(
                onTabCreated,
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

    this.moveTab = function(tabId, index) {
        // TODO The browser can refuse to move the tab, needs to do something with it?
        return browser.tabs.move(tabId, { index: index });
    };

    this.changeTabUrl = function(tabId, newUrl) {
        return browser.tabs.update(tabId, { url: newUrl });
    };

    this.closeTab = function(tabId) {
        return browser.tabs.remove(tabId);
    };

    this.activateTab = function(tabId) {
        return browser.tabs.update(tabId, { active: true })
    };

    this.getAllTabsWithUrls = function() {
        return browser.tabs.query({});
    }

    this.getTabIndexByTabId = function(tabId) {
        return browser.tabs.get(tabId).then(function(tabInfo) {
            return Promise.resolve(tabInfo.index);
        })
    }

    function onTabCreated(createdTab) {
        return invokeOrInterceptHandler(function() {
            console.log("OnCreated:");
            console.log(createdTab);

            tabsWithOnCreatedCalled[createdTab.id] = true;

            if (!tabsCreatedBySynchronizer.hasOwnProperty(createdTab.id)) {
                return synchronizerServer.addTab(
                    createdTab.id,
                    createdTab.index,
                    createdTab.url, !createdTab.active);
            }
        });
    }

    function onTabRemoved(tabId) {
        return invokeOrInterceptHandler(function() {
            console.log("OnRemoved tabId: " + tabId);

            return synchronizerServer.closeTab(tabId);
        });
    }

    function onTabUpdated(tabId, changeInfo, tabInfo) {
        return invokeOrInterceptHandler(function() {
            console.log("OnUpdated tabId " + tabId);
            console.log("Changed attributes: ");
            console.log(changeInfo);
            console.log(tabInfo);

            // When opening a tab with "Open Link in new tab" a update event with about:blank url
            // is triggered before onCreated - it is useless as update with correct url comes later.
            if (changeInfo.url && tabsWithOnCreatedCalled.hasOwnProperty(tabId)) {
                return synchronizerServer.changeTabUrl(tabId, tabInfo.index, changeInfo.url);
            }
        });
    }

    function onTabMoved(tabId, moveInfo) {
        return invokeOrInterceptHandler(function() {
            console.log("onMoved tabId " + tabId);
            console.log("Move Info: ");
            console.log(moveInfo);

            return synchronizerServer.moveTab(tabId, moveInfo.fromIndex, moveInfo.toIndex);
        });
    }

    function onTabActivated(activeInfo) {
        return invokeOrInterceptHandler(function() {
            console.log("OnActivated tabId: " + activeInfo.tabId);

            return synchronizerServer.activateTab(activeInfo.tabId);
        });
    }
};