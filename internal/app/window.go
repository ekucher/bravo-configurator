package app

import (
	"fmt"
	"os"
	"strings"

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"

	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/profile"
	"github.com/ekucher/bravo-bis-configurator/internal/schema"
)

// RunGUI is the GUI entry point invoked by cmd/configurator/main.go when
// no --validate flag is given: pick a profile -> auto-locate its file (or
// fall back to a manual "open file" dialog) -> edit in a generated form
// -> save. Each step is a modal dialog; RunGUI returns once the editor
// window is closed.
func RunGUI() error {
	prof, ok, err := chooseProfile()
	if err != nil {
		return fmt.Errorf("app: profile selection: %w", err)
	}
	if !ok {
		return nil // operator cancelled
	}

	filePath, ok, err := resolveFilePath(prof)
	if err != nil {
		return fmt.Errorf("app: locating %s: %w", prof.FileHint, err)
	}
	if !ok {
		return nil
	}

	return openEditor(prof, filePath)
}

// resolveFilePath auto-discovers prof's file at its well-known location
// (see defaultPathForProfile) and uses it directly when it exists there —
// no dialog at all. If it cannot be located (missing %SystemRoot%, an
// unusual deployment layout, first-time setup before bravo.ini exists
// yet, ...), it explains why and falls back to the manual "open file"
// dialog: the well-known location is fixed and correctness-critical for
// bravo.ini, but an operator must still be able to point the tool at a
// file directly when the automatic guess doesn't hold. ok is false if the
// operator cancelled the fallback dialog.
func resolveFilePath(prof profile.Profile) (path string, ok bool, err error) {
	defaultPath, locateErr := defaultPathForProfile(prof)
	if locateErr == nil {
		if _, statErr := os.Stat(defaultPath); statErr == nil {
			return defaultPath, true, nil
		}
	}

	reason := fmt.Sprintf("файл не знайдено за очікуваним шляхом: %s", defaultPath)
	if locateErr != nil {
		reason = locateErr.Error()
	}
	walk.MsgBox(nil, "Автоматичний пошук файлу",
		fmt.Sprintf("%s (%s): %s\n\nВкажіть файл вручну.", prof.FileHint, prof.DisplayName, reason),
		walk.MsgBoxIconInformation)

	return chooseFile(prof)
}

// chooseProfile shows a small modal dialog offering the two known
// profiles. ok is false if the operator cancelled.
func chooseProfile() (profile.Profile, bool, error) {
	var dlg *walk.Dialog
	var selected profile.Profile
	var chosen bool

	_, err := Dialog{
		AssignTo: &dlg,
		Title:    "Конфігуратор BRAVO/BIS",
		MinSize:  Size{Width: 360, Height: 160},
		Layout:   VBox{},
		Children: []Widget{
			Label{Text: "Яку конфігурацію ви хочете редагувати?"},
			PushButton{
				Text: "BRAVO (сервер) — bravo.ini",
				OnClicked: func() {
					selected, _ = profile.Find("bravo")
					chosen = true
					dlg.Accept()
				},
			},
			PushButton{
				Text: "BIS (клієнт) — bis.ini",
				OnClicked: func() {
					selected, _ = profile.Find("bis")
					chosen = true
					dlg.Accept()
				},
			},
			VSpacer{},
			PushButton{
				Text:      "Скасувати",
				OnClicked: func() { dlg.Cancel() },
			},
		},
	}.Run(nil)
	if err != nil {
		return profile.Profile{}, false, err
	}
	return selected, chosen, nil
}

// chooseFile shows a native "open file" dialog defaulted to prof's
// filename, used as a fallback when resolveFilePath's auto-discovery
// can't locate the file itself. The text encoding is always auto-detected
// (see internal/ini's DetectAndDecode) — there is no GUI override; use
// --encoding on the CLI --validate path if a legacy codepage ever needs
// to be forced. ok is false if the operator cancelled.
func chooseFile(prof profile.Profile) (path string, ok bool, err error) {
	dlg := walk.FileDialog{
		Title:  fmt.Sprintf("Відкрити %s (%s)", prof.FileHint, prof.DisplayName),
		Filter: "INI-файли (*.ini)|*.ini|Усі файли (*.*)|*.*",
	}
	accepted, err := dlg.ShowOpen(nil)
	if err != nil {
		return "", false, err
	}
	if !accepted {
		return "", false, nil
	}
	return dlg.FilePath, true, nil
}

func openEditor(prof profile.Profile, filePath string) error {
	doc, enc, err := ini.ReadFile(filePath, ini.DefaultParseOptions(), "")
	if err != nil {
		walk.MsgBox(nil, "Помилка читання", err.Error(), walk.MsgBoxIconError)
		return err
	}
	s, err := prof.LoadSchema()
	if err != nil {
		walk.MsgBox(nil, "Помилка схеми", err.Error(), walk.MsgBoxIconError)
		return err
	}
	model := NewFormModel(prof, s, doc, enc, filePath)
	return runEditorWindow(model)
}

// fieldKey identifies a field's status label in the lookup map built while
// constructing the form; must match the (section, key) pairing used by
// FormModel.Sections/FieldView.
func fieldKey(section, key string) string {
	return strings.ToLower(section) + "\x00" + strings.ToLower(key)
}

// runEditorWindow builds and runs the main editor window for model. It
// blocks until the window is closed.
func runEditorWindow(model *FormModel) error {
	var mw *walk.MainWindow
	var saveButton *walk.PushButton
	var summaryLabel *walk.Label
	// statusLabels holds **walk.Label (the address of each field's local
	// AssignTo variable), not *walk.Label directly: at the time
	// buildFieldRow populates this map, the declarative widget tree has
	// only been described, not yet built, so the label variable is still
	// nil. walk's declarative builder fills the real *walk.Label into that
	// variable during Create(). Storing the address lets refresh() always
	// read the variable's *current* value instead of the nil snapshot from
	// construction time.
	statusLabels := map[string]**walk.Label{}

	refresh := func() {
		for _, sec := range model.Sections {
			for _, f := range sec.Fields {
				lblPtr, ok := statusLabels[fieldKey(sec.Name, f.Key)]
				if !ok || lblPtr == nil || *lblPtr == nil {
					// Not registered yet, or this field's status Label
					// hasn't been constructed by walk yet — genuinely
					// expected during initial widget construction, since
					// setting a field's starting value can itself fire a
					// change event (see buildEditorWidget) before every
					// widget in the form exists.
					continue
				}
				setFieldStatusLabel(*lblPtr, f)
			}
		}
		if saveButton != nil {
			saveButton.SetEnabled(model.CanSave())
		}
		if summaryLabel != nil {
			summaryLabel.SetText(summaryText(model))
		}
	}

	pages := make([]TabPage, 0, len(model.Sections))
	for i := range model.Sections {
		sec := &model.Sections[i]
		pages = append(pages, buildSectionTab(model, sec, statusLabels, refresh))
	}

	statusLabel := "чернетка"
	if model.Schema.Status == schema.StatusVerified {
		statusLabel = "перевірена"
	}
	banner := fmt.Sprintf("%s — %s  [схема: %s]", model.Profile.DisplayName, model.FilePath, statusLabel)

	_, err := MainWindow{
		AssignTo: &mw,
		Title:    "Конфігуратор BRAVO/BIS",
		MinSize:  Size{Width: 720, Height: 520},
		Layout:   VBox{},
		Children: []Widget{
			Label{Text: banner},
			TabWidget{Pages: pages},
			Composite{
				Layout: HBox{},
				Children: []Widget{
					Label{AssignTo: &summaryLabel, Text: summaryText(model)},
					HSpacer{},
					PushButton{
						AssignTo: &saveButton,
						Text:     "Зберегти",
						Enabled:  model.CanSave(),
						OnClicked: func() {
							result, err := model.Save()
							if err != nil {
								walk.MsgBox(mw, "Помилка збереження", err.Error(), walk.MsgBoxIconError)
								return
							}
							msg := "Збережено."
							if result.BackupPath != "" {
								msg += " Резервна копія: " + result.BackupPath
							}
							icon := walk.MsgBoxIconInformation
							if result.RootCopyPath != "" {
								msg += "\nКопію в корені оновлено: " + result.RootCopyPath
							}
							if result.RootCopyErr != nil {
								msg += "\n\nУВАГА: не вдалося оновити копію в корені: " + result.RootCopyErr.Error()
								icon = walk.MsgBoxIconWarning
							}
							walk.MsgBox(mw, "Збереження", msg, icon)
						},
					},
					PushButton{Text: "Закрити", OnClicked: func() { mw.Close() }},
				},
			},
		},
	}.Run()
	return err
}

func summaryText(model *FormModel) string {
	var errors, warnings int
	for _, r := range model.AllFindings {
		switch r.Severity {
		case schema.SeverityError:
			errors++
		case schema.SeverityWarning:
			warnings++
		}
	}
	return fmt.Sprintf("Помилок: %d   Попереджень: %d", errors, warnings)
}

func setFieldStatusLabel(lbl *walk.Label, f FieldView) {
	if len(f.Findings) == 0 {
		lbl.SetText("")
		return
	}
	r := f.Findings[0]
	prefix := "ПОПЕРЕДЖЕННЯ"
	if r.Severity == schema.SeverityError {
		prefix = "ПОМИЛКА"
	}
	lbl.SetText(fmt.Sprintf("[%s] %s", prefix, r.Message))
}

// buildSectionTab renders one schema section as a TabPage: a scrollable
// list of label/editor/status rows, one per field.
func buildSectionTab(model *FormModel, sec *SectionView, statusLabels map[string]**walk.Label, onChanged func()) TabPage {
	var rows []Widget
	for i := range sec.Fields {
		f := &sec.Fields[i]
		rows = append(rows, buildFieldRow(model, f, statusLabels, onChanged))
	}
	title := sec.Label
	if title == "" {
		title = sec.Name
	}
	return TabPage{
		Title:  title,
		Layout: VBox{},
		Children: []Widget{
			ScrollView{
				Layout:   VBox{},
				Children: rows,
			},
		},
	}
}

// buildFieldRow renders one field as a Composite: a label (marked with a
// leading "*" when required), a type-appropriate editor widget, and a
// status label that setFieldStatusLabel keeps in sync with validation
// findings.
func buildFieldRow(model *FormModel, f *FieldView, statusLabels map[string]**walk.Label, onChanged func()) Widget {
	var statusLbl *walk.Label
	labelText := f.Label
	if labelText == "" {
		labelText = f.Key
	}
	if f.Required {
		labelText = "* " + labelText
	}

	section, key := f.Section, f.Key
	applyEdit := func(value string) {
		model.ApplyEdit(section, key, value)
		onChanged()
	}

	editor := buildEditorWidget(f, applyEdit)

	row := Composite{
		Layout: Grid{Columns: 2},
		Children: []Widget{
			Label{Text: labelText, ToolTipText: f.Description},
			editor,
			HSpacer{},
			Label{AssignTo: &statusLbl, Text: ""},
		},
	}
	statusLabels[fieldKey(section, key)] = &statusLbl
	return row
}

// buildEditorWidget picks a widget by FieldType and wires its change event
// to applyEdit(newValue).
func buildEditorWidget(f *FieldView, applyEdit func(string)) Widget {
	switch f.Type {
	case schema.TypeBool:
		var cb *walk.CheckBox
		checked := f.Value == "1" || strings.EqualFold(f.Value, "true")
		return CheckBox{
			AssignTo: &cb,
			Checked:  checked,
			OnCheckedChanged: func() {
				if cb.Checked() {
					applyEdit("1")
				} else {
					applyEdit("0")
				}
			},
		}
	case schema.TypeEnum:
		if f.Rule != nil && f.Rule.Kind == schema.RuleEnum && len(f.Rule.Values) > 0 {
			var cb *walk.ComboBox
			idx := -1
			for i, v := range f.Rule.Values {
				if v == f.Value {
					idx = i
				}
			}
			return ComboBox{
				AssignTo:     &cb,
				Model:        f.Rule.Values,
				CurrentIndex: idx,
				OnCurrentIndexChanged: func() {
					i := cb.CurrentIndex()
					if i >= 0 && i < len(f.Rule.Values) {
						applyEdit(f.Rule.Values[i])
					}
				},
			}
		}
		return textEditor(f, applyEdit)
	case schema.TypePath:
		return pathEditor(f, applyEdit)
	default:
		return textEditor(f, applyEdit)
	}
}

func textEditor(f *FieldView, applyEdit func(string)) Widget {
	var le *walk.LineEdit
	return LineEdit{
		AssignTo: &le,
		Text:     f.Value,
		OnTextChanged: func() {
			applyEdit(le.Text())
		},
	}
}

func pathEditor(f *FieldView, applyEdit func(string)) Widget {
	var le *walk.LineEdit
	isDir := f.Rule != nil && f.Rule.Kind == schema.RulePathExists && f.Rule.PathMode == schema.PathModeDir

	return Composite{
		Layout: HBox{MarginsZero: true},
		Children: []Widget{
			LineEdit{
				AssignTo: &le,
				Text:     f.Value,
				OnTextChanged: func() {
					applyEdit(le.Text())
				},
			},
			PushButton{
				Text:    "...",
				MaxSize: Size{Width: 30},
				OnClicked: func() {
					newPath, ok := browseForPath(isDir)
					if !ok {
						return
					}
					le.SetText(newPath)
					applyEdit(newPath)
				},
			},
		},
	}
}

func browseForPath(isDir bool) (string, bool) {
	dlg := new(walk.FileDialog)
	var accepted bool
	var err error
	if isDir {
		accepted, err = dlg.ShowBrowseFolder(nil)
	} else {
		accepted, err = dlg.ShowOpen(nil)
	}
	if err != nil || !accepted {
		return "", false
	}
	return dlg.FilePath, true
}
