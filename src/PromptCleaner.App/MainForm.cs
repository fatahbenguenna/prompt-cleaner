using PromptCleaner.Core.Config;
using PromptCleaner.Core.Model;
using PromptCleaner.Core.Pipeline;

namespace PromptCleaner.App;

public sealed class MainForm : Form
{
    // Vert/rouge : fond ET teinte du texte, pour rester lisible par les daltoniens (NFR-6).
    private static readonly Color ReplacedBack = Color.FromArgb(0xC8, 0xF7, 0xC5);
    private static readonly Color ReplacedFore = Color.FromArgb(0x1B, 0x5E, 0x20);
    private static readonly Color AlertBack = Color.FromArgb(0xF7, 0xC5, 0xC5);
    private static readonly Color AlertFore = Color.FromArgb(0x8B, 0x1A, 0x1A);

    private const string AutoConfigFileName = "prompt-cleaner.cfg";

    private readonly Button _btnLoadConfig;
    private readonly Label _lblConfig;
    private readonly Button _btnPaste;
    private readonly TextBox _txtInput;
    private readonly Button _btnClean;
    private readonly Button _btnCopy;
    private readonly RichTextBox _rtbResult;
    private readonly ToolStripStatusLabel _statusLabel;

    private IReadOnlyList<ReplacementRule> _rules = Array.Empty<ReplacementRule>();
    private CleanResult? _lastResult;

    public MainForm()
    {
        Text = "prompt-cleaner";
        MinimumSize = new Size(720, 620);
        Size = new Size(860, 700);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var textFont = new Font("Consolas", 10f);

        _btnLoadConfig = new Button { Text = "Charger &config…", AutoSize = true };
        _lblConfig = new Label
        {
            Text = "Aucune config chargée",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
        };

        _btnPaste = new Button { Text = "C&oller", AutoSize = true, Anchor = AnchorStyles.Right };
        _txtInput = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            AcceptsReturn = true,
            AcceptsTab = true,
            Font = textFont,
            Dock = DockStyle.Fill,
            AccessibleName = "Texte d'entrée à nettoyer",
        };

        _btnClean = new Button
        {
            Text = "🧹  C&lean",
            AutoSize = true,
            Padding = new Padding(24, 6, 24, 6),
            Font = new Font(Font, FontStyle.Bold),
            Anchor = AnchorStyles.None,
            Enabled = false,
        };

        _btnCopy = new Button { Text = "Co&pier", AutoSize = true, Anchor = AnchorStyles.Right, Enabled = false };
        _rtbResult = new RichTextBox
        {
            ReadOnly = true,
            BackColor = SystemColors.Window,
            // Les liens détectés fausseraient le placement des couleurs.
            DetectUrls = false,
            HideSelection = false,
            Font = textFont,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            Dock = DockStyle.Fill,
            AccessibleName = "Résultat nettoyé (copié dans le presse-papier)",
        };

        _statusLabel = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        Controls.Add(BuildLayout());
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_statusLabel);
        Controls.Add(statusStrip);

        _btnLoadConfig.Click += OnLoadConfigClicked;
        _btnPaste.Click += OnPasteClicked;
        _btnClean.Click += OnCleanClicked;
        _btnCopy.Click += OnCopyClicked;
        _txtInput.TextChanged += (_, _) => _btnClean.Enabled = _txtInput.TextLength > 0;
        KeyDown += OnFormKeyDown;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Confort portable (FR-1.6) : une config posée à côté de l'exe est
        // chargée d'office. AppContext.BaseDirectory reste valide en single-file.
        string autoConfig = Path.Combine(AppContext.BaseDirectory, AutoConfigFileName);
        if (File.Exists(autoConfig))
        {
            LoadConfig(autoConfig, auto: true);
        }
        else
        {
            SetStatus("Prêt — chargez un fichier de configuration puis collez votre texte.");
        }
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 1,
            RowCount = 7,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // barre config
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // en-tête entrée
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));        // zone entrée
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // bouton Clean
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // en-tête résultat
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));        // zone résultat
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // légende

        var configBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
        };
        configBar.Controls.Add(_btnLoadConfig);
        _lblConfig.Margin = new Padding(8, 8, 0, 0);
        configBar.Controls.Add(_lblConfig);
        root.Controls.Add(configBar, 0, 0);

        root.Controls.Add(BuildHeaderRow("Texte d'entrée", _btnPaste), 0, 1);
        root.Controls.Add(_txtInput, 0, 2);

        var cleanPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
        };
        cleanPanel.Controls.Add(_btnClean, 0, 0);
        root.Controls.Add(cleanPanel, 0, 3);

        root.Controls.Add(BuildHeaderRow("Résultat (copié automatiquement dans le presse-papier)", _btnCopy), 0, 4);
        root.Controls.Add(_rtbResult, 0, 5);
        root.Controls.Add(BuildLegend(), 0, 6);
        return root;
    }

    private static Control BuildHeaderRow(string title, Button rightButton)
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(SystemFonts.DefaultFont ?? Control.DefaultFont, FontStyle.Bold),
        }, 0, 0);
        header.Controls.Add(rightButton, 1, 0);
        return header;
    }

    private static Control BuildLegend()
    {
        var legend = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        legend.Controls.Add(new Label { Text = "Légende :", AutoSize = true, Margin = new Padding(0, 3, 6, 0) });
        legend.Controls.Add(MakeLegendChip("remplacé", ReplacedBack, ReplacedFore));
        legend.Controls.Add(MakeLegendChip("suspect non remplacé (à vérifier)", AlertBack, AlertFore));
        return legend;
    }

    private static Control MakeLegendChip(string text, Color back, Color fore)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            BackColor = back,
            ForeColor = fore,
            Padding = new Padding(6, 3, 6, 3),
            Margin = new Padding(0, 0, 10, 0),
        };
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter && _btnClean.Enabled)
        {
            e.SuppressKeyPress = true;
            _btnClean.PerformClick();
        }
    }

    private void OnLoadConfigClicked(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choisir le fichier de configuration",
            Filter = "Fichiers de configuration (*.cfg;*.txt)|*.cfg;*.txt|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadConfig(dialog.FileName, auto: false);
        }
    }

    private void LoadConfig(string path, bool auto)
    {
        try
        {
            var report = ConfigParser.ParseFile(path);
            _rules = report.Rules;
            _lblConfig.Text = DescribeConfig(report);
            _lblConfig.ForeColor = SystemColors.ControlText;
            SetStatus(auto
                ? $"Config « {AutoConfigFileName} » chargée automatiquement — {report.Rules.Count} règle(s)."
                : $"Config chargée — {report.Rules.Count} règle(s).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // L'état précédent (règles + libellé) est volontairement conservé (S2.2).
            MessageBox.Show(this,
                $"Impossible de lire le fichier de configuration :\n{path}\n\n{ex.Message}",
                "prompt-cleaner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string DescribeConfig(ConfigLoadReport report)
    {
        string text = $"{Path.GetFileName(report.FilePath)} — {report.Rules.Count} règle(s)";
        if (report.IgnoredLineCount > 0)
        {
            text += $", {report.IgnoredLineCount} ligne(s) ignorée(s)";
        }

        if (report.DuplicateKeywordCount > 0)
        {
            text += $", {report.DuplicateKeywordCount} doublon(s)";
        }

        return text;
    }

    private void OnPasteClicked(object? sender, EventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            _txtInput.Text = Clipboard.GetText();
            SetStatus("Texte collé depuis le presse-papier.");
        }
        else
        {
            SetStatus("Le presse-papier ne contient pas de texte.");
        }
    }

    private async void OnCleanClicked(object? sender, EventArgs e)
    {
        string input = _txtInput.Text;
        if (input.Length == 0)
        {
            return;
        }

        _btnClean.Enabled = false;
        UseWaitCursor = true;
        SetStatus("Nettoyage en cours…");
        try
        {
            var rules = _rules;
            var result = await Task.Run(() => new CleaningPipeline().Clean(input, rules));
            _lastResult = result;
            RenderResult(result);
            CopyToClipboard(result.CleanText);
            _btnCopy.Enabled = result.CleanText.Length > 0;
            string status = DescribeResult(result.Stats);
            if (result.Warnings.Count > 0)
            {
                status += " — " + string.Join(" ", result.Warnings);
            }

            SetStatus(status);
        }
        catch (Exception ex)
        {
            SetStatus("Échec du nettoyage.");
            MessageBox.Show(this, $"Le nettoyage a échoué :\n\n{ex.Message}",
                "prompt-cleaner", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnClean.Enabled = _txtInput.TextLength > 0;
        }
    }

    private void OnCopyClicked(object? sender, EventArgs e)
    {
        if (_lastResult is { CleanText.Length: > 0 })
        {
            CopyToClipboard(_lastResult.CleanText);
            SetStatus("Résultat recopié dans le presse-papier.");
        }
    }

    private static string DescribeResult(CleanStats stats)
    {
        if (stats.TotalReplacements == 0 && stats.Alerts == 0)
        {
            return "Aucune donnée sensible détectée — texte copié dans le presse-papier ✔";
        }

        return $"{stats.TotalReplacements} remplacement(s) " +
               $"({stats.ConfigReplacements} config, {stats.AutoReplacements} auto) — " +
               $"{stats.Alerts} alerte(s) — copié dans le presse-papier ✔";
    }

    private void RenderResult(CleanResult result)
    {
        var rtb = _rtbResult;
        rtb.SuspendLayout();
        try
        {
            rtb.Clear();
            rtb.Text = result.CleanText;

            // RichTextBox normalise les fins de ligne \r\n en \n : chaque '\r'
            // en amont d'un span décale son offset d'affichage d'un caractère.
            string text = result.CleanText;
            int carriageReturns = 0;
            int cursor = 0;
            foreach (var span in result.Spans)
            {
                while (cursor < span.Start)
                {
                    if (text[cursor] == '\r')
                    {
                        carriageReturns++;
                    }

                    cursor++;
                }

                int displayStart = span.Start - carriageReturns;
                int end = span.Start + span.Length;
                int crInSpan = 0;
                while (cursor < end)
                {
                    if (text[cursor] == '\r')
                    {
                        crInSpan++;
                    }

                    cursor++;
                }

                carriageReturns += crInSpan;

                rtb.Select(displayStart, span.Length - crInSpan);
                bool replaced = span.Kind == SpanKind.Replaced;
                rtb.SelectionBackColor = replaced ? ReplacedBack : AlertBack;
                rtb.SelectionColor = replaced ? ReplacedFore : AlertFore;
            }

            rtb.Select(0, 0);
        }
        finally
        {
            rtb.ResumeLayout();
        }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            if (text.Length > 0)
            {
                Clipboard.SetText(text);
            }
            else
            {
                Clipboard.Clear();
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException)
        {
            // Presse-papier verrouillé par une autre application : on informe sans crasher.
            SetStatus("Impossible d'écrire dans le presse-papier (occupé par une autre application).");
        }
    }

    private void SetStatus(string text) => _statusLabel.Text = text;
}
