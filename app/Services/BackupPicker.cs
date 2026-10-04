using System.Runtime.InteropServices;

namespace JiYaoChu.Services;

/// <summary>The desktop common dialog works in the elevated, unpackaged host.</summary>
internal static class BackupPicker
{
    public static Task<string?> PickAsync()
    {
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = BackgroundHost.Handle;
        var thread = new Thread(() =>
        {
            IFileDialog? dialog = null;
            IShellItem? item = null;
            try
            {
                dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"))!)!;
                dialog.SetFileTypes(1, [new Filter { Name = "机耀处设置备份 (*.zip)", Spec = "*.zip" }]);
                dialog.SetOptions(0x40 | 0x800 | 0x1000 | 0x10000000); // filesystem, existing path/file, no recent list
                dialog.SetTitle("选择设置备份");
                var hr = dialog.Show(owner);
                if (hr == unchecked((int)0x800704C7)) { result.SetResult(null); return; }
                Marshal.ThrowExceptionForHR(hr);
                dialog.GetResult(out item);
                item.GetDisplayName(0x80058000, out var pointer); // SIGDN_FILESYSPATH
                try { result.SetResult(Marshal.PtrToStringUni(pointer)); }
                finally { Marshal.FreeCoTaskMem(pointer); }
            }
            catch (Exception error) { result.SetException(error); }
            finally { if (item is not null) Marshal.FinalReleaseComObject(item); if (dialog is not null) Marshal.FinalReleaseComObject(dialog); }
        }) { IsBackground = true, Name = "LumaDesk backup picker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
    public static bool Confirm(BackupPreview preview, bool hardware) => MessageBox(BackgroundHost.Handle,
        "将恢复此备份的应用偏好与资料：\n" + preview.ArchivePath + "\n\n" + (hardware ? "已选择同时恢复确认支持的硬件设置。\n" : "硬件设置保持当前值。\n") + "恢复前会自动保存当前设置备份。是否继续？",
        "确认恢复设置", 0x4 | 0x30 | 0x100) == 6;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint owner, string text, string caption, uint type);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Filter { [MarshalAs(UnmanagedType.LPWStr)] public string Name; [MarshalAs(UnmanagedType.LPWStr)] public string Spec; }
    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] Filter[] filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName(out nint name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetResult(out IShellItem item);
    }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint bind, in Guid handler, in Guid iid, out nint value);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint type, out nint value);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
}
