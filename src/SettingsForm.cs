using System;
using System.Drawing;
using System.Windows.Forms;

namespace SuperRename
{
    /// <summary>引数なしで起動したときに出る、登録／解除画面。</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly Label _statusLabel;
        private readonly Button _installButton;
        private readonly Button _uninstallButton;

        public SettingsForm()
        {
            Text = Program.AppName;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Yu Gothic UI", 10f);
            ClientSize = new Size(480, 240);
            Padding = new Padding(16);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = Program.AppName,
                Font = new Font("Yu Gothic UI", 16f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
            };

            var desc = new Label
            {
                Dock = DockStyle.Fill,
                Text =
                    "ファイルを複数選択 → 右クリック → 「" + Program.AppName + "」で連番リネーム。\n\n" +
                    "登録: 右クリックメニューに追加\n" +
                    "削除: 右クリックメニューから削除",
            };

            _statusLabel = new Label { AutoSize = true, Margin = new Padding(0, 8, 0, 8) };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            _uninstallButton = new Button { Text = "削除 (アンインストール)", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            _installButton = new Button { Text = "登録 (インストール)", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            buttons.Controls.Add(_uninstallButton);
            buttons.Controls.Add(_installButton);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(desc, 0, 1);
            layout.Controls.Add(_statusLabel, 0, 2);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);

            _installButton.Click += (s, e) => Try(() =>
            {
                Installer.Install();
                MessageBox.Show(this, "右クリックメニューに登録しました。", Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            _uninstallButton.Click += (s, e) => Try(() =>
            {
                var answer = MessageBox.Show(this,
                    "削除しますか？",
                    Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
                Installer.Uninstall();
                MessageBox.Show(this, "削除しました。", Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            RefreshStatus();
        }

        private void Try(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "失敗しました。\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            bool installed = Installer.IsInstalled;
            _statusLabel.Text = installed
                ? "状態: 登録済み ✔"
                : "状態: 未登録";
            _statusLabel.ForeColor = installed ? Color.ForestGreen : SystemColors.ControlText;
            _installButton.Text = installed ? "再登録 (更新)" : "登録 (インストール)";
            _uninstallButton.Enabled = installed;
        }
    }
}
