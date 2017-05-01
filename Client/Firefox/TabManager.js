function TabManager() {

    browser.tabs.onActivated.addListener(onTabActivated);
    browser.tabs.onCreated.addListener(onTabCreated);
    browser.tabs.onRemoved.addListener(onTabRemoved);
    browser.tabs.onUpdated.addListener(onTabUpdated);
    browser.tabs.onMoved.addListener(onTabMoved);

    // Temporary variables to prevent invoking server.addTab() for tabs created by addon.
    // capturedEventHandlers and createTabResultPendingCount are strictly needed only on
    // Android due to onCreated event firing first but is nonetheless left also on desktop
    // in case Mozilla will change the order to be consistent.
    var tabsCreatedBySynchronizer = {}
    var capturedEventHandlers = [];
    var createTabResultPendingCount = 0;
    var invokeOrInterceptHandler = function(handler) {
        if (createTabResultPendingCount == 0) {
            handler();
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

        return browser.tabs.create(newTabProperties).then(function(tab) {

            createTabResultPendingCount--;
            tabsCreatedBySynchronizer[tab.id] = true;

            for (var i = 0; i < capturedEventHandlers.length; ++i) {
                capturedEventHandlers[i]();
            }

            capturedEventHandlers = [];

            return { tabId: tab.id, index: tab.index };
        });
    };

    this.moveTab = function(tabId, index) {
        // TODO The browser can refuse to move the tab, needs to do something with it?
        browser.tabs.move(tabId, { index: tabId });
    };

    this.changeTabUrl = function(tabId, newUrl) {
        browser.tabs.update(tabId, { url: newUrl });
    };

    this.closeTab = function(tabId) {
        browser.tabs.remove(tabId);
    };

    this.activateTab = function(tabId) {
        browser.tabs.update(tabId, { active: true })
    };

    this.getAllTabsWithUrls = function() {
        return browser.tabs.query({});
    }

    function onTabCreated(createdTab) {
        invokeOrInterceptHandler(function() {
            console.log("OnCreated:");
            console.log(createdTab);

            if (!tabsCreatedBySynchronizer.hasOwnProperty(createdTab.id)) {
                synchronizerServer.addTab(
                    createdTab.id,
                    createdTab.index,
                    createdTab.url, !createdTab.active);
            }
        });
    }

    function onTabRemoved(tabId) {
        invokeOrInterceptHandler(function() {
            console.log("OnRemoved:");
            console.log("TabId: " + tabId);

            synchronizerServer.closeTab(tabId);
        });
    }

    function onTabUpdated(tabId, changeInfo, tabInfo) {
        invokeOrInterceptHandler(function() {
            console.log("OnUpdated:");
            console.log("TabId: " + tabId);
            console.log("Changed attributes: ");
            console.log(changeInfo);

            if (changeInfo.url) {
                synchronizerServer.changeTabUrl(tabId, changeInfo.url);
            }
        });
    }

    function onTabMoved(tabId, moveInfo) {
        invokeOrInterceptHandler(function() {
            console.log("onMoved:");
            console.log("{");
            console.log("TabId: " + tabId);
            console.log("Move Info: ");
            console.log(moveInfo);
            console.log("}")
        });
    }

    function onTabActivated(activeInfo) {
        invokeOrInterceptHandler(function() {
            console.log("OnActivated:");
            console.log("TabId: " + activeInfo.tabId);

            synchronizerServer.activateTab(activeInfo.tabId);
        });
    }
};