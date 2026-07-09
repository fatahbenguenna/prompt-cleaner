namespace PromptCleaner.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Filet de sécurité global (S6.1) : aucune exception ne doit tuer
        // l'application sans message — on affiche l'erreur et on continue.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowUnexpectedError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowUnexpectedError(ex);
            }
        };

        Application.Run(new MainForm());
    }

    private static void ShowUnexpectedError(Exception ex)
    {
        MessageBox.Show(
            $"Une erreur inattendue s'est produite :\n\n{ex.Message}",
            "prompt-cleaner",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
