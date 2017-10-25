(function() {

    // On Firefox Desktop 55.0.2 tabs are available only after event window.OnCreated has
    // been created...
    var mTabsAvailablePromise = new Promise(function(resolve) {

        var listener = function() {
            console.log("Got window.onCreated event...");

            browser.windows.onCreated.removeListener(listener);

            // Sometimes they are still not available yet...
            setTimeout(resolve, 100);
        }

        // In case we lost onCreated event:
        setTimeout(function() {
            browser.windows.onCreated.removeListener(listener);
            return resolve();
        }, 2000);
        return browser.windows.onCreated.addListener(listener);
    });

    tabManager.getAllTabsWithUrls = function() {
        return mTabsAvailablePromise.then(function() { return browser.tabs.query({}) });
    };
})();