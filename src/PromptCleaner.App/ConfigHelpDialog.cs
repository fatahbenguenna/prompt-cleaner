namespace PromptCleaner.App;

/// <summary>Popin d'aide ouverte par le bouton « ? » : format attendu du
/// fichier de configuration, avec un exemple copiable dans le presse-papier.</summary>
internal sealed class ConfigHelpDialog : Form
{
    private const string ExampleContent =
        """
        # Les lignes vides et celles commençant par # sont ignorées.
        # Une règle par ligne, au format : motclé : remplacement

        google : mon-entreprise
        fb44ja8k : nom-user
        myApp : nom-application

        # La casse des mots-clés est ignorée (Google = google = GOOGLE).
        # Seul le premier « : » sépare : la valeur peut en contenir.
        portail : https://intranet.exemple:8080/accueil
        """;

    public ConfigHelpDialog()
    {
        Text = "Format du fichier de configuration";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 460);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // introduction
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // exemple
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // remarques
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // boutons

        root.Controls.Add(new Label
        {
            Text = "Fichier texte (UTF-8), une règle par ligne au format « motclé : remplacement ».\n" +
                   "Au nettoyage, chaque occurrence du motclé est remplacée par la valeur de droite.",
            AutoSize = true,
            MaximumSize = new Size(590, 0),
            Margin = new Padding(0, 0, 0, 8),
        }, 0, 0);

        var example = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Text = ExampleContent.Replace("\n", Environment.NewLine),
            Font = new Font("Consolas", 10f),
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Window,
            TabStop = false,
            AccessibleName = "Exemple de fichier de configuration",
        };
        root.Controls.Add(example, 0, 1);

        root.Controls.Add(new Label
        {
            Text = "• En cas de mot-clé en double, la dernière ligne gagne.\n" +
                   "• Les mots-clés les plus longs sont appliqués en premier.\n" +
                   "• Astuce : un fichier nommé « prompt-cleaner.cfg » posé à côté de\n" +
                   "  prompt-cleaner.exe est chargé automatiquement au démarrage.",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 8),
        }, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
        };
        var btnClose = new Button { Text = "&Fermer", AutoSize = true, DialogResult = DialogResult.OK };
        var btnCopy = new Button { Text = "&Copier l'exemple", AutoSize = true };
        btnCopy.Click += (_, _) =>
        {
            Clipboard.SetText(example.Text);
            btnCopy.Text = "Exemple copié ✔";
            btnCopy.Enabled = false;
        };
        buttons.Controls.Add(btnClose);
        buttons.Controls.Add(btnCopy);
        root.Controls.Add(buttons, 0, 3);

        Controls.Add(root);
        AcceptButton = btnClose;
        CancelButton = btnClose;
    }
}
