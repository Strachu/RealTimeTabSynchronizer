Promise.prototype.thenEvenIfError = function(func) {
    return this.then(func, func);
}