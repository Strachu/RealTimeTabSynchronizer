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
- Mozilla Firefox (desktop) 52.0.2 or higher - it's possible that the adding will work with lower version however its the lowest version it was tested on,
- Firefox For Android 54.0 or higher

# Server requirements
To run the application you need:
- Any modern linux distribution (tested on Xubuntu 16.04), Windows (not tested), Mac OS X (not tested) or Raspbian Jessie (or later).
- [.NET Core 1.1 Runtime](https://www.microsoft.com/net/download/core#/runtime)

Additionally, if you want to compile the server you need:
- [.NET Core 1.1 SDK](https://www.microsoft.com/net/download/core#/sdk)

# Download
The binary releases and their corresponding source code snapshots can be downloaded at the [releases](https://github.com/Strachu/RealTimeTabSynchronizer/releases) page.

If you would like to retrieve the most up to date source code and build the server and browser addins yourself, install git
and clone the repository by executing the command:
`git clone https://github.com/Strachu/FileArchiver.git` or alternatively, click the "Download ZIP" button at the side
panel of this page.

# Installation
## Ubuntu 16.04 / Raspbian Jessy
1. Install [.NET Core 1.1 Runtime](https://www.microsoft.com/net/download/linux) if haven't done yet,
2. TODO

# Building
## Ubuntu 16.04 / Raspbian Jessy
1. Install [.NET Core 1.1 SDK](https://www.microsoft.com/net/download/linux)
2. Open terminal and `cd` into the directory root of RealTimeTabSynchronizer:  
`cd /path/to/RealTimeTabSynchronizer`
3. Execute build script:  
`chmod +x Build.sh`  
`./Build.sh`  
4. Done. Building results are in a "Bin" directory.

## Windows
1. Install Linux
2. Go to ubuntu building instruction

# Libraries
The application uses the following libraries:
**TODO**

# Tools
During the creation of the application the following tools were used:
**TODO**

# License
RealTimeTabSynchronizer is a free software distributed under the GNU LGPL 3 or later license.

See LICENSE.txt and LICENSE_THIRD_PARTY.txt for more information.

The most important point is:  
You can use, modify and distribute the application freely without any charge but you have to make public free of charge any changes to the source code (on LGPL 3 license) of the application *if* you modify the application and distribute the modified version.
