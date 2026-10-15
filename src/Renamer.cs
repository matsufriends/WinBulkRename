using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SuperRename
{
    internal sealed class RenamePlan
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string SourceName => Path.GetFileName(Source);
        public string TargetName => Path.GetFileName(Target);
    }

    internal static class Renamer
    {
        public const int MinPadding = 2;

        /// <summary>エクスプローラーと同じ「自然順」で並べる (file2 &lt; file10)。</summary>
        public static List<string> SortNatural(IEnumerable<string> files)
        {
            var list = files.ToList();
            list.Sort((a, b) => NativeMethods.StrCmpLogicalW(Path.GetFileName(a), Path.GetFileName(b)));
            return list;
        }

        /// <summary>最初のファイル名から既定の入力値を推測する ("foo_01" → "foo")。</summary>
        public static string SuggestBaseName(string firstFile)
        {
            var name = Path.GetFileNameWithoutExtension(firstFile) ?? "";
            name = Regex.Replace(name, @"[_\-\s]*\d+$", "");
            return name.Length > 0 ? name : "file";
        }

        public static string Sanitize(string baseName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = baseName.Trim().Where(c => !invalid.Contains(c)).ToArray();
            return new string(chars);
        }

        /// <summary>リネーム計画を作る。拡張子は元ファイルのものを維持する。</summary>
        public static List<RenamePlan> BuildPlan(IList<string> sortedFiles, string baseName)
        {
            int padding = Math.Max(MinPadding, sortedFiles.Count.ToString().Length);
            var plans = new List<RenamePlan>();
            for (int i = 0; i < sortedFiles.Count; i++)
            {
                var src = sortedFiles[i];
                var ext = Path.GetExtension(src) ?? "";
                var dir = Path.GetDirectoryName(src) ?? "";
                var newName = baseName + "_" + (i + 1).ToString().PadLeft(padding, '0') + ext;
                plans.Add(new RenamePlan { Source = src, Target = Path.Combine(dir, newName) });
            }
            return plans;
        }

        /// <summary>実行前チェック。問題があればメッセージを返す。</summary>
        public static string Validate(IList<RenamePlan> plans)
        {
            var sources = new HashSet<string>(plans.Select(p => p.Source), StringComparer.OrdinalIgnoreCase);
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in plans)
            {
                if (!File.Exists(p.Source))
                    return "ファイルが見つかりません:\n" + p.Source;

                if (!targets.Add(p.Target))
                    return "変更後の名前が重複します:\n" + p.TargetName;

                // 選択外の既存ファイルを潰してしまう場合は中止
                if (File.Exists(p.Target) && !sources.Contains(p.Target))
                    return "同じ名前のファイルが既に存在します:\n" + p.TargetName;
            }
            return null;
        }

        /// <summary>
        /// 2 段階リネーム。a_01→a_02 のような入れ替えでも衝突しないよう、
        /// まず一時名に退避してから最終名にする。
        /// </summary>
        public static void Execute(IList<RenamePlan> plans)
        {
            var temp = new List<KeyValuePair<string, string>>(); // (tempPath, finalPath)
            var done = new List<KeyValuePair<string, string>>(); // ロールバック用 (current, original)

            try
            {
                foreach (var p in plans)
                {
                    if (string.Equals(p.Source, p.Target, StringComparison.Ordinal))
                        continue; // 完全に同じ名前なら何もしない

                    var dir = Path.GetDirectoryName(p.Source) ?? "";
                    var tmp = Path.Combine(dir, "~" + Guid.NewGuid().ToString("N") + ".nctmp");
                    File.Move(p.Source, tmp);
                    done.Add(new KeyValuePair<string, string>(tmp, p.Source));
                    temp.Add(new KeyValuePair<string, string>(tmp, p.Target));
                }

                for (int i = 0; i < temp.Count; i++)
                {
                    File.Move(temp[i].Key, temp[i].Value);
                    // ロールバック情報を更新
                    var idx = done.FindIndex(d => d.Key == temp[i].Key);
                    done[idx] = new KeyValuePair<string, string>(temp[i].Value, done[idx].Value);
                }
            }
            catch
            {
                // 失敗したら可能な限り元に戻す
                foreach (var d in done)
                {
                    try { if (File.Exists(d.Key) && !File.Exists(d.Value)) File.Move(d.Key, d.Value); } catch { }
                }
                throw;
            }
        }
    }
}
