var changeTracker = {
    storageKey: "changeTracker_StorageKey",

    // Used to queue the calls to storage.get(), without queing it was happening that 2 events
    // invoked storage.get() before first finished thus operating on the same array resulting in
    // loss of first event.
    _pushChangeQueuePromise: Promise.resolve(),

    addTab: function(tabIndex, url, createInBackground) {
        return changeTracker._pushChange({
            type: "createTab",
            dateTime: new Date(),
            index: tabIndex,
            url: url,
            createInBackground: createInBackground
        });
    },

    moveTab: function(tabIndex, newIndex) {
        return changeTracker._pushChange({
            type: "moveTab",
            dateTime: new Date(),
            index: tabIndex,
            newIndex: newIndex
        });
    },

    closeTab: function(tabIndex) {
        return changeTracker._pushChange({
            type: "closeTab",
            dateTime: new Date(),
            index: tabIndex
        });
    },

    changeTabUrl: function(tabIndex, newUrl) {
        return changeTracker._pushChange({
            type: "changeTabUrl",
            dateTime: new Date(),
            index: tabIndex,
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
        changeTracker._pushChangeQueuePromise = changeTracker._pushChangeQueuePromise.then(function() {
            return new Promise(function(resolve) {
                browser.storage.local.get(changeTracker.storageKey).then(function(storage) {
                    if (!storage[changeTracker.storageKey]) {
                        storage[changeTracker.storageKey] = [];
                    }

                    storage[changeTracker.storageKey].push(change);

                    browser.storage.local.set({
                        [changeTracker.storageKey]: storage[changeTracker.storageKey]
                    }).then(resolve);
                })
            });
        })

        return changeTracker._pushChangeQueuePromise;
    }
};