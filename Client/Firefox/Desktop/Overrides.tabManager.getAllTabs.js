(function() {
    var resolveWhenAllTabsAvailable = function(resolve) {
        browser.tabs.query({}).then(function(tabs) {
            if (tabs.length > 0) {
                resolve();
            } else {
                setTimeout(function() {
                    resolveWhenAllTabsAvailable(resolve);
                }, 50);
            }
        });
    }

    // On Firefox Desktop 55.0.2 tabs are available only after event window.OnCreated has
    // been created and not always, sometimes we need to wait more...
    var mTabsAvailablePromise = new Promise(function(resolve) {
        resolveWhenAllTabsAvailable(resolve);
    });

    tabManager.getAllTabsWithUrls = function() {
        return mTabsAvailablePromise.then(function() { return browser.tabs.query({}) });
    };
})();