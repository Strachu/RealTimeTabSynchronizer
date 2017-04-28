var synchronizerServer = $.connection.synchronizerHub;
var tabsCreatedBySynchronizer = {} // TODO Should this be done server side?

synchronizerServer.client.addTab = function(tabIndex, url, createInBackground) {
    console.log("appendEmptyTab");

    var newTabProperties = {
        index: tabIndex,
        url: url,
        active: !createInBackground
    };

    // Seems like there is some race condition - onCreated is working nonetheless
    // Maybe using index in the dictionary will work.
    browser.tabs.create(newTabProperties).then(function(tab) {
        tabsCreatedBySynchronizer[tab.id] = true;
    });
};

synchronizerServer.client.moveTab = function(oldTabIndex, newTabIndex) {
    console.log("moveTab(" + oldTabIndex + ", " + newTabIndex + ")");

    //	browser.tabs.move(oldTabIndex);
};

synchronizerServer.client.closeTab = function(tabIndex) {
    console.log("closeTab(" + tabIndex + ")");

    browser.tabs.query({ index: tabIndex }).then(function(tabs) {
        browser.tabs.remove(tabs[0].id);
    });
};

synchronizerServer.client.changeTabUrl = function(tabIndex, newUrl) {
    console.log("changeTabUrl(" + tabIndex + ", " + newUrl + ")");

    // This seems to fail on android tablet when changing many tabs at once
    browser.tabs.query({ index: tabIndex }).then(function(tabs) {
        browser.tabs.update(tabs[0].id, { url: newUrl });
    });
};

synchronizerServer.client.activateTab = function(tabIndex) {
    console.log("activateTab(" + tabIndex + ")");

    browser.tabs.query({ index: tabIndex }).then(function(tabs) {
        browser.tabs.update(tabs[0].id, { active: true })
    });
};

// It's not possible to get index of deleted tab in onRemoved() on Android.
// On desktop it seems to work just fine.
var tabsStateBeforeRemoval = {};
var saveTabsState = function() {
    browser.tabs.query({}).then(function(tabs) { tabsStateBeforeRemoval = tabs });
}

var connectToServer = function() {
    browser.storage.local.get("server_url").then(function(config) {
        if (config.hasOwnProperty("server_url")) {
            $.connection.hub.url = config.server_url + "/signalR";
            $.connection.hub.logging = true;
            $.connection.hub.start()
                .done(function() {
                    console.log("Connected to server.");

                    syncWithServerIfNotDoneSoYet().then(function() {

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
                    });
                })
                .fail(function() { console.log('Failed to connect to the server.'); });

        } else {
            browser.runtime.openOptionsPage();
        }
    });
}

$.connection.hub.disconnected(function() {
    setTimeout(connectToServer, 10000);

    browser.tabs.onActivated.removeListener(onTabActivated);
    browser.tabs.onCreated.removeListener(onTabCreated);
    browser.tabs.onRemoved.removeListener(onTabRemoved);
    browser.tabs.onUpdated.removeListener(onTabUpdated);
    browser.tabs.onMoved.removeListener(onTabMoved);
});

var onTabActivated = function(activeInfo) {
    console.log("OnActivated:");
    console.log("TabId: " + activeInfo.tabId);

    browser.tabs.get(activeInfo.tabId).then(function(tab) {
        synchronizerServer.server.activateTab(tab.index);
    });
}

var onTabCreated = function(createdTab) {
    console.log("OnCreated:");
    console.log(createdTab);

    if (!tabsCreatedBySynchronizer.hasOwnProperty(createdTab.id)) {
        synchronizerServer.server.addTab(
            createdTab.index, createdTab.url, !createdTab.active);
    }
}

var onTabRemoved = function(tabId) {
    console.log("OnRemoved:");
    console.log("TabId: " + tabId);

    var tab = tabsStateBeforeRemoval.find(function(x) { return x.id == tabId });

    synchronizerServer.server.closeTab(tab.index);
}

var onTabUpdated = function(tabId, changeInfo, tabInfo) {
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
            synchronizerServer.server.changeTabUrl(tab.index, changeInfo.url);
        });
    }
}

var onTabMoved = function(tabId, moveInfo) {
    console.log("onMoved:");
    console.log("{");
    console.log("TabId: " + tabId);
    console.log("Move Info: ");
    console.log(moveInfo);
    console.log("}");
}

var getAllTabsWithUrls = function() {
    return browser.tabs.query({});
}

var syncWithServerIfNotDoneSoYet = function() {

    return new Promise(function(resolve) {

        browser.storage.local.get("syncAlreadyDone").then(function(config) {

            if (config.syncAlreadyDone) {
                console.log("Sync has been already done. Exiting...");
                resolve();
                return;
            }

            console.log("Starting first time synchronization with the server...");

            getAllTabsWithUrls().then(function(tabs) {

                synchronizerServer.server.synchronizeTabs(tabs).then(function() {

                    browser.storage.local.set({ "syncAlreadyDone": true });

                    console.log("Finished first time synchronization with the server.");

                    resolve();
                });
            });
        });
    });
}

browser.storage.onChanged.addListener(function(changes) {

    if (changes.server_url) {
        $.connection.hub.stop();
        connectToServer();
    }
});

connectToServer();