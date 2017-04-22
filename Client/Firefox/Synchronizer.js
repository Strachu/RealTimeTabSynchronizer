var synchronizerServer = $.connection.synchronizerHub;
var tabsCreatedBySynchronizer = {} // TODO Should this be done server side?

synchronizerServer.client.appendEmptyTab = function() {
    console.log("appendEmptyTab");

    browser.tabs.create({}).then(function(tab) {
        tabsCreatedBySynchronizer[tab.id] = true;
    });
};

synchronizerServer.client.moveTab = function(oldTabIndex, newTabIndex) {
    console.log("moveTab(" + oldTabIndex + ", " + newTabIndex + ")");

    //	browser.tabs.move(oldTabIndex);
};

synchronizerServer.client.closeTab = function(tabIndex) {
    console.log("closeTab(" + tabIndex + ")");
};

synchronizerServer.client.changeTabUrl = function(tabIndex, newUrl) {
    console.log("changeTabUrl(" + tabIndex + ", " + newUrl + ")");
};

// TODO Reconnect on connection lost
$.connection.hub.logging = true;
$.connection.hub.url = "http://192.168.0.2:31711/signalr" // TODO - Move to config page
$.connection.hub.start()
    .done(function() {
        console.log("Connected to server.");
        addListeners();
    })
    .fail(function() { console.log('Failed to connect to the server.'); });

var addListeners = function() {

    browser.tabs.onActivated.addListener(function(activeInfo) {
        console.log("OnActivated:");
        console.log("TabId: " + activeInfo.tabId);
    });

    browser.tabs.onCreated.addListener(function(createdTab) {
        console.log("OnCreated:");
        console.log(createdTab);

        if (!tabsCreatedBySynchronizer.hasOwnProperty(createdTab.id)) {
            synchronizerServer.server.addTab();
        }
    });

    browser.tabs.onRemoved.addListener(function(tabId) {
        console.log("OnRemoved:");
        console.log("TabId: " + tabId);
    })

    browser.tabs.onUpdated.addListener(function(tabId, changeInfo, tabInfo) {
        console.log("OnUpdated:");
        console.log("{");
        console.log("TabId: " + tabId);
        console.log("Changed attributes: ");
        console.log(changeInfo);
        console.log("New tab Info: ");
        console.log(tabInfo);
        console.log("}");
    });

    browser.tabs.onMoved.addListener(function(tabId, moveInfo) {
        console.log("onMoved:");
        console.log("{");
        console.log("TabId: " + tabId);
        console.log("Move Info: ");
        console.log(moveInfo);
        console.log("}");
    });
}