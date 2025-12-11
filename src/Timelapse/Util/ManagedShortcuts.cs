using System;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable UnusedMember.Global

namespace Timelapse.Util
{
    /// <summary>
    /// Managed alternative to COM interop for handling Windows shortcuts
    /// Replaces IWshRuntimeLibrary and Shell32 COM references
    /// </summary>
    public static class ManagedShortcuts
    {
        /// <summary>
        /// Create a Windows shortcut file
        /// </summary>
        /// <param name="shortcutPath">Full path where the .lnk file should be created</param>
        /// <param name="targetPath">Path that the shortcut should point to</param>
        /// <param name="description">Description for the shortcut</param>
        /// <param name="workingDirectory">Working directory for the shortcut</param>
        /// <returns>True if shortcut was created successfully</returns>
        public static bool CreateShortcut(string shortcutPath, string targetPath, string description = "", string workingDirectory = "")
        {
            try
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                IShellLink link = (IShellLink)new ShellLink();
                
                // Configure the shortcut
                link.SetDescription(description);
                link.SetPath(targetPath);
                
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    link.SetWorkingDirectory(workingDirectory);
                }
                
                // Save the shortcut
                // ReSharper disable once SuspiciousTypeConversion.Global
                IPersistFile file = (IPersistFile)link;
                file.Save(shortcutPath, false);
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the target path of a shortcut file
        /// </summary>
        /// <param name="shortcutPath">Path to the .lnk file</param>
        /// <returns>Target path or empty string if failed</returns>
        public static string GetShortcutTarget(string shortcutPath)
        {
            try
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                IShellLink link = (IShellLink)new ShellLink();
                // ReSharper disable once SuspiciousTypeConversion.Global
                IPersistFile file = (IPersistFile)link;
                
                file.Load(shortcutPath, 0);
                
                StringBuilder sb = new(260);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    // COM interfaces for IShellLink
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    internal interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([In, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
    }
}