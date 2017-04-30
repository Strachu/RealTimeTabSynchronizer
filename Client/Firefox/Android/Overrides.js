tabManager.getAllTabsWithUrls = function() {

    return new Promise(function(resolve) {

        // TODO This is needed on android because tab.url is "about:blank" on all
        // tabs not yet activated. But activating every tab can be bad for performance - needs profiling.
        // But still some tabs are not included. Need to wait some time??
        browser.tabs.query({}).then(function(tabs) {

            var promises = [];
            for (var i = 0; i < tabs.length; i++) {
                promises.push(browser.tabs.update(tabs[i].id, { active: true }));
            }

            Promise.all(promises).then(function() {

                browser.tabs.query({}).then(resolve);

            });
        });

    });

};