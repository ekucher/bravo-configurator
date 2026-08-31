namespace BravoBisConfigurator.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application: dispatches to the
    ///  headless "--validate ..." CLI mode (see CliRunner) or, with no
    ///  flags, the GUI (see GuiRunner) — ported 1:1 from
    ///  cmd/configurator/main.go's main()/run().
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        return CliRunner.Run(args, Console.Out, Console.Error);
    }
}
