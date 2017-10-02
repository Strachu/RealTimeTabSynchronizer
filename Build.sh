#/bin/sh

rm -rf Bin

echo "Building Firefox for Android extension..."

mkdir --parents Bin/Firefox/Android
zip -r9 -j Bin/Firefox/Android/RealTimeTabSynchronizer.xpi \
	"Client/ThirdParty/jquery.signalr-2.2.1.min.js" \
	"Client/ThirdParty/jquery-3.2.1.min.js" \
	"Client/ThirdParty/guidgenerator.js" \
	"Client/Firefox/Android/manifest.json" \
	"Server/signalr.hubs.js" \
	"Client/Firefox/Helpers.js" \
	"Client/Firefox/Init.js" \
	"Client/Firefox/OfflineChangeTracking.js" \
	"Client/Firefox/Server.js" \
	"Client/Firefox/TabManager.js" \
	"Client/Firefox/Settings.html" \
	"Client/Firefox/Settings.page.js" \
	"Client/Firefox/Settings.js" \
	Client/Firefox/Android/Overrides*.js \

echo "Building Firefox Desktop extension..."

mkdir --parents Bin/Firefox/Desktop
zip -r9 -j Bin/Firefox/Desktop/RealTimeTabSynchronizer.xpi \
	"Client/ThirdParty/jquery.signalr-2.2.1.min.js" \
	"Client/ThirdParty/jquery-3.2.1.min.js" \
	"Client/ThirdParty/guidgenerator.js" \
	"Client/Firefox/Desktop/manifest.json" \
	"Server/signalr.hubs.js" \
	"Client/Firefox/Helpers.js" \
	"Client/Firefox/Init.js" \
	"Client/Firefox/OfflineChangeTracking.js" \
	"Client/Firefox/Server.js" \
	"Client/Firefox/TabManager.js" \
	"Client/Firefox/Settings.html" \
	"Client/Firefox/Settings.page.js" \
	"Client/Firefox/Settings.js" \
	Client/Firefox/Desktop/Overrides*.js \
	
echo "Building the server..."

mkdir --parents Bin/Server
cd Server
dotnet build -c Release
cd ..
cp -R Server/bin/Release Bin/Server
cp Server/appsettings.json Bin/Server/Release/netcoreapp2.0/
	
echo "Done"
