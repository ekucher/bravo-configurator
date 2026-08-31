namespace BravoBisConfigurator.App;

/// <summary>
///  Static shell (see docs/ARCHITECTURE.md) offering the two known profiles.
///  Mirrors the Go version's chooseProfile(): SelectedProfileName is set and
///  DialogResult.OK returned only when the operator picks BRAVO or BIS;
///  Cancel/close otherwise leaves it null.
/// </summary>
public partial class ProfileSelectForm : Form
{
    public string? SelectedProfileName { get; private set; }

    public ProfileSelectForm()
    {
        InitializeComponent();
    }

    private void BravoButton_Click(object sender, EventArgs e)
    {
        SelectedProfileName = "bravo";
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BisButton_Click(object sender, EventArgs e)
    {
        SelectedProfileName = "bis";
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
