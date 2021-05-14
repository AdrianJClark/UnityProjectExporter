// Export Project To Unity Package
// -------------------------------
// Adrian Clark
// adrian.clark@canterbury.ac.nz
// -------------------------------
// First Release Nov 2020
// Updated March 2020
// - fixed bug preventing MacOS/Windows cross platform compatibility

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using System;
using System.Text;
using System.IO;
using System.IO.Compression;

// This class represents the Meta Data present in a TAR archive for
// an individual file. It is not a complete implementation.
//
// This file is based on the TAR archive file format as defined at
// https://en.wikipedia.org/wiki/Tar_(computing)
public class TarFileMetaData
{
    //Pre-Posix TAR specification fields
    public string fileName;
    public string fileMode;
    public string ownerID;
    public string groupID;
    public long fileSize;
    public long lastModifiedTime;
    public long checkSum;
    public string typeFlag;
    public string linkName;

    //UStar TAR specification fields
    public string ustarIndicator;
    public string ustarVersion;
    public string ownerUserName;
    public string ownerGroupName;
    public string deviceMajorNumber;
    public string deviceMinorNumber;
    public string filenamePrefix;

    //The various type flags - this is not a complete list
    //Some type flags are alphanumeric, so an enum may not be the best
    public enum TypeFlags
    {
        Unknown = -1,
        NormalFile = 0,
        HardLink = 1,
        SymbolicLink = 2,
        CharacterSpecial = 3,
        BlockSpecial = 4,
        Directory = 5,
        FIFO = 6,
        ContiguousFile = 7,
    };

    // Returns true if the ustar indicator is set
    public bool isUStar
    {
        get
        {
            return ustarIndicator.Equals("ustar", StringComparison.InvariantCultureIgnoreCase);
        }
    }

    // Returns the matching TypeFlag enum value
    public TypeFlags GetTypeFlags()
    {
        int parsedTypeFlag = -1;
        if (int.TryParse(typeFlag, out parsedTypeFlag))
            return (TypeFlags)parsedTypeFlag;

        return TypeFlags.Unknown;
    }

    // Helper function to get a trimmed string from a byte array, and update
    // the bufferposition accordingly
    string ExtractStringFromByteArray(byte[] buffer, int length, ref int bufferPos)
    {
        string s = Encoding.ASCII.GetString(buffer, bufferPos, length).Trim().Trim('\0');
        bufferPos += length;
        return s;
    }

    // Returns the checksum of the Metadata
    public long CalculateCheckSum(byte[] buffer)
    {
        long checksum = 0;
        for (int i = 0; i < buffer.Length; i++)
            checksum += buffer[i];

        return checksum;
    }

    // Writes a string of a maximum length into the byte array
    // at a certain position
    int WriteStringToByteArray(string s, ref byte[] buffer, int maxLength, int bufferPos)
    {
        Encoding.ASCII.GetBytes(s, 0, s.Length < maxLength ? s.Length : maxLength, buffer, bufferPos);
        return maxLength;
    }

    // Create a byte array for the Meta Data, optionally recalculating the
    // Checksum
    public byte[] ToByteArray(bool recalculateChecksum = false)
    {
        byte[] buffer = new byte[512];

        int bufferPos = 0;

        // If the filename is null it is an invalid entry
        if (!String.IsNullOrWhiteSpace(fileName))
        {
            // Pre-Posix Fields
            bufferPos += WriteStringToByteArray(fileName, ref buffer, 100, bufferPos);
            bufferPos += WriteStringToByteArray(fileMode, ref buffer, 8, bufferPos);
            bufferPos += WriteStringToByteArray(ownerID, ref buffer, 8, bufferPos);
            bufferPos += WriteStringToByteArray(groupID, ref buffer, 8, bufferPos);

            // Pad the file size with 0 to a string length of 11
            string sFileSize = Convert.ToString(fileSize, 8).PadLeft(11, '0');
            bufferPos += WriteStringToByteArray(sFileSize + " ", ref buffer, 12, bufferPos);

            string sLastModifiedTime = Convert.ToString(lastModifiedTime, 8);
            bufferPos += WriteStringToByteArray(sLastModifiedTime + " ", ref buffer, 12, bufferPos);

            // If we're recalculating the checksum, set the existing checksum
            // To spaces as per the TAR file format
            string sCheckSum;
            if (recalculateChecksum)
                sCheckSum = "        ";
            else
                //Otherwise pad with 0 to a string length of 6
                sCheckSum = Convert.ToString(checkSum, 8).PadLeft(6, '0');
            bufferPos += WriteStringToByteArray(sCheckSum + "\0 ", ref buffer, 8, bufferPos);

            bufferPos += WriteStringToByteArray(typeFlag, ref buffer, 1, bufferPos);
            bufferPos += WriteStringToByteArray(linkName, ref buffer, 100, bufferPos);

            // UStar Fields
            bufferPos += WriteStringToByteArray(ustarIndicator, ref buffer, 6, bufferPos);
            bufferPos += WriteStringToByteArray(ustarVersion, ref buffer, 2, bufferPos);
            bufferPos += WriteStringToByteArray(ownerUserName, ref buffer, 32, bufferPos);
            bufferPos += WriteStringToByteArray(ownerGroupName, ref buffer, 32, bufferPos);
            bufferPos += WriteStringToByteArray(deviceMajorNumber, ref buffer, 8, bufferPos);
            bufferPos += WriteStringToByteArray(deviceMinorNumber, ref buffer, 8, bufferPos);
            bufferPos += WriteStringToByteArray(filenamePrefix, ref buffer, 155, bufferPos);

            // If we're recalculating the checksum, do that now and then
            // insert the value at position 148
            if (recalculateChecksum)
            {
                sCheckSum = Convert.ToString(CalculateCheckSum(buffer), 8).PadLeft(6, '0');
                WriteStringToByteArray(sCheckSum + "\0 ", ref buffer, 8, 148);
            }
        }

        return buffer;
    }

    // Constructor which populates fields based a byte buffer
    public TarFileMetaData(byte[] buffer)
    {
        int bufferPos = 0;

        fileName = ExtractStringFromByteArray(buffer, 100, ref bufferPos);
        // If the filename is null it is an invalid entry
        if (!String.IsNullOrWhiteSpace(fileName))
        {
            // Pre-Posix Fields
            fileMode = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
            ownerID = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
            groupID = ExtractStringFromByteArray(buffer, 8, ref bufferPos);

            // Filesize is stored in Octets
            string sFileSize = ExtractStringFromByteArray(buffer, 12, ref bufferPos);
            fileSize = Convert.ToInt64(sFileSize.Trim(), 8);

            // Last Modified Time is stored in Octets
            string sLastModifiedTime = ExtractStringFromByteArray(buffer, 12, ref bufferPos);
            lastModifiedTime = Convert.ToInt64(sLastModifiedTime.Trim(), 8);

            // Checksum is stored in Octets
            string sCheckSum = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
            checkSum = Convert.ToInt64(sCheckSum.Trim(), 8);

            typeFlag = ExtractStringFromByteArray(buffer, 1, ref bufferPos);

            linkName = ExtractStringFromByteArray(buffer, 100, ref bufferPos);

            // UStar Fields
            ustarIndicator = ExtractStringFromByteArray(buffer, 6, ref bufferPos);
            ustarVersion = ExtractStringFromByteArray(buffer, 2, ref bufferPos);
            ownerUserName = ExtractStringFromByteArray(buffer, 32, ref bufferPos);
            ownerGroupName = ExtractStringFromByteArray(buffer, 32, ref bufferPos);
            deviceMajorNumber = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
            deviceMinorNumber = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
            filenamePrefix = ExtractStringFromByteArray(buffer, 155, ref bufferPos);
        }
    }

    // Constructor which manually populates fields from values
    public TarFileMetaData(
        string fileName, string fileMode = "000000", string ownerID = "000000", string groupID = "000000",
        long fileSize = 0, long lastModifiedTime = 0, string typeFlag = "0", string linkName = "", bool isUStar = true,
        string ownerUserName = "", string ownerGroupName = "", string deviceMajorNumber = "000000", string deviceMinorNumber = "000000", string filenamePrefix = "")
    {
        this.fileName = fileName;
        this.fileMode = fileMode;
        this.ownerID = ownerID;
        this.groupID = groupID;

        this.fileSize = fileSize;
        this.lastModifiedTime = lastModifiedTime;
        this.typeFlag = typeFlag;
        this.linkName = linkName;

        // If it is a UStar TAR Archive, populate the Indicator and Version
        if (isUStar)
        {
            ustarIndicator = "ustar";
            ustarVersion = "00";
        }

        this.ownerUserName = ownerUserName;
        this.ownerGroupName = ownerGroupName;
        this.deviceMajorNumber = deviceMajorNumber;
        this.deviceMinorNumber = deviceMinorNumber;
        this.filenamePrefix = filenamePrefix;
    }

    // Helper function to create default Meta Data for a File
    public static TarFileMetaData CreateDefaultFileMetaData(string fileName, long fileSize = 0, long lastModifiedTime=0, string ownerUserName = "", string ownerGroupName = "")
    {
        return new TarFileMetaData(fileName, "000644", "000765", "000024", fileSize, lastModifiedTime, "0", "", true, ownerUserName, ownerGroupName);
    }

    // Helper function to create default Meta Data for a Directory
    public static TarFileMetaData CreateDefaultDirectoryMetaData(string directoryName, long lastModifiedTime = 0, string ownerUserName = "", string ownerGroupName = "")
    {
        return new TarFileMetaData(directoryName, "000755", "000765", "000024", 0, lastModifiedTime, "5", "", true, ownerUserName, ownerGroupName);
    }

    // Print out the various fields for the Meta Data
    public override string ToString()
    {
        if (!String.IsNullOrWhiteSpace(fileName))
            return
                "File Name: " + fileName + "\n" +
                "File Mode: " + fileMode + "\n" +
                "Owner ID: " + ownerID + "\n" +
                "Groud ID: " + groupID + "\n" +
                "File Size (bytes): " + fileSize + "\n" +
                "Last Modified Time: " + DateTimeOffset.FromUnixTimeSeconds(lastModifiedTime).ToString() + "\n" +
                "Checksum: " + checkSum + "\n" +
                "Type Flag: " + typeFlag + " (" + GetTypeFlags() + ")\n" +
                "UStar Indicator: " + ustarIndicator + "\n" +
                "UStar Version: " + ustarVersion + "\n" +
                "Owner User Name: " + ownerUserName + "\n" +
                "Owner Group Name: " + ownerGroupName + "\n" +
                "Device Major Number: " + deviceMajorNumber + "\n" +
                "Device Minor Number: " + deviceMinorNumber + "\n" +
                "Filename Prefix: " + filenamePrefix;
        else
            return null;
    }
}

// This Class contains a function to export an entire Unity project (including
// Project Settings and Packages Manifest) to a Unity Package File
public class ExportProjectToUnityPackage : MonoBehaviour
{
    // Add "Export Entire Project" Menu Item
    [MenuItem("Assets/Export Entire Project")]
    public static void ExportEntireProject()
    {
        // Get all assets in the project
        string[] allAssets = AssetDatabase.GetAllAssetPaths();

        List<string> assetPathAssets = new List<string>();

        // For each asset, check that it's either in the Assets or ProjectSettings
        // Directory, and that it is not a directory
        foreach (string asset in allAssets)
            if (asset.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) ||
                asset.StartsWith("ProjectSettings/", System.StringComparison.OrdinalIgnoreCase))
                if (!File.GetAttributes(asset).HasFlag(FileAttributes.Directory))
                    assetPathAssets.Add(asset);

        // Add the Packages Manifest
        assetPathAssets.Add("Packages/manifest.json");

        // Get the name of the file to save our project to.
        string filename = EditorUtility.SaveFilePanel("Select where you would like your Exported Project archive saved", "", "MyUnityProject.unitypackage", "unitypackage");

        // If the filename exists, save the file, then open the explorer
        if (filename.Length > 0)
        {
            CreateNewTar(filename, assetPathAssets.ToArray());
            EditorUtility.RevealInFinder(filename);
        }
    }


    static void CreateNewTar(string outputFile, string[] files)
    {
        // Open a gzipped stream for the file
        using (var stream = File.OpenWrite(outputFile))
        {
            using (var gzip = new GZipStream(stream, CompressionMode.Compress))
            {
                // Meta Data and Files must end of a 512 byte boundary
                // In the format - if we don't land there we'll have to pad
                // until we reach the next boundary
                int paddingBytes;
                byte[] bPaddingBytes;

                // Getting the username is easy, haven't found a simple
                // crossplatform way to get the users group yet though
                string userName = Environment.UserName;
                string userGroup = Environment.UserName;

                // Loop through each file
                foreach (string file in files)
                {
                    // Get the GUID. If there is no GUID, assume it's the
                    // Package Manager Manifest File
                    string assetGUID = AssetDatabase.AssetPathToGUID(file);
                    if (string.IsNullOrEmpty(assetGUID)) assetGUID = "packagemanagermanifest";

                    // Use the last write time of the asset for the last modified
                    // times for the TAR files
                    long currentTime = new DateTimeOffset(File.GetLastWriteTime(file)).ToUnixTimeSeconds();

                    // Create the TAR entry for the files directory based
                    // on the Asset GUID
                    TarFileMetaData assetContainerMD = TarFileMetaData.CreateDefaultDirectoryMetaData(assetGUID, currentTime, userName, userGroup);
                    byte[] bAssetContainerMD = assetContainerMD.ToByteArray(true);
                    gzip.Write(bAssetContainerMD, 0, bAssetContainerMD.Length);

                    // Read the files data, then create the TAR entry for the
                    // file and add the file out too
                    byte[] assetData = File.ReadAllBytes(file);
                    TarFileMetaData assetMD = TarFileMetaData.CreateDefaultFileMetaData(assetGUID + "/asset", assetData.Length, currentTime, userName, userGroup);
                    byte[] bAssetMD = assetMD.ToByteArray(true);
                    gzip.Write(bAssetMD, 0, bAssetMD.Length);
                    gzip.Write(assetData, 0, assetData.Length);

                    // Pad until we reach a 512 byte boundary if needed
                    if (assetData.Length % 512 > 0)
                    {
                        paddingBytes = 512 - (assetData.Length % 512);
                        bPaddingBytes = new byte[paddingBytes];
                        gzip.Write(bPaddingBytes, 0, paddingBytes);
                    }

                    // If there's a meta data file, create a TAR entry for that
                    // too, and add the meta data to the TAR
                    if (File.Exists(file + ".meta"))
                    {
                        long currentTimeMeta = new DateTimeOffset(File.GetLastWriteTime(file + ".meta")).ToUnixTimeSeconds();
                        byte[] assetMetaData = File.ReadAllBytes(file + ".meta");
                        TarFileMetaData assetMetaMD = TarFileMetaData.CreateDefaultFileMetaData(assetGUID + "/asset.meta", assetMetaData.Length, currentTimeMeta, userName, userGroup);
                        byte[] bAssetMetaMD = assetMetaMD.ToByteArray(true);
                        gzip.Write(bAssetMetaMD, 0, bAssetMetaMD.Length);
                        gzip.Write(assetMetaData, 0, assetMetaData.Length);

                        // Pad until we reach a 512 byte boundary if needed
                        if (assetMetaData.Length % 512 > 0)
                        {
                            paddingBytes = 512 - (assetMetaData.Length % 512);
                            bPaddingBytes = new byte[paddingBytes];
                            gzip.Write(bPaddingBytes, 0, paddingBytes);
                        }
                    }

                    // Create a TAR entry for a "pathname" file, which contains
                    // The assets original path in the project
                    byte[] pathName = Encoding.ASCII.GetBytes(file);
                    TarFileMetaData pathNameMD = TarFileMetaData.CreateDefaultFileMetaData(assetGUID + "/pathname", pathName.Length, currentTime, userName, userGroup);
                    byte[] bPathNameMD = pathNameMD.ToByteArray(true);
                    gzip.Write(bPathNameMD, 0, bPathNameMD.Length);
                    gzip.Write(pathName, 0, pathName.Length);

                    // Pad until we reach a 512 byte boundary if needed
                    if (pathName.Length % 512 > 0)
                    {
                        paddingBytes = 512 - (pathName.Length % 512);
                        bPaddingBytes = new byte[paddingBytes];
                        gzip.Write(bPaddingBytes, 0, paddingBytes);
                    }

                }

                // "The end of an archive is marked by at least 
                // two consecutive zero-filled records"
                bPaddingBytes = new byte[1024];
                gzip.Write(bPaddingBytes, 0, 1024);

                //Flush and close after we've written all the files
                gzip.Flush();
                gzip.Close();
            }
            stream.Close();
        }
    }

}
