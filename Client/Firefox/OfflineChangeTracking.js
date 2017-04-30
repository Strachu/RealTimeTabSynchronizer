var changeTracker = {
    storageKey: "changeTracker_StorageKey",

    addTab: function(tabIndex, url, createInBackground) {
        changeTracker._pushChange({
            type: "createTab",
            dateTime: new Date(),
            tabIndex: tabIndex,
            url: url,
            createInBackground: createInBackground
        });
    },

    moveTab: function(oldTabIndex, newTabIndex) {
        changeTracker._pushChange({
            type: "moveTab",
            dateTime: new Date(),
            oldTabIndex: oldTabIndex,
            newTabIndex: newTabIndex
        });
    },

    closeTab: function(tabIndex) {
        changeTracker._pushChange({
            type: "closeTab",
            dateTime: new Date(),
            tabIndex: tabIndex
        });
    },

    changeTabUrl: function(tabIndex, newUrl) {
        changeTracker._pushChange({
            type: "changeTabUrl",
            dateTime: new Date(),
            tabIndex: tabIndex,
            newUrl: newUrl
        });
    },

    activateTab: function() {},

    getAllChanges: function() {
        return new Promise(function(resolve) {
            browser.storage.local.get(changeTracker.storageKey).then(function(storage) {
                if (storage[changeTracker.storageKey]) {
                    resolve(storage[changeTracker.storageKey]);
                } else {
                    changeTracker._initializeStorageToEmptyArray().then(resolve([]))
                }
            })
        });
    },

    clear: function() { return changeTracker._initializeStorageToEmptyArray() },

    _initializeStorageToEmptyArray: function() {
        return browser.storage.local.set({
            [changeTracker.storageKey]: []
        });
    },

    _pushChange: function(change) {
        return new Promise(function(resolve) {
            browser.storage.local.get(changeTracker.storageKey).then(function(storage) {
                if (!storage[changeTracker.storageKey]) {
                    storage[changeTracker.storageKey] = [];
                }

                storage[changeTracker.storageKey].push(change);

                browser.storage.local.set({
                    [changeTracker.storageKey]: storage[changeTracker.storageKey]
                }).then(resolve());
            })
        });
    }
};