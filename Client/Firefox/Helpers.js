Promise.prototype.thenEvenIfError = function(func) {
    return this.then(func, function(error) {
        console.error(error);
        return func();
    });
}

Array.prototype.remove = function(item) {
    var index = this.indexOf(item);
    var removed = false;
    while (index !== -1) {
        this.splice(index, 1);

        index = this.indexOf(item, index);
        removed = true;
    }

    return removed;
}