using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Model;
using BravoBisConfigurator.Core.Profile;

namespace BravoBisConfigurator.App;

/// <summary>
///  GUI entry-point orchestration: pick a profile -&gt; auto-locate its file
///  (or fall back to a manual "open file" dialog) -&gt; edit in a generated
///  form -&gt; save. Ported 1:1 from internal/app/window.go's
///  RunGUI/resolveFilePath/chooseFile/openEditor.
/// </summary>
internal static class GuiRunner
{
    public static void Run()
    {
        using var profileForm = new ProfileSelectForm();
        if (profileForm.ShowDialog() != DialogResult.OK || profileForm.SelectedProfileName is null)
        {
            return; // operator cancelled
        }
        if (!ProfileDefinition.TryFind(profileForm.SelectedProfileName, out var prof))
        {
            return;
        }

        if (!TryResolveFilePath(prof, out var filePath))
        {
            return; // operator cancelled the fallback dialog
        }

        OpenEditor(prof, filePath);
    }

    /// <summary>
    ///  Auto-discovers prof's file at its well-known location and uses it
    ///  directly when it exists there — no dialog at all. If it cannot be
    ///  located, explains why and falls back to the manual "open file"
    ///  dialog.
    /// </summary>
    private static bool TryResolveFilePath(ProfileDefinition prof, out string path)
    {
        string? defaultPath = null;
        Exception? locateErr = null;
        try
        {
            defaultPath = Discover.DefaultPathForProfile(prof);
            if (File.Exists(defaultPath))
            {
                path = defaultPath;
                return true;
            }
        }
        catch (Exception ex)
        {
            locateErr = ex;
        }

        var reason = locateErr?.Message ?? $"файл не знайдено за очікуваним шляхом: {defaultPath}";
        MessageBox.Show(
            $"{prof.FileHint} ({prof.DisplayName}): {reason}\n\nВкажіть файл вручну.",
            "Автоматичний пошук файлу",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        return TryChooseFile(prof, out path);
    }

    /// <summary>
    ///  Shows a native "open file" dialog defaulted to prof's filename,
    ///  used as a fallback when auto-discovery can't locate the file
    ///  itself. The text encoding is always auto-detected (see
    ///  IniEncodingCodec.DetectAndDecode) — there is no GUI override.
    /// </summary>
    private static bool TryChooseFile(ProfileDefinition prof, out string path)
    {
        using var dlg = new OpenFileDialog
        {
            Title = $"Відкрити {prof.FileHint} ({prof.DisplayName})",
            Filter = "INI-файли (*.ini)|*.ini|Усі файли (*.*)|*.*",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            path = dlg.FileName;
            return true;
        }
        path = "";
        return false;
    }

    private static void OpenEditor(ProfileDefinition prof, string filePath)
    {
        Document doc;
        IniEncoding enc;
        try
        {
            (doc, enc) = IniFile.ReadFile(filePath, ParseOptions.Default());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Помилка читання", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Core.Schema.Schema schema;
        try
        {
            schema = prof.LoadSchema();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Помилка схеми", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var model = new FormModel(prof, schema, doc, enc, filePath);
        Application.Run(new EditorForm(model));
    }
}
