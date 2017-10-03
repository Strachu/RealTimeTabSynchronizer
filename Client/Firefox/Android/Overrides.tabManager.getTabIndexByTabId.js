(function() {

    // It's not possible to get index of deleted tab in onRemoved() on Android.
    // On desktop it seems to work just fine (on Firefox 52, on 55 mozilla screwed...).
    var tabsStateBeforeRemoval = {};
    var mTabStateUpdatedPromise = Promise.resolve();
    var saveTabsState = function() {
        return mTabStateUpdatedPromise = browser.tabs.query({}).then(function(tabs) {
            for (i = 0; i < tabs.length; ++i) {
                tabsStateBeforeRemoval[tabs[i].id] = tabs[i];
            }
        });
    }

    function saveTabsStateOnListenerAction(listener) {
        if (typeof listener !== 'undefined') {
            listener.addListener(saveTabsState);
        }
    }

    saveTabsStateOnListenerAction(browser.tabs.onAttached);
    saveTabsStateOnListenerAction(browser.tabs.onCreated);
    saveTabsStateOnListenerAction(browser.tabs.onDetached);
    saveTabsStateOnListenerAction(browser.tabs.onHighlightChanged);
    saveTabsStateOnListenerAction(browser.tabs.onHighlighted);
    saveTabsStateOnListenerAction(browser.tabs.onMoved);
    saveTabsStateOnListenerAction(browser.tabs.onRemoved);
    saveTabsStateOnListenerAction(browser.tabs.onReplaced);
    saveTabsStateOnListenerAction(browser.tabs.onSelectionChanged);
    saveTabsStateOnListenerAction(browser.tabs.onUpdated);
    saveTabsState();

    var originalGetTabIndexByTabId = tabManager.getTabIndexByTabId;

    tabManager.getTabIndexByTabId = function(tabId) {
        if (!tabsStateBeforeRemoval.hasOwnProperty(tabId)) {
            return originalGetTabIndexByTabId(tabId);
        }

        return mTabStateUpdatedPromise.then(function() {
            var tabState = tabsStateBeforeRemoval[tabId];
            return Promise.resolve(tabState.index);
        });
    }
})();