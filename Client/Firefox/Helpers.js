Promise.prototype.thenEvenIfError = function(func) {
    return this.then(func, function(error) {
        console.error(error);
        return func();
    });
}