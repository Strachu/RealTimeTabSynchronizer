(function() {
    var mTabsAtBrowserInit = {};

    tabManager.handlerQueuePromise = new Promise(function(resolve) {
        return tabManager.getAllTabsWithUrls().then(function(tabs) {
            console.log("Initializing tabs at browser init list...");
            console.log("Found " + tabs.length + " tabs.");

            for (var i = 0; i < tabs.length; ++i) {
                mTabsAtBrowserInit[tabs[i].id] = tabs[i];
            }

            resolve();
        });
    });

    // Firefox event API is a useless shit...
    // it's behaviour is just random... changing every version, has illogical event order...
    // It this case, in 55.0.2 firefox fires onTabCreated event for tabs from last sessions
    // but only for those that are after the active tab...
    // We stop tab event consuming until a tab list is available and then filter out events
    // for tabs restored from previous session.
    var originalOnTabCreated = tabManager.onTabCreated;
    tabManager.onTabCreated = function(createdTab) {

        if (isTabFromPreviousSession(createdTab)) {
            console.log(
                "OnTabCreated event for " + createdTab.id + " ignored " +
                "because it was already available at browser launch.")
            return;
        }

        return originalOnTabCreated(createdTab);
    }

    // The state of browser tabs created in many situations:
    // 1. Tab from previous session at browser startup:
    // Title: "New Tab" (52) | final title (55),
    // Url: "about:blank" (52) | final url (55),
    // 2. "Restore tab"
    // Title: "New Tab" (52) | final title (55),
    // Url: "about:blank"
    // 3. Open link in new tab:
    // Title: "Connecting..." (52) | final title (55),
    // Url: "about:blank",
    // 4. New empty tab
    // Title: "New Tab",
    // Url: "about:newtab",
    // 5. A browser opened by clicking on tab in external application
    // Always tab id = 1,
    // Always moved to the end after creation,
    // Title: "New Tab",
    // Url: "about:blank"
    var isTabFromPreviousSession = function(tab) {
        return mTabsAtBrowserInit.hasOwnProperty(tab.id) &&
            tab.status == "complete" &&
            tab.url !== "about:blank" && // Will not work for Firefox 52...
            tab.url !== "about:newtab";
    }
})();