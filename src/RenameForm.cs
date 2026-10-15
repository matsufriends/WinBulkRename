using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SuperRename
{
    internal sealed class RenameForm : Form
    {
        private readonly List<string> _files;
        private readonly TextBox _nameBox;
        private readonly ListView _preview;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly Label _countLabel;

        public RenameForm(List<string> files)
        {
            _files = Renamer.SortNatural(files);

            Text = Program.AppName;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;             // エクスプローラーの裏に隠れないように
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Yu Gothic UI", 10f);
            ClientSize = new Size(640, 440);
            MinimumSize = new Size(480, 320);
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- 入力行
            var inputPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var nameLabel = new Label { Text = "ファイル名:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };
            _nameBox = new TextBox { Dock = DockStyle.Fill, Text = Renamer.SuggestBaseName(_files[0]) };
            inputPanel.Controls.Add(nameLabel, 0, 0);
            inputPanel.Controls.Add(_nameBox, 1, 0);

            _countLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 6, 0, 6) };

            // --- プレビュー
            _preview = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                MultiSelect = false,
            };
            _preview.Columns.Add("変更前", 280);
            _preview.Columns.Add("変更後", 300);

            // --- ボタン
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            _cancelButton = new Button { Text = "キャンセル", AutoSize = true, Padding = new Padding(8, 2, 8, 2), DialogResult = DialogResult.Cancel };
            _okButton = new Button { Text = "リネーム実行", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_okButton);

            layout.Controls.Add(inputPanel, 0, 0);
            layout.Controls.Add(_countLabel, 0, 1);
            layout.Controls.Add(_preview, 0, 2);
            layout.Controls.Add(buttonPanel, 0, 3);
            Controls.Add(layout);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            _nameBox.TextChanged += (s, e) => UpdatePreview();
            _okButton.Click += (s, e) => Run();
            Shown += (s, e) =>
            {
                Activate();
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
            Resize += (s, e) => FitColumns();

            UpdatePreview();
        }

        private void FitColumns()
        {
            if (_preview.ClientSize.Width <= 0) return;
            int w = _preview.ClientSize.Width - 4;
            _preview.Columns[0].Width = w / 2;
            _preview.Columns[1].Width = w - w / 2;
        }

        private List<RenamePlan> CurrentPlan()
        {
            var baseName = Renamer.Sanitize(_nameBox.Text);
            if (baseName.Length == 0) return null;
            return Renamer.BuildPlan(_files, baseName);
        }

        private void UpdatePreview()
        {
            var plan = CurrentPlan();
            _preview.BeginUpdate();
            _preview.Items.Clear();
            if (plan != null)
            {
                foreach (var p in plan)
                {
                    var item = new ListViewItem(p.SourceName);
                    item.SubItems.Add(p.TargetName);
                    _preview.Items.Add(item);
                }
            }
            _preview.EndUpdate();
            FitColumns();

            _countLabel.Text = _files.Count + " 個のファイル";
            _okButton.Enabled = plan != null;
        }

        private void Run()
        {
            var plan = CurrentPlan();
            if (plan == null) return;

            var error = Renamer.Validate(plan);
            if (error != null)
            {
                MessageBox.Show(this, error, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Renamer.Execute(plan);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "リネームに失敗しました。\n\n" + ex.Message, Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
