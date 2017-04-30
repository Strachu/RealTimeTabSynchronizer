function TabManager() {

    browser.tabs.onActivated.addListener(onTabActivated);
    browser.tabs.onCreated.addListener(onTabCreated);
    browser.tabs.onRemoved.addListener(onTabRemoved);
    browser.tabs.onUpdated.addListener(onTabUpdated);
    browser.tabs.onMoved.addListener(onTabMoved);

    browser.tabs.onAttached.addListener(saveTabsState);
    browser.tabs.onCreated.addListener(saveTabsState);
    browser.tabs.onDetached.addListener(saveTabsState);
    browser.tabs.onMoved.addListener(saveTabsState);
    browser.tabs.onRemoved.addListener(saveTabsState);
    browser.tabs.onReplaced.addListener(saveTabsState);
    browser.tabs.onUpdated.addListener(saveTabsState);

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

        browser.tabs.create(newTabProperties).then(function(tab) {

            createTabResultPendingCount--;
            tabsCreatedBySynchronizer[tab.id] = true;

            for (var i = 0; i < capturedEventHandlers.length; ++i) {
                capturedEventHandlers[i]();
            }

            capturedEventHandlers = [];
        });
    };

    this.moveTab = function(oldTabIndex, newTabIndex) {
        console.log("moveTab(" + oldTabIndex + ", " + newTabIndex + ")");

        //	browser.tabs.move(oldTabIndex);
    };

    this.changeTabUrl = function(tabIndex, newUrl) {
        console.log("changeTabUrl(" + tabIndex + ", " + newUrl + ")");

        // This seems to fail on android tablet when changing many tabs at once
        browser.tabs.query({ index: tabIndex }).then(function(tabs) {
            browser.tabs.update(tabs[0].id, { url: newUrl });
        });
    };

    this.closeTab = function(tabIndex) {
        console.log("closeTab(" + tabIndex + ")");

        browser.tabs.query({ index: tabIndex }).then(function(tabs) {
            browser.tabs.remove(tabs[0].id);
        });
    };

    this.activateTab = function(tabIndex) {
        console.log("activateTab(" + tabIndex + ")");

        browser.tabs.query({ index: tabIndex }).then(function(tabs) {
            browser.tabs.update(tabs[0].id, { active: true })
        });
    };

    this.getAllTabsWithUrls = function() {
        return browser.tabs.query({});
    }

    // It's not possible to get index of deleted tab in onRemoved() on Android.
    // On desktop it seems to work just fine.
    var tabsStateBeforeRemoval = [];

    function saveTabsState() {
        browser.tabs.query({}).then(function(tabs) { tabsStateBeforeRemoval = tabs });
    }

    function onTabCreated(createdTab) {
        invokeOrInterceptHandler(function() {
            console.log("OnCreated:");
            console.log(createdTab);

            if (!tabsCreatedBySynchronizer.hasOwnProperty(createdTab.id)) {
                synchronizerServer.addTab(createdTab.index, createdTab.url, !createdTab.active);
            }
        });
    }

    function onTabRemoved(tabId) {
        invokeOrInterceptHandler(function() {
            console.log("OnRemoved:");
            console.log("TabId: " + tabId);

            var tab = tabsStateBeforeRemoval.find(function(x) { return x.id == tabId });

            synchronizerServer.closeTab(tab.index);
        });
    }

    function onTabUpdated(tabId, changeInfo, tabInfo) {
        invokeOrInterceptHandler(function() {
            console.log("OnUpdated:");
            console.log("{");
            console.log("TabId: " + tabId);
            console.log("Changed attributes: ");
            console.log(changeInfo);
            console.log("New tab Info: ");
            console.log(tabInfo);
            console.log("}");

            if (changeInfo.url) {
                browser.tabs.get(tabId).then(function(tab) {
                    synchronizerServer.changeTabUrl(tab.index, changeInfo.url);
                });
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

            browser.tabs.get(activeInfo.tabId).then(function(tab) {
                synchronizerServer.activateTab(tab.index);
            })
        });
    }
};