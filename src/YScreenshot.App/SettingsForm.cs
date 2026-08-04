using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace YScreenshot.App
{
    /// <summary>
    /// A normal titled dialog -- unlike the toolbar strip, Settings is an explicit,
    /// occasional action reached from the tray menu, so it doesn't need to stay a thin
    /// borderless strip the way the main UI does.
    /// </summary>
    public sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;

        private TextBox _fullScreenHotkeyBox;
        private TextBox _regionHotkeyBox;
        private TextBox _scrollingHotkeyBox;
        private CheckBox _startWithWindowsCheckBox;
        private ComboBox _feedbackStyleCombo;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Text = "YScreenshot Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 260);

            BuildLayout();
            LoadValues();
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(12),
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Full Screen hotkey:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _fullScreenHotkeyBox = new TextBox { Width = 200 };
            AttachHotkeyRecorder(_fullScreenHotkeyBox);
            layout.Controls.Add(_fullScreenHotkeyBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Rectangle hotkey:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _regionHotkeyBox = new TextBox { Width = 200 };
            AttachHotkeyRecorder(_regionHotkeyBox);
            layout.Controls.Add(_regionHotkeyBox, 1, 1);

            layout.Controls.Add(new Label { Text = "Scrolling hotkey:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            _scrollingHotkeyBox = new TextBox { Width = 200 };
            AttachHotkeyRecorder(_scrollingHotkeyBox);
            layout.Controls.Add(_scrollingHotkeyBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Capture feedback:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            _feedbackStyleCombo = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            _feedbackStyleCombo.Items.AddRange(new object[] { "Toast", "TrayBalloon", "None" });
            layout.Controls.Add(_feedbackStyleCombo, 1, 3);

            layout.Controls.Add(new Panel(), 0, 4);
            _startWithWindowsCheckBox = new CheckBox { Text = "Start with Windows", AutoSize = true };
            layout.Controls.Add(_startWithWindowsCheckBox, 1, 4);

            Controls.Add(layout);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(12)
            };

            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var saveButton = new Button { Text = "Save" };
            saveButton.Click += OnSaveClick;

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(saveButton);
            Controls.Add(buttonPanel);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void LoadValues()
        {
            _fullScreenHotkeyBox.Text = _settings.FullScreenHotkey;
            _regionHotkeyBox.Text = _settings.RegionHotkey;
            _scrollingHotkeyBox.Text = _settings.ScrollingHotkey;
            _startWithWindowsCheckBox.Checked = _settings.StartWithWindows;

            int index = _feedbackStyleCombo.Items.IndexOf(_settings.FeedbackStyle);
            _feedbackStyleCombo.SelectedIndex = index >= 0 ? index : 0;
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            if (!ValidateHotkey("Full Screen", _fullScreenHotkeyBox.Text)) return;
            if (!ValidateHotkey("Rectangle", _regionHotkeyBox.Text)) return;
            if (!ValidateHotkey("Scrolling", _scrollingHotkeyBox.Text)) return;

            _settings.FullScreenHotkey = _fullScreenHotkeyBox.Text;
            _settings.RegionHotkey = _regionHotkeyBox.Text;
            _settings.ScrollingHotkey = _scrollingHotkeyBox.Text;
            _settings.StartWithWindows = _startWithWindowsCheckBox.Checked;
            _settings.FeedbackStyle = (string)_feedbackStyleCombo.SelectedItem;
            _settings.Save();

            StartupManager.SetEnabled(_settings.StartWithWindows);

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ValidateHotkey(string label, string spec)
        {
            if (HotkeyManager.TryParse(spec, out _, out _))
            {
                return true;
            }

            MessageBox.Show(this, $"'{spec}' isn't a valid hotkey for {label}.", "Invalid hotkey",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private static void AttachHotkeyRecorder(TextBox box)
        {
            box.ReadOnly = true;
            box.KeyDown += (s, e) =>
            {
                e.SuppressKeyPress = true;

                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu)
                {
                    return;
                }

                if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
                {
                    box.Text = string.Empty;
                    return;
                }

                var parts = new List<string>();
                if (e.Control) parts.Add("Ctrl");
                if (e.Alt) parts.Add("Alt");
                if (e.Shift) parts.Add("Shift");
                parts.Add(e.KeyCode.ToString());

                box.Text = string.Join("+", parts);
            };
        }
    }
}
