#/bin/sh

rm -rf Bin

echo "Building Firefox for Android extension..."

mkdir --parents Bin/Firefox/Android
zip -r9 -j Bin/Firefox/Android/RealTimeTabSynchronizer.xpi \
	"Client/ThirdParty/jquery.signalr-2.2.1.min.js" \
	"Client/ThirdParty/jquery-3.2.1.min.js" \
	"Client/Firefox/Android/manifest.json" \
	"Server/signalr.hubs.js" \
	"Client/Firefox/Synchronizer.js" \
	"Client/Firefox/Android/Overrides.js" \

echo "Building Firefox Desktop extension..."

mkdir --parents Bin/Firefox/Desktop
zip -r9 -j Bin/Firefox/Desktop/RealTimeTabSynchronizer.xpi \
	"Client/ThirdParty/jquery.signalr-2.2.1.min.js" \
	"Client/ThirdParty/jquery-3.2.1.min.js" \
	"Client/Firefox/Desktop/manifest.json" \
	"Server/signalr.hubs.js" \
	"Client/Firefox/Synchronizer.js" \
	
echo "Building the server..."

mkdir --parents Bin/Server
cd Server
dotnet build -c Release
cd ..
cp -R Server/bin/Release Bin/Server
	
echo "Done"