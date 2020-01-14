# VirtualRescene

## Warning!! This software is in alpha stage of developement.

### What is this? 
VirtualRescene is a userspace filesystem that is trying to emulate ReScene process.

### How does it work?
You provide it with a .srr file and extracted video file, with the metadata inside of .srr file and the video file itself, VirtualRescene is able to reconstruct files in scene format on the fly.

And it doesn't require double the disk space of the video file.
![](https://i.imgur.com/rSb2SJk.png)

### How to setup:
1. If you are not using Windows 10, you need to install [.Net Framework 4.6](https://dotnet.microsoft.com/download/dotnet-framework/net46), download and install "Run apps - Runtime" version.

2. Download and extract [pyReScene](https://bitbucket.org/Gfy/pyrescene/downloads/pyReScene-0.7-w32.zip).

3. Install [dokany](https://github.com/dokan-dev/dokany/releases). (Version 1.3.1.1000 is used in developement.)

   (This step needs your admin privilege... but it won't ask for that anymore after this)
   
4. Download VirtualRescene (in release page) and extract.

5. Give it the required command line argument.

```Usage: (all options are mendatory)
--srrexe (path to srr.exe)
--srrfile (path to a .srr file)
--video (path to a video file)
--drive (a drive letter to mount Virtual Rescene, like  V  , you need to pass a single letter)
```

### I need your help to test this filesystem

Please try to do the following:

1. Complete the setup and mount VirtualRescene partition.
2. Use a media player to open RAR file, does it play?
3. Can you verify your torrent against the generated file?
4. Is there any other quirks that I didn't account for?