(function() {
    tabManager.getAllTabsWithUrls = function() {

        return new Promise(function(resolve) {

            // TODO This is needed on android because tab.url is "about:blank" on all
            // tabs not yet activated. But activating every tab can be bad for performance - needs profiling.
            browser.tabs.query({}).then(function(tabs) {

                var promises = [];
                for (var i = 0; i < tabs.length; i++) {
                    promises.push(ensureTabUrlReady(tabs[i].id));
                }

                Promise.all(promises).then(function() {
                    browser.tabs.query({}).then(resolve);
                });
            });
        });
    };

    function ensureTabUrlReady(tabId) {
        return browser.tabs.update(tabId, { active: true })
            .then(function(tab) {

                if (tab.url && tab.url !== "about:blank")
                    return;

                // The url is not available yet when update returns...
                return new Promise(function(resolve) {
                    var currentTabUrlUpdatedClosure = function(tabId, changeInfo) {

                        if (changeInfo.url) {
                            resolve();
                            browser.tabs.onUpdated.removeListener(currentTabUrlUpdatedClosure);
                        }
                    };

                    browser.tabs.onUpdated.addListener(currentTabUrlUpdatedClosure);
                });
            });
    }
})();