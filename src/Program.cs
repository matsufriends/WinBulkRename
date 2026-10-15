using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SuperRename
{
    internal static class Program
    {
        public const string AppName = "納品これくたーくん";

        // 多重起動の集約用
        private const string LockMutexName = "Local\\NouhinCollector_Lock";
        private const string MasterMutexName = "Local\\NouhinCollector_Master";

        private static string PendingFilePath =>
            Path.Combine(Installer.DataDir, "pending.txt");

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                if (args.Length == 0)
                {
                    Application.Run(new SettingsForm());
                    return 0;
                }

                if (args.Length == 1 && args[0].Equals("--install", StringComparison.OrdinalIgnoreCase))
                {
                    Installer.Install();
                    return 0;
                }
                if (args.Length == 1 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    var answer = MessageBox.Show(
                        AppName + " を削除しますか？",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1,
                        MessageBoxOptions.DefaultDesktopOnly);
                    if (answer != DialogResult.Yes) return 0;
                    Installer.Uninstall();
                    MessageBox.Show("削除しました。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                    return 0;
                }

                // ファイルが渡された: エクスプローラーは選択ファイル1つにつき1プロセス起動するので集約する
                var files = CollectFiles(args);
                if (files == null) return 0;          // 自分はマスターではない → 他プロセスに託して終了
                if (files.Count == 0) return 0;

                using (var form = new RenameForm(files))
                {
                    Application.Run(form);
                }
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("エラーが発生しました。\n\n" + ex, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        /// <summary>
        /// 自分の引数を共有ファイルに追記し、マスターなら一定時間待って全件回収して返す。
        /// マスターでなければ null。
        /// </summary>
        private static List<string> CollectFiles(string[] args)
        {
            Directory.CreateDirectory(Installer.DataDir);

            using (var lockMutex = new Mutex(false, LockMutexName))
            {
                // 1) 自分の分を追記
                WithLock(lockMutex, () =>
                {
                    File.AppendAllLines(PendingFilePath, args.Where(a => !string.IsNullOrWhiteSpace(a)), Encoding.UTF8);
                });

                // 2) マスターになれるか
                bool createdNew;
                using (var master = new Mutex(true, MasterMutexName, out createdNew))
                {
                    if (!createdNew) return null;

                    try
                    {
                        // 3) 追記が止まるまで待つ (最後の更新から 400ms 静かなら確定、最長 5 秒)
                        long lastLen = -1;
                        var quietSince = DateTime.UtcNow;
                        var start = DateTime.UtcNow;
                        while (true)
                        {
                            Thread.Sleep(100);
                            long len = WithLock(lockMutex, () => File.Exists(PendingFilePath) ? new FileInfo(PendingFilePath).Length : 0);
                            if (len != lastLen)
                            {
                                lastLen = len;
                                quietSince = DateTime.UtcNow;
                            }
                            else if ((DateTime.UtcNow - quietSince).TotalMilliseconds >= 400)
                            {
                                break;
                            }
                            if ((DateTime.UtcNow - start).TotalSeconds > 5) break;
                        }

                        // 4) 回収して消す
                        return WithLock(lockMutex, () =>
                        {
                            var lines = File.Exists(PendingFilePath)
                                ? File.ReadAllLines(PendingFilePath, Encoding.UTF8)
                                : new string[0];
                            try { File.Delete(PendingFilePath); } catch { }

                            return lines
                                .Select(l => l.Trim())
                                .Where(l => l.Length > 0 && File.Exists(l))   // フォルダや消えたものは除外
                                .Select(Path.GetFullPath)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                        });
                    }
                    finally
                    {
                        master.ReleaseMutex();
                    }
                }
            }
        }

        private static void WithLock(Mutex m, Action action)
        {
            WithLock(m, () => { action(); return 0; });
        }

        private static T WithLock<T>(Mutex m, Func<T> func)
        {
            try { m.WaitOne(); }
            catch (AbandonedMutexException) { }
            try { return func(); }
            finally { m.ReleaseMutex(); }
        }
    }
}
