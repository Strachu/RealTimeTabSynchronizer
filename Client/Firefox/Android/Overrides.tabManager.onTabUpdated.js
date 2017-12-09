(function() {
    var mTabLastUrl = {};
    var storeTabUrl = function(tab) {
        mTabLastUrl[tab.id] = tab.url;
    }

    browser.tabs.onCreated.addListener(storeTabUrl);

    var originalOnTabUpdated = tabManager.onTabUpdated;
    tabManager.onTabUpdated = function(requestId, tabId, changeInfo, tabInfo) {

        // In some cases, hard to reproduce, after restarting
        // the Firefox for Android 56.0 did not store new url 
        // in changeInfo even if it changed.
        if (mTabLastUrl.hasOwnProperty(tabId) &&
            tabInfo.url != mTabLastUrl[tabId]) {
            changeInfo.url = tabInfo.url;
        }

        var promise = originalOnTabUpdated(requestId, tabId, changeInfo, tabInfo);

        storeTabUrl(tabInfo);

        return promise;
    }
})();