using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// The modern Explorer-style folder chooser - address bar, sidebar,
    /// search, and a box you can paste a path into.
    ///
    /// WinForms' FolderBrowserDialog is still the Windows 2000-era
    /// SHBrowseForFolder tree: a cramped scrolling outline with no way to type
    /// a path. .NET 5 added an AutoUpgradeEnabled property that switches it to
    /// the Vista dialog, but this plugin targets .NET Framework 4.8, where that
    /// property does not exist. The only route is the COM interface the real
    /// dialog is built on: IFileOpenDialog with FOS_PICKFOLDERS, which is what
    /// Explorer, Office and every other modern application use.
    ///
    /// Falls back to FolderBrowserDialog if the COM call fails for any reason,
    /// so a folder can always be chosen even if this path breaks.
    /// </summary>
    internal static class FolderPicker
    {
        /// <summary>
        /// Shows the folder chooser. Returns the selected path, or null if the
        /// user cancelled.
        /// </summary>
        /// <param name="owner">Dialog owner, may be null.</param>
        /// <param name="title">Window title.</param>
        /// <param name="initialPath">Folder to start in; ignored if missing.</param>
        public static string Show(IWin32Window owner, string title, string initialPath = null)
        {
            try
            {
                return ShowVistaDialog(owner, title, initialPath);
            }
            catch (Exception)
            {
                // Any COM failure - unregistered interface, an OS that predates
                // the API, a shell extension misbehaving - falls back rather
                // than leaving the user unable to pick a folder at all.
                return ShowLegacyDialog(owner, title, initialPath);
            }
        }

        private static string ShowVistaDialog(IWin32Window owner, string title, string initialPath)
        {
            var dialog = (IFileDialog)new FileOpenDialogRcw();
            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);

                if (!string.IsNullOrEmpty(title))
                    dialog.SetTitle(title);

                if (!string.IsNullOrEmpty(initialPath) &&
                    System.IO.Directory.Exists(initialPath))
                {
                    IShellItem startItem;
                    var itemGuid = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref itemGuid, out startItem) == 0
                        && startItem != null)
                    {
                        dialog.SetFolder(startItem);
                        Marshal.ReleaseComObject(startItem);
                    }
                }

                IntPtr hwnd = owner != null ? owner.Handle : IntPtr.Zero;
                if (dialog.Show(hwnd) != 0) return null;   // non-zero = cancelled

                IShellItem result;
                dialog.GetResult(out result);
                if (result == null) return null;

                try
                {
                    string path;
                    result.GetDisplayName(SIGDN_FILESYSPATH, out path);
                    return path;
                }
                finally
                {
                    Marshal.ReleaseComObject(result);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }

        private static string ShowLegacyDialog(IWin32Window owner, string title, string initialPath)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = title;
                dlg.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(initialPath)) dlg.SelectedPath = initialPath;
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedPath : null;
            }
        }

        // ── COM plumbing ─────────────────────────────────────────────────
        //
        // Only the members actually called carry real signatures. The rest are
        // placeholders that exist purely to hold their slot: a COM interface is
        // dispatched by position in the vtable, so every method must be
        // declared in the original order even when unused. Never call one.

        private const uint FOS_PICKFOLDERS     = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST   = 0x00000800;
        private const uint SIGDN_FILESYSPATH   = 0x80058000;

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRcw { }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            // IModalWindow
            [PreserveSig] int Show(IntPtr parent);

            // IFileDialog - order is the contract; do not rearrange.
            void SetFileTypes();
            void SetFileTypeIndex();
            void GetFileTypeIndex();
            void Advise();
            void Unadvise();
            void SetOptions(uint fos);
            void GetOptions(out uint fos);
            void SetDefaultFolder();
            void SetFolder(IShellItem psi);
            void GetFolder();
            void GetCurrentSelection();
            void SetFileName();
            void GetFileName();
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel();
            void SetFileNameLabel();
            void GetResult(out IShellItem ppsi);
            void AddPlace();
            void SetDefaultExtension();
            void Close();
            void SetClientGuid();
            void ClearClientData();
            void SetFilter();
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler();
            void GetParent();
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes();
            void Compare();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);
    }
}
