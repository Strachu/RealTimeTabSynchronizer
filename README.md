# RealTimeTabSynchronizer
**RealTimeTabSynchronizer** is an utility for synchronization in real-time of open tabs between browsers on different machines.

The main use case for the application is:

> *As* a Internet user  
> *I want* to have the same tabs open on my desktop and tablet browser  
> *So that* I can anytime continue reading at the point I had left without wasting my time for manual opening of tabs.

The application consists of 2 parts:
1. A client - a browser addin which automatically sends the information about currently open tabs to the server,
2. A server - a desktop application based on .NET Core which incorporates all logic needed to synchronize the tabs between multiple clients and manage all conflicts in the case of independent changes on multiple browsers.

It's a self host solution which means that to use the utility you need to host the server yourself. 
This allows you to keep a maximum privacy as the history of your browsing does not leave your local network at the expense of 
maintaining your own server which needs to be online for synchronization.

Synchronization of tabs for multiple users with single server instance is currently **not** supported.
Although contributions are welcome.

**Note: The software is in very early development stage. It's currently not usable. Installing it is discouraged unless you want to contribute as using the application in this stage can result in data loss.**

# Features
- Synchronization of open tabs in real-time,
- Automatic resynchronization during connection recovery after previous connection loss,
- Conflict resolving in the case of making independent changes on multiple browsers in offline mode, 
- Easy extending to new browsers due to doing almost all logic server side,
- Cross platform server software supporting all main desktop OSes and Raspberry Pi,

# Supported browsers
- Mozilla Firefox (desktop) **55.0**,
- Firefox For Android **56.0**

# Server requirements
To run the application you need:
- Any modern linux distribution (tested on Xubuntu 16.04), Windows (not tested), Mac OS X (not tested) or Raspbian Jessie (or later).
- [.NET Core 2.0 Runtime](https://www.microsoft.com/net/download/core#/runtime)

Additionally, if you want to compile the server you need:
- [.NET Core 2.0 SDK](https://www.microsoft.com/net/download/core#/sdk)

# Download
The binary releases and their corresponding source code snapshots can be downloaded at the [releases](https://github.com/Strachu/RealTimeTabSynchronizer/releases) page.

If you would like to retrieve the most up to date source code and build the server and browser addins yourself, install git
and clone the repository by executing the command:
`git clone https://github.com/Strachu/RealTimeTabSynchronizer.git` or alternatively, click the "Download ZIP" button at the side
panel of this page.

## Ubuntu 16.04 / Raspbian Jessy
1. Install .NET Core 2.0 Runtime if haven't done yet:  
a) **Ubuntu**: download the [.NET Core 2.0 Runtime](https://www.microsoft.com/net/download/linux),  
b) **Raspbian**: follow the instructions in section *Task: Install the .NET Core Runtime on the Raspberry Pi* at [Setting up Raspian and .NET Core 2.0 on a Raspberry Pi](https://blogs.msdn.microsoft.com/david/2017/07/20/setting_up_raspian_and_dotnet_core_2_0_on_a_raspberry_pi/) (but **change the .net core url to https://dotnetcli.blob.core.windows.net/dotnet/Runtime/2.0.0/dotnet-runtime-2.0.0-linux-arm.tar.gz** as the newer prelease builds of runtime are not compatible with apps requesting stable runtime)
:
```
sudo apt-get install curl libunwind8 gettext.  
curl -sSL -o dotnet.tar.gz https://dotnetcli.blob.core.windows.net/dotnet/Runtime/2.0.0/dotnet-runtime-2.0.0-linux-arm.tar.gz
sudo mkdir -p /opt/dotnet && sudo tar zxf dotnet.tar.gz -C /opt/dotnet
sudo ln -s /opt/dotnet/dotnet /usr/local/bin
```
2. [Download](#download) the binary release or [build](#building) the application yourself.
3. [Optional] Modify the [*appsettings.json*](#configuration) configuration file,
4. Copy the entire content of server binaries directory to a destination directory from which the service should run (such as /opt or /srv)
5. [Optional] Create a dedicated user for the service:
```
sudo adduser --system --no-create-home --disabled-login tabsynchronizer
sudo addgroup tabsynchronizer
sudo usermod -g tabsynchronizer tabsynchronizer
```
6. If created new user for the service, remember to grant him permissions (at least read) to the service directory,
7. Install the server application as a service:
```
echo '[Unit]
Description=A server component for RealTimeTabSynchronizer
Wants=network-online.target
After=network.target network-online.target

[Service]
Type=simple
User=tabsynchronizer
ExecStart=/usr/local/bin/dotnet /srv/RealTimeTabSynchronizer.Server/RealTimeTabSynchronizer.Server.dll
WorkingDirectory=/srv/RealTimeTabSynchronizer.Server

[Install]
WantedBy=default.target' | sudo tee /etc/systemd/system/RealTimeTabSynchronizer.Server.service
sudo chmod 754 /etc/systemd/system/RealTimeTabSynchronizer.Server.service
sudo systemctl daemon-reload
sudo systemctl enable RealTimeTabSynchronizer.Server.service
sudo systemctl start RealTimeTabSynchronizer.Server.service
```
8. TODO Client installation

# Building
## Ubuntu 16.04 / Raspbian Jessy
1. Install [.NET Core 2.0 SDK](https://www.microsoft.com/net/download/linux)
2. Open terminal and `cd` into the directory root of RealTimeTabSynchronizer:  
`cd /path/to/RealTimeTabSynchronizer`
3. Execute build script:  
`chmod +x Build.sh`  
`./Build.sh`  
4. Done. Building artifacts are in a *Bin* directory. The deployable server binaries are available under path *Bin/Server/Release/netcoreapp2.0/publish/*

## Windows
1. Install Linux (or translate the `Build.sh` script to a script interpretable by Windows)
2. Go to ubuntu building instruction

# Configuration
Under the server directory there is a JSON file named *appsettings.json*. This file can be used to configure the server.
## Available options
### Database
Configures the database to use for storing the server data. By default the server is configured to use SQLite database which does not require any configuration.

Configuration properties:
- DatabaseType - indicates which database engine to use. Possible values are "Sqlite" and "Postgresql",
- ConnectionString - the connection string indicating how to connect to the database.
#### SQLite
By default the server is configured to use SQLite and store the database in a file named realtimetabsynchronizer.db.
```
    "Database": {
        "DatabaseType": "Sqlite",
        "ConnectionString": "Data Source=realtimetabsynchronizer.db;"
    },
```
#### Postgresql
You can also tell the server to store all data in a postgresql. It can be usefull if you want to browser the database remotely. Before the server can use the database engine you have to create an empty database and an user with DDL and DML rights which the server will user to connect to the database.
```
    "Database": {
        "DatabaseType": "Postgresql",
        "ConnectionString": "Host=192.168.0.2;Database=realtimetabsynchronizer;Username=user;Password=pass;"
    },
```
### server.urls
Tells the server at which url and port to listen for connections. The default is to listen at any address at port 31711.
```
    "server.urls": "http://+:31711/",
```

# Libraries
The application uses the following libraries:
- .NET Core 2.0
- [SignalR 0.2.0](http://signalr.net/)
- [Entity Framework Core 1.1.1](https://github.com/aspnet/EntityFrameworkCore)
- [Newtonsoft.Json](http://www.newtonsoft.com/json) - JSON parsing.
- [NUnit 3.7.1](http://www.nunit.org/) as a unit test framework.
- [Moq 4.7.99](https://github.com/moq/moq4) as a test mock framework.
- [FluentAssertions 4.19.4](http://fluentassertions.com/) assertions helpers.

# Tools
During the creation of the application the following tools were used:
- [Visual Studio Code](https://www.pgadmin.org/)
- [Git](https://git-scm.com/)
- [Git Extensions](https://github.com/gitextensions/gitextensions)
- [pgAdmin](https://www.pgadmin.org/)

# License
RealTimeTabSynchronizer is a free software distributed under the GNU LGPL 3 or later license.

See LICENSE.txt and LICENSE_THIRD_PARTY.txt for more information.

The most important point is:  
You can use, modify and distribute the application freely without any charge but you have to make public free of charge any changes to the source code (on LGPL 3 license) of the application *if* you modify the application and distribute the modified version.
