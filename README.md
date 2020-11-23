# UnityProjectExporter
Export complete Unity Projects to a UnityPackage file (including project settings and packages).

<b>Overview</b><br/>
As best I can tell, there's no straight forward way to share a complete Unity project other than zipping up the entire project folder. This either means that a whole bunch of automatically generated files get included (often at least doubling the size of the zip file), or you have to manually exclude certain folders (which, for students just learning about programming, is just one more thing to remember). Alternatively, you can export a UnityPackage of your project, but this only includes assets, not the project settings or packages, which means that you lose information like Tags (which can break your game), as well as any packages included (including things like Post Processing Effects). Unity does support complete project UnityPackages, but it seems the only way to create them is uploading a project to the asset store, which isn't really ideal for student projects.

<b>Usage</b><br/>
This script automatically exports all assets in a package, as well as everything in the ProjectSettings folder, and the Package Manager Manifest. To use, just click on the "Export Entire Package" menu item in the Assets Menu, or by right clicking on the project window. You'll be prompted where you want to save your UnityPackage too, and then the script will export out your package.

<b>Details</b><br/>
After some research I found out that UnityPackages are just gzipped tar files. Fortunately GZip functionality is provided by System.IO.Compression.GZipStream, but unfortunately I couldn't find a lightweight TAR handler, and I really just wanted a single script that students could drop into their project and use. Using the Wikipedia entry on the TAR file format, I built a very small TAR implementation which was enough to create a working UnityPackage.

This has been tested and is working on Unity 2019, but should work on older and newer versions. Perhaps eventually Unity will add this functionality and it will no longer be required.

<b>Suggested Improvements</b><br/>
For larger projects, it can be a bit slow to generate the UnityPackage, and currently no feedback is provided. A cancelable progress bar window would be helpful here.
