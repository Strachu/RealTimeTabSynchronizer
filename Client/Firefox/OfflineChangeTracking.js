var changeTracker = {
    storageKey: "changeTracker_StorageKey",

    addTab: function(tabId, index, url, createInBackground) {
        changeTracker._pushChange({
            type: "createTab",
            dateTime: new Date(),
            tabId: tabId,
            index: index,
            url: url,
            createInBackground: createInBackground
        });
    },

    moveTab: function(tabId, oldIndex, newIndex) {
        changeTracker._pushChange({
            type: "moveTab",
            dateTime: new Date(),
            tabId: tabId,
            oldIndex: oldIndex,
            newIndex: newIndex
        });
    },

    closeTab: function(tabId) {
        changeTracker._pushChange({
            type: "closeTab",
            dateTime: new Date(),
            tabId: tabId
        });
    },

    changeTabUrl: function(tabId, newUrl) {
        changeTracker._pushChange({
            type: "changeTabUrl",
            dateTime: new Date(),
            tabId: tabId,
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