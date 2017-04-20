browser.tabs.onActivated.addListener(function(activeInfo)
{
	console.log("OnActivated:");
	console.log("TabId: " + activeInfo.tabId);
});

browser.tabs.onCreated.addListener(function(createdTab)
{
	console.log("OnCreated:");
	console.log(createdTab);
});

browser.tabs.onRemoved.addListener(function(tabId)
{
	console.log("OnRemoved:");
	console.log("TabId: " + tabId);
})

browser.tabs.onUpdated.addListener(function(tabId, changeInfo, tabInfo)
{
	console.log("OnUpdated:");
	console.log("{");
	console.log("TabId: " + tabId);
	console.log("Changed attributes: ");
	console.log(changeInfo);
	console.log("New tab Info: ");
	console.log(tabInfo);  
	console.log("}");
});

browser.tabs.onMoved.addListener(function(tabId, moveInfo)
{
	console.log("onMoved:");
	console.log("{");
	console.log("TabId: " + tabId);
	console.log("Move Info: ");
	console.log(moveInfo);  
	console.log("}");
});
