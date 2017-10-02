var changeTracker = {
    storageKey: "changeTracker_StorageKey",

    // Used to queue the calls to storage.get(), without queing it was happening that 2 events
    // invoked storage.get() before first finished thus operating on the same array resulting in
    // loss of first event.
    // It's also used to wait for completion of all pending tasks when retrieving changes - important
    // for reliable replaying of events after synchronization.
    _lastActionPromise: Promise.resolve(),

    addTab: function(tabId, tabIndex, url, createInBackground) {
        return changeTracker._pushChange({
            type: "createTab",
            dateTime: new Date(),
            tabId: tabId,
            index: tabIndex,
            url: url,
            createInBackground: createInBackground
        });
    },

    moveTab: function(tabId, tabIndex, newIndex) {
        return changeTracker._pushChange({
            type: "moveTab",
            dateTime: new Date(),
            tabId: tabId,
            index: tabIndex,
            newIndex: newIndex
        });
    },

    closeTab: function(tabId, tabIndex) {
        return changeTracker._pushChange({
            type: "closeTab",
            dateTime: new Date(),
            tabId: tabId,
            index: tabIndex
        });
    },

    changeTabUrl: function(tabId, tabIndex, newUrl) {
        return changeTracker._pushChange({
            type: "changeTabUrl",
            dateTime: new Date(),
            tabId: tabId,
            index: tabIndex,
            newUrl: newUrl
        });
    },

    activateTab: function() {},

    getAllChanges: function() {
        return changeTracker._lastActionPromise = changeTracker._lastActionPromise.thenEvenIfError(function() {
            return new Promise(function(resolve) {
                browser.storage.local.get(changeTracker.storageKey).then(function(storage) {
                    if (storage[changeTracker.storageKey]) {
                        resolve(storage[changeTracker.storageKey]);
                    } else {
                        changeTracker._initializeStorageToEmptyArray().then(resolve([]))
                    }
                })
            });
        });
    },

    remove: function(changesToRemove) {
        if (!(changesToRemove instanceof Array)) {
            changesToRemove = [changesToRemove];
        }

        return changeTracker._lastActionPromise = changeTracker._lastActionPromise.thenEvenIfError(function() {
            return changeTracker._updateStorage(function(storage) {

                var stringifiedChangesToRemove = changesToRemove.map(JSON.stringify);
                var stringifiedStorage = storage.map(function(x) {
                    return {
                        stringifiedObject: JSON.stringify(x),
                        original: x
                    };
                });

                return stringifiedStorage.filter(function(x) {
                        return !stringifiedChangesToRemove.includes(x.stringifiedObject);
                    })
                    .map(function(x) { return x.original; });
            })
        });
    },

    _initializeStorageToEmptyArray: function() {
        return browser.storage.local.set({
            [changeTracker.storageKey]: []
        });
    },

    _pushChange: function(change) {
        return changeTracker._lastActionPromise = changeTracker._lastActionPromise.thenEvenIfError(function() {
            return changeTracker._updateStorage(function(storage) {
                storage.push(change);
                return storage;
            });

        });
    },

    _updateStorage: function(storageModifier) {
        return new Promise(function(resolve) {
            browser.storage.local.get(changeTracker.storageKey).then(function(storage) {
                var changeArray = storage[changeTracker.storageKey];
                if (!changeArray) {
                    changeArray = [];
                }

                changeArray = storageModifier(changeArray);

                browser.storage.local.set({
                    [changeTracker.storageKey]: changeArray
                }).then(resolve);
            })
        });
    }
};