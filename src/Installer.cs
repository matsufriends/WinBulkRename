using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SuperRename
{
    /// <summary>
    /// 右クリックメニューの登録／解除。HKCU のみ使うので管理者権限は不要。
    /// </summary>
    internal static class Installer
    {
        private const string VerbKeyName = "NouhinCollector";
        private const string ShellKeyPath = @"Software\Classes\*\shell\" + VerbKeyName;
        private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + VerbKeyName;

        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NouhinCollector");

        public static string InstalledExePath => Path.Combine(DataDir, "NouhinCollector.exe");

        public static string CurrentExePath => Application.ExecutablePath;

        public static bool IsInstalled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(ShellKeyPath + @"\command"))
                {
                    if (key == null) return false;
                    var cmd = key.GetValue("") as string;
                    return !string.IsNullOrEmpty(cmd) && File.Exists(InstalledExePath);
                }
            }
        }

        public static void Install()
        {
            Directory.CreateDirectory(DataDir);

            // 自分自身を固定の場所にコピー (ダウンロードした exe を消しても動くように)
            if (!string.Equals(Path.GetFullPath(CurrentExePath), Path.GetFullPath(InstalledExePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(CurrentExePath, InstalledExePath, true);
            }

            using (var key = Registry.CurrentUser.CreateSubKey(ShellKeyPath))
            {
                key.SetValue("", Program.AppName);
                key.SetValue("MUIVerb", Program.AppName);
                key.SetValue("Icon", "\"" + InstalledExePath + "\"");
                // 複数選択時に全ファイルに対してメニューを有効化 (既定だと 15 個までで消える)
                key.SetValue("MultiSelectModel", "Player");
            }
            using (var key = Registry.CurrentUser.CreateSubKey(ShellKeyPath + @"\command"))
            {
                key.SetValue("", "\"" + InstalledExePath + "\" \"%1\"");
            }

            // 「設定 > アプリ > インストールされているアプリ」からもアンインストールできるように
            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", Program.AppName);
                key.SetValue("DisplayVersion", Application.ProductVersion);
                key.SetValue("Publisher", Program.AppName);
                key.SetValue("DisplayIcon", InstalledExePath);
                key.SetValue("InstallLocation", DataDir);
                key.SetValue("UninstallString", "\"" + InstalledExePath + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExePath).Length / 1024), RegistryValueKind.DWord);
            }

            NotifyShell();
        }

        public static void Uninstall()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(ShellKeyPath, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false); } catch { }
            NotifyShell();

            if (File.Exists(InstalledExePath))
            {
                if (string.Equals(Path.GetFullPath(CurrentExePath), Path.GetFullPath(InstalledExePath), StringComparison.OrdinalIgnoreCase))
                {
                    // 自分自身は実行中に消せないので、終了後に消す
                    var psi = new ProcessStartInfo("cmd.exe",
                        "/c ping 127.0.0.1 -n 3 > nul & del /q \"" + InstalledExePath + "\" & rmdir /q \"" + DataDir + "\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };
                    Process.Start(psi);
                }
                else
                {
                    try { File.Delete(InstalledExePath); } catch { }
                    try { Directory.Delete(DataDir, true); } catch { }
                }
            }
        }

        private static void NotifyShell()
        {
            try { NativeMethods.SHChangeNotify(0x08000000 /*SHCNE_ASSOCCHANGED*/, 0, IntPtr.Zero, IntPtr.Zero); }
            catch { }
        }
    }
}
