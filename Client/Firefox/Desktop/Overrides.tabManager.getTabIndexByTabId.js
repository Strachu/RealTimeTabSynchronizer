(function() {
    var originalGetTabIndexByTabId = tabManager.getTabIndexByTabId;

    tabManager.getTabIndexByTabId = function(tabId) {
        return originalGetTabIndexByTabId(tabId)
            .catch(function() {
                // Cannot retrieve tab with browser.tabs.get() after it has been removed
                // but query() still returns results as it was before removing...
                // Where's the logic?!
                return browser.tabs.query({}).then(function(tabs) {
                    var tab = tabs.find(function(x) { return x.id == tabId });
                    return tab.index;
                });
            });
    }
})();