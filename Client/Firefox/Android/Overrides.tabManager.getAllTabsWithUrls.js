(function() {
    var tabUrlsAlreadyReady = [];
    var mMultipleCallsPromise = Promise.resolve();

    tabManager.getAllTabsWithUrls = function() {
        return mMultipleCallsPromise = mMultipleCallsPromise.then(function() {
            return new Promise(function(resolve) {

                // TODO This is needed on android because tab.url is "about:blank" on all
                // tabs not yet activated.
                browser.tabs.query({}).then(function(tabs) {
                    if (tabs.every(function(tab) { return hasUrlReady(tab.id) })) {
                        return resolve(tabs);
                    }

                    var activeTab = tabs.find(function(x) { return x.active === true });
                    if (activeTab === undefined) {
                        activeTab = tabs[0];
                    }

                    var promises = [];
                    for (var i = 0; i < tabs.length; i++) {
                        promises.push(ensureTabUrlReady(tabs[i].id, activeTab.id));
                    }

                    Promise.all(promises).then(function() {
                        browser.tabs.query({}).then(resolve);
                    });
                });
            });
        });
    };

    function hasUrlReady(tabId) {
        return tabUrlsAlreadyReady.hasOwnProperty(tabId);
    }

    function ensureTabUrlReady(tabId, originalActiveTabId) {
        if (hasUrlReady(tabId)) {
            return Promise.resolve();
        }

        return browser.tabs.update(tabId, { active: true })
            .then(function(tab) {
                browser.tabs.update(originalActiveTabId, { active: true })

                if (tab.url && tab.url !== "about:blank") {
                    tabUrlsAlreadyReady[tabId] = true;
                    return;
                }

                // The url is not available yet when update returns...
                return new Promise(function(resolve) {
                    var currentTabUrlUpdatedClosure = function(tabId, changeInfo) {
                        if (tabId != tab.id) {
                            return;
                        }

                        if (changeInfo.url) {
                            resolve();
                            browser.tabs.onUpdated.removeListener(currentTabUrlUpdatedClosure);

                            tabUrlsAlreadyReady[tabId] = true;
                        }
                    };

                    browser.tabs.onUpdated.addListener(currentTabUrlUpdatedClosure);
                });
            });
    }
})();