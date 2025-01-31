using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Util
{
    public static class ShortcutFiles
    {
        // Given a path, search it for shortcuts and return only paths to folders that exist.
        public static List<string> GetUniqueFolderTargetsFromPath(string folderPath)
        {
            return (GetUniqueFolderTargetsFromShortcutInfos(GetShortcutsInFolder(folderPath)));
        }

        // Given a list of shortcutInfos, return only paths to folders that exist.
        public static List<string> GetUniqueFolderTargetsFromShortcutInfos(List<ShortcutInfo> shortcutInfos)
        {
            List<string> targetFolders = new List<string>();

            foreach (ShortcutInfo shortcutInfo in shortcutInfos)
            {
                string prefixString = string.Empty;
                if (shortcutInfo.TargetExists && shortcutInfo.IsDirectory)
                {
                    targetFolders.Add(shortcutInfo.TargetPath);
                }
            }

            return targetFolders;
        }


        // Get a list of shortcuts in a folder as a list of ShortcutInfos that indicates:
        // - the shortcut path and its target
        // - whether the target exists and is to a file or folder
        public static List<ShortcutInfo> GetShortcutsInFolder(string folderPath)
        {
            string[] shortcutFiles;
            List<ShortcutInfo> shortcutInfos = new List<ShortcutInfo>();
            try
            {
                // Search the folder for .lnk (shortcut) files, if any
                shortcutFiles = System.IO.Directory.GetFiles(folderPath, "*.lnk");
            }
            catch
            {
                // Possible errors include DirectoryNotFound, UnauthorizedAccessException, PathTooLongException, etc.
                return shortcutInfos;
            }

            foreach (string shortcutPath in shortcutFiles)
            {
                string currentDestinationPath = GetTargetFromShortcut(shortcutPath);
                if (currentDestinationPath == string.Empty)
                {
                    continue;
                }

                ShortcutInfo shortcutInfo = new ShortcutInfo()
                {
                    SourcePath = shortcutPath,
                    TargetPath = currentDestinationPath,
                };

                try
                {
                    FileAttributes attr = File.GetAttributes(currentDestinationPath);
                    shortcutInfo.TargetExists = true;
                    shortcutInfo.IsDirectory = attr == FileAttributes.Directory;
                }
                catch
                {
                    shortcutInfo.TargetExists = false;
                    shortcutInfo.IsDirectory = false;
                }
                shortcutInfos.Add(shortcutInfo);
            }

            return shortcutInfos;
        }

        // Given a file path to a shortcut .lnk file, 
        // fill in the destination path the shortcut points to
        // or an error state
        public static string GetTargetFromShortcut(string shortcutPath)
        {
            try
            {
                if (System.IO.Path.GetExtension(shortcutPath).ToLower() != ".lnk")
                {
                    return string.Empty;
                }

                FileStream fileStream = File.Open(shortcutPath, FileMode.Open, FileAccess.Read);
                using (System.IO.BinaryReader fileReader = new BinaryReader(fileStream))
                {
                    fileStream.Seek(0x14, SeekOrigin.Begin);     // Seek to flags
                    uint flags = fileReader.ReadUInt32();        // Read flags
                    if ((flags & 1) == 1)
                    {                      // Bit 1 set means we have to
                                           // skip the shell item ID list
                        fileStream.Seek(0x4c, SeekOrigin.Begin); // Seek to the end of the header
                        uint offset = fileReader.ReadUInt16();   // Read the length of the Shell item ID list
                        fileStream.Seek(offset, SeekOrigin.Current); // Seek past it (to the file locator info)
                    }

                    long fileInfoStartsAt = fileStream.Position; // Store the offset where the file info
                                                                 // structure begins
                    uint totalStructLength = fileReader.ReadUInt32(); // read the length of the whole struct
                    fileStream.Seek(0xc, SeekOrigin.Current); // seek to offset to base pathname
                    uint fileOffset = fileReader.ReadUInt32(); // read offset to base pathname
                                                               // the offset is from the beginning of the file info struct (fileInfoStartsAt)
                    fileStream.Seek((fileInfoStartsAt + fileOffset), SeekOrigin.Begin); // Seek to beginning of
                                                                                        // base pathname (target)
                    long pathLength = (totalStructLength + fileInfoStartsAt) - fileStream.Position - 1; // read
                                                                                                        // the base pathname. I don't need the 2 terminating nulls.
                    char[] linkTarget = fileReader.ReadChars((int)pathLength); // should be unicode safe
                    var link = new string(linkTarget);

                    int begin = link.IndexOf("\0\0", StringComparison.InvariantCulture);
                    if (begin > -1)
                    {
                        int end = link.IndexOf("\\\\", begin + 2, StringComparison.InvariantCulture) + 2;
                        end = link.IndexOf('\0', end) + 1;

                        string firstPart = link.Substring(0, begin);
                        string secondPart = link.Substring(end);

                        return firstPart + secondPart;
                    }
                    else
                    {
                        return link;
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
    public class ShortcutInfo
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public bool TargetExists { get; set; }
        public bool IsDirectory { get; set; }
    }
}
