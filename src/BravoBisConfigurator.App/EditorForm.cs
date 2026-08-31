using BravoBisConfigurator.Core.Model;
using BravoBisConfigurator.Core.Schema;
using BravoBisConfigurator.Core.Validate;

namespace BravoBisConfigurator.App;

/// <summary>
///  Static shell (banner/tab container/Save-Close buttons — see
///  EditorForm.Designer.cs) plus programmatic per-field content generated
///  from a FormModel. Ported 1:1 from internal/app/window.go's
///  runEditorWindow/buildSectionTab/buildFieldRow/buildEditorWidget: the
///  field set differs per profile/schema and cannot be drawn ahead of time
///  in the designer, but every static chrome element (this form, the
///  profile-select dialog, message boxes) is real designer-editable
///  WinForms — the whole reason for this rewrite.
/// </summary>
public partial class EditorForm : Form
{
    private readonly FormModel _model;

    /// <summary>
    ///  Each field's status Label, keyed the same way as Go's
    ///  fieldKey(section, key) — unlike walk's declarative builder (which
    ///  needed **walk.Label indirection because widgets don't exist until
    ///  Create() runs), WinForms controls are constructed imperatively, so
    ///  a plain Dictionary of already-built Labels is enough.
    /// </summary>
    private readonly Dictionary<string, Label> _statusLabels = new();

    public EditorForm(FormModel model)
    {
        InitializeComponent();
        _model = model;

        var statusLabel = model.Schema.Status == SchemaStatus.Verified ? "перевірена" : "чернетка";
        bannerLabel.Text = $"{model.Profile.DisplayName} — {model.FilePath}  [схема: {statusLabel}]";

        foreach (var sec in model.Sections)
        {
            tabControl.TabPages.Add(BuildSectionTab(sec));
        }

        // saveButton.Click / closeButton.Click are already wired to
        // SaveButton_Click / CloseButton_Click in EditorForm.Designer.cs.
        RefreshUi();
    }

    private static string FieldKey(string section, string key) => $"{section.ToLowerInvariant()}\0{key.ToLowerInvariant()}";

    /// <summary>
    ///  Re-syncs every field's status label, the Save button's enabled
    ///  state, and the error/warning summary from the model's current
    ///  (just-recomputed) findings — called once after construction and
    ///  again after every field edit. Mirrors Go's refresh() closure.
    /// </summary>
    private void RefreshUi()
    {
        foreach (var sec in _model.Sections)
        {
            foreach (var f in sec.Fields)
            {
                if (_statusLabels.TryGetValue(FieldKey(sec.Name, f.Key), out var lbl))
                {
                    SetFieldStatusLabel(lbl, f);
                }
            }
        }
        saveButton.Enabled = _model.CanSave();
        summaryLabel.Text = SummaryText(_model);
    }

    private static void SetFieldStatusLabel(Label lbl, FieldView f)
    {
        if (f.Findings.Count == 0)
        {
            lbl.Text = "";
            return;
        }
        var r = f.Findings[0];
        var prefix = r.Severity == Severity.Error ? "ПОМИЛКА" : "ПОПЕРЕДЖЕННЯ";
        lbl.Text = $"[{prefix}] {r.Message}";
    }

    private static string SummaryText(FormModel model)
    {
        var errors = model.AllFindings.Count(r => r.Severity == Severity.Error);
        var warnings = model.AllFindings.Count(r => r.Severity == Severity.Warning);
        return $"Помилок: {errors}   Попереджень: {warnings}";
    }

    /// <summary>
    ///  Renders one schema section as a TabPage: a scrollable list of
    ///  label/editor/status rows, one per field.
    /// </summary>
    private TabPage BuildSectionTab(SectionView sec)
    {
        var page = new TabPage(string.IsNullOrEmpty(sec.Label) ? sec.Name : sec.Label);

        // AutoScroll host, matching Go's ScrollView — the field list can
        // exceed the tab's visible height.
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        page.Controls.Add(scroll);

        var rows = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(6, 4, 6, 4),
        };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        scroll.Controls.Add(rows);

        foreach (var f in sec.Fields)
        {
            rows.RowCount++;
            rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rows.Controls.Add(BuildFieldRow(f), 0, rows.RowCount - 1);
        }

        return page;
    }

    /// <summary>
    ///  Renders one field as a row: a bold label (marked with a leading "*"
    ///  when required, carrying the description + resolved-path hint as a
    ///  tooltip), a type-appropriate editor widget, and a status label kept
    ///  in sync by RefreshUi.
    /// </summary>
    private Control BuildFieldRow(FieldView f)
    {
        var labelText = string.IsNullOrEmpty(f.Label) ? f.Key : f.Label;
        if (f.Required)
        {
            labelText = "* " + labelText;
        }

        var tooltip = f.Description ?? "";
        if (f.ResolvedHint != "")
        {
            var hint = "Відносний шлях; резолвиться як: " + f.ResolvedHint;
            tooltip = tooltip != "" ? tooltip + "\n\n" + hint : hint;
        }

        var section = f.Section;
        var key = f.Key;
        void ApplyEdit(string value)
        {
            _model.ApplyEdit(section, key, value);
            RefreshUi();
        }

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 5, 6, 0),
        };
        if (tooltip != "")
        {
            new ToolTip().SetToolTip(label, tooltip);
        }

        var editor = BuildEditorWidget(f, ApplyEdit);
        editor.Dock = DockStyle.Fill;

        var statusLbl = new Label { Text = "", AutoSize = true, ForeColor = Color.Firebrick, Margin = new Padding(0, 0, 0, 2) };
        _statusLabels[FieldKey(section, key)] = statusLbl;

        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            // Dock.Top (not a fixed Width) stretches the row to match its
            // parent's full width while height stays auto-sized — a fixed
            // Width previously clipped the editor column (it doesn't
            // "shrink to fit", it clips) whenever the real window was
            // wider or narrower than the hardcoded guess.
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 2),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(editor, 1, 0);
        row.Controls.Add(statusLbl, 1, 1);
        return row;
    }

    /// <summary>Picks a widget by FieldType and wires its change event to applyEdit(newValue).</summary>
    private static Control BuildEditorWidget(FieldView f, Action<string> applyEdit)
    {
        switch (f.Type)
        {
            case FieldType.Bool:
            {
                var cb = new CheckBox { Checked = f.Value == "1" || string.Equals(f.Value, "true", StringComparison.OrdinalIgnoreCase) };
                cb.CheckedChanged += (_, _) => applyEdit(cb.Checked ? "1" : "0");
                return cb;
            }
            case FieldType.Enum when f.Rule is { Kind: RuleKind.Enum, Values.Count: > 0 }:
            {
                // Strictly a dropdown (DropDownList, not editable) — enum
                // fields must only offer the schema's declared values.
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                combo.Items.AddRange(f.Rule.Values.Cast<object>().ToArray());
                var idx = f.Rule.Values.IndexOf(f.Value);
                combo.SelectedIndex = idx >= 0 ? idx : -1;
                combo.SelectedIndexChanged += (_, _) =>
                {
                    if (combo.SelectedIndex >= 0)
                    {
                        applyEdit(f.Rule.Values[combo.SelectedIndex]);
                    }
                };
                return combo;
            }
            case FieldType.Path:
                return PathEditor(f, applyEdit);
            default:
                return TextEditor(f, applyEdit);
        }
    }

    private static Control TextEditor(FieldView f, Action<string> applyEdit)
    {
        var tb = new TextBox { Text = f.Value };
        tb.TextChanged += (_, _) => applyEdit(tb.Text);
        return tb;
    }

    private static Control PathEditor(FieldView f, Action<string> applyEdit)
    {
        var isDir = f.Rule is { Kind: RuleKind.PathExists, PathMode: PathMode.Dir };

        var tb = new TextBox { Text = f.Value, Dock = DockStyle.Fill };
        tb.TextChanged += (_, _) => applyEdit(tb.Text);

        var browse = new Button { Text = "...", Width = 30, Dock = DockStyle.Right };
        browse.Click += (_, _) =>
        {
            if (TryBrowseForPath(isDir, out var newPath))
            {
                tb.Text = newPath;
                // TextChanged above already calls applyEdit.
            }
        };

        var container = new Panel { Height = tb.PreferredHeight, Dock = DockStyle.Fill };
        // Right-docked control added before the Fill-docked one, so Fill's
        // computed area correctly excludes the button's reserved width.
        container.Controls.Add(browse);
        container.Controls.Add(tb);
        return container;
    }

    private static bool TryBrowseForPath(bool isDir, out string path)
    {
        if (isDir)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                path = dlg.SelectedPath;
                return true;
            }
        }
        else
        {
            using var dlg = new OpenFileDialog { Filter = "Усі файли (*.*)|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                path = dlg.FileName;
                return true;
            }
        }
        path = "";
        return false;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        SaveResult result;
        try
        {
            result = _model.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка збереження", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var msg = "Збережено.";
        if (result.BackupPath != "")
        {
            msg += " Резервна копія: " + result.BackupPath;
        }
        var icon = MessageBoxIcon.Information;
        if (result.RootCopyPath != "")
        {
            msg += "\nКопію в корені оновлено: " + result.RootCopyPath;
        }
        if (result.RootCopyError is not null)
        {
            msg += "\n\nУВАГА: не вдалося оновити копію в корені: " + result.RootCopyError.Message;
            icon = MessageBoxIcon.Warning;
        }
        MessageBox.Show(this, msg, "Збереження", MessageBoxButtons.OK, icon);
    }

    private void CloseButton_Click(object sender, EventArgs e)
    {
        Close();
    }
}
