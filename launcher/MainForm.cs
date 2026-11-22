using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace SanAndreasHorizonLauncher;

internal sealed class MainForm : Form
{
    private const string DffUrl = "https://raw.githubusercontent.com/boruto4713-creator/SanAndreasHorizon-Assets/main/bmx.dff";
    private const string TxdUrl = "https://raw.githubusercontent.com/boruto4713-creator/SanAndreasHorizon-Assets/main/bmx.txd";
    private const long DffSize = 16_250_880;
    private const long TxdSize = 563_200;
    private const string DffSha256 = "92C92C1269ADA1DADEDDA7AE63B9B2142B8922A07DAF1EA22E8E57BD6C904E99";
    private const string TxdSha256 = "924F5F468E91C3DE4A79C3E598F912ABE0303B41E7CC596F3DAAD59394B173DD";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    private static readonly Color BackgroundColor = Color.FromArgb(7, 17, 30);
    private static readonly Color CardColor = Color.FromArgb(13, 29, 47);
    private static readonly Color FieldColor = Color.FromArgb(6, 19, 33);
    private static readonly Color AccentColor = Color.FromArgb(0, 203, 214);
    private static readonly Color TextColor = Color.FromArgb(232, 242, 250);
    private static readonly Color MutedColor = Color.FromArgb(139, 160, 180);

    private readonly LauncherSettings _settings;
    private readonly FlowLayoutPanel _content;
    private readonly TextBox _host = new();
    private readonly TextBox _port = new();
    private readonly TextBox _nickname = new();
    private readonly TextBox _gameFolder = new();
    private readonly TextBox _clientExecutable = new();
    private readonly ComboBox _clientType = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly TextBox _log = new();
    private readonly Button _installButton;
    private readonly Button _restoreButton;
    private readonly Button _playButton;

    public MainForm()
    {
        _settings = LauncherSettings.Load();

        Text = "San Andreas Horizon Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 800);
        Size = new Size(1120, 900);
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackgroundColor,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        _content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = BackgroundColor,
            Padding = new Padding(22, 18, 22, 24)
        };
        root.Controls.Add(_content, 0, 1);

        BuildCards();
        LoadSettingsIntoControls();
        UpdateCardWidths();

        _content.SizeChanged += (_, _) => UpdateCardWidths();
        FormClosing += (_, _) => SaveSettings();
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 26, 44),
            Padding = new Padding(28, 16, 28, 12)
        };

        var mark = new Panel
        {
            Location = new Point(28, 24),
            Size = new Size(64, 64),
            BackColor = AccentColor
        };
        var markText = new Label
        {
            Dock = DockStyle.Fill,
            Text = "H",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 29F, FontStyle.Bold),
            ForeColor = Color.FromArgb(4, 20, 32)
        };
        mark.Controls.Add(markText);
        header.Controls.Add(mark);

        header.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(110, 24),
            Text = "SAN ANDREAS HORIZON",
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            ForeColor = TextColor
        });
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(112, 65),
            Text = "TU ACCESO A HORIZON  //  MULTIPLAYER",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = AccentColor
        });
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(840, 49),
            Text = "LAUNCHER",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = MutedColor
        });

        return header;
    }

    private void BuildCards()
    {
        var serverCard = CreateCard(
            "01  ·  SERVIDOR Y JUGADOR",
            "Configura el destino al que se conectará el cliente.",
            142);
        AddField(serverCard, "DIRECCIÓN DEL SERVIDOR", _host, 20, 66, 440);
        AddField(serverCard, "PUERTO", _port, 480, 66, 110);
        AddField(serverCard, "NOMBRE DE JUGADOR", _nickname, 610, 66, 260);
        _content.Controls.Add(serverCard);

        var gameCard = CreateCard(
            "02  ·  INSTALACIÓN DEL JUEGO",
            "Selecciona tu carpeta de GTA San Andreas y el ejecutable multijugador.",
            196);
        AddField(gameCard, "CARPETA DE GTA SAN ANDREAS", _gameFolder, 20, 60, 690);
        var browseGame = CreateButton("EXAMINAR", false, 140, 36);
        browseGame.Location = new Point(730, 83);
        browseGame.Click += (_, _) => BrowseGameFolder();
        gameCard.Controls.Add(browseGame);

        AddField(gameCard, "CLIENTE MULTIJUGADOR (.EXE)", _clientExecutable, 20, 119, 690);
        var browseClient = CreateButton("BUSCAR EXE", false, 140, 36);
        browseClient.Location = new Point(730, 142);
        browseClient.Click += (_, _) => BrowseClientExecutable();
        gameCard.Controls.Add(browseClient);
        _content.Controls.Add(gameCard);

        var typeCard = CreateCard(
            "03  ·  TIPO DE CLIENTE",
            "Elige el cliente que tienes instalado. El launcher prepara los parámetros de conexión.",
            114);
        _clientType.DropDownStyle = ComboBoxStyle.DropDownList;
        _clientType.Items.AddRange(new object[] { "open.mp", "SA-MP" });
        _clientType.BackColor = FieldColor;
        _clientType.ForeColor = TextColor;
        _clientType.FlatStyle = FlatStyle.Flat;
        _clientType.Font = new Font("Segoe UI", 10F);
        _clientType.Location = new Point(20, 67);
        _clientType.Size = new Size(220, 34);
        typeCard.Controls.Add(_clientType);

        var typeHint = new Label
        {
            Location = new Point(270, 69),
            Size = new Size(590, 34),
            Text = "open.mp añade la ruta de GTA con -g. SA-MP usa los datos de conexión y el nombre.",
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        typeCard.Controls.Add(typeHint);
        _content.Controls.Add(typeCard);

        var actionsCard = CreateCard(
            "04  ·  ACCIONES",
            "La BMX se instala en el archivo original del juego. No utiliza ASI ni modifica el servidor.",
            134);
        _installButton = CreateButton("INSTALAR / ACTUALIZAR BMX", true, 270, 48);
        _installButton.Location = new Point(20, 72);
        _installButton.Click += async (_, _) => await InstallBmxAsync();
        actionsCard.Controls.Add(_installButton);

        _restoreButton = CreateButton("RESTAURAR ORIGINAL", false, 210, 48);
        _restoreButton.Location = new Point(310, 72);
        _restoreButton.Click += async (_, _) => await RestoreOriginalAsync();
        actionsCard.Controls.Add(_restoreButton);

        _playButton = CreateButton("JUGAR", true, 180, 48);
        _playButton.Location = new Point(540, 72);
        _playButton.Click += (_, _) => LaunchGame();
        actionsCard.Controls.Add(_playButton);
        _content.Controls.Add(actionsCard);

        var statusCard = CreateCard(
            "05  ·  ESTADO",
            "El progreso y los avisos del launcher aparecerán aquí.",
            176);
        _status.Text = "Listo. Selecciona tu carpeta de GTA San Andreas.";
        _status.Location = new Point(20, 57);
        _status.Size = new Size(850, 22);
        _status.ForeColor = AccentColor;
        _status.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        statusCard.Controls.Add(_status);

        _progress.Location = new Point(20, 85);
        _progress.Size = new Size(850, 12);
        _progress.Style = ProgressBarStyle.Continuous;
        statusCard.Controls.Add(_progress);

        _log.Location = new Point(20, 108);
        _log.Size = new Size(850, 48);
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.BackColor = FieldColor;
        _log.ForeColor = MutedColor;
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.Font = new Font("Consolas", 8.5F);
        statusCard.Controls.Add(_log);
        _content.Controls.Add(statusCard);

        var note = new Label
        {
            AutoSize = false,
            Width = 900,
            Height = 35,
            Text = "HORIZON  •  El launcher conserva una copia del gta3.img antes de instalar la BMX.",
            ForeColor = MutedColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 0, 8)
        };
        _content.Controls.Add(note);
    }

    private Panel CreateCard(string title, string description, int height)
    {
        var card = new Panel
        {
            Height = height,
            Width = 940,
            BackColor = CardColor,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(18)
        };

        card.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(20, 12),
            Text = title,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextColor
        });
        card.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(21, 37),
            Text = description,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = MutedColor
        });

        return card;
    }

    private static void AddField(Panel parent, string caption, TextBox box, int x, int y, int width)
    {
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(x, y),
            Text = caption,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = MutedColor
        });

        box.Location = new Point(x, y + 22);
        box.Size = new Size(width, 32);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = FieldColor;
        box.ForeColor = TextColor;
        box.Font = new Font("Segoe UI", 10F);
        box.Margin = new Padding(0);
        parent.Controls.Add(box);
    }

    private Button CreateButton(string caption, bool primary, int width, int height)
    {
        var button = new Button
        {
            Text = caption,
            Size = new Size(width, height),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? AccentColor : Color.FromArgb(21, 43, 64),
            ForeColor = primary ? Color.FromArgb(4, 20, 32) : TextColor,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(54, 83, 108);
        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(67, 225, 227)
            : Color.FromArgb(32, 62, 88);
        return button;
    }

    private void UpdateCardWidths()
    {
        int width = Math.Max(820, _content.ClientSize.Width - 58);
        foreach (Control control in _content.Controls)
        {
            if (control is Panel panel)
                panel.Width = width;
            else if (control is Label label)
                label.Width = width - 8;
        }
    }

    private void LoadSettingsIntoControls()
    {
        _host.Text = _settings.Host;
        _port.Text = _settings.Port.ToString();
        _nickname.Text = _settings.Nickname;
        _gameFolder.Text = _settings.GameFolder;
        _clientExecutable.Text = _settings.ClientExecutable;
        _clientType.SelectedItem = _settings.ClientType;
        if (_clientType.SelectedIndex < 0)
            _clientType.SelectedItem = "open.mp";

        if (!Directory.Exists(_gameFolder.Text))
            TryDetectGameFolder();
        if (Directory.Exists(_gameFolder.Text))
            DetectClientInGameFolder(_gameFolder.Text);

        Log("Launcher iniciado. BMX sin ASI: instalación directa en gta3.img.");
    }

    private void TryDetectGameFolder()
    {
        string[] candidates =
        {
            @"C:\Games\GTA San Andreas",
            @"C:\Program Files (x86)\Rockstar Games\GTA San Andreas",
            @"C:\Program Files\Rockstar Games\GTA San Andreas",
            @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto San Andreas"
        };

        string? found = candidates.FirstOrDefault(IsValidGameFolder);
        if (found is not null)
        {
            _gameFolder.Text = found;
            DetectClientInGameFolder(found);
        }
    }

    private void BrowseGameFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta donde está gta_sa.exe",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = Directory.Exists(_gameFolder.Text)
                ? _gameFolder.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        if (!IsValidGameFolder(dialog.SelectedPath))
        {
            MessageBox.Show(this,
                "La carpeta seleccionada debe contener gta_sa.exe y models/gta3.img.",
                "Carpeta de GTA no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _gameFolder.Text = dialog.SelectedPath;
        DetectClientInGameFolder(dialog.SelectedPath);
        SaveSettings();
        Log("Carpeta de GTA seleccionada.");
    }

    private static bool IsValidGameFolder(string folder)
    {
        return !string.IsNullOrWhiteSpace(folder)
            && File.Exists(Path.Combine(folder, "gta_sa.exe"))
            && File.Exists(Path.Combine(folder, "models", "gta3.img"));
    }

    private void DetectClientInGameFolder(string folder)
    {
        if (!File.Exists(_clientExecutable.Text))
        {
            string? candidate = new[] { "omp-launcher.exe", "samp.exe" }
                .Select(name => Path.Combine(folder, name))
                .FirstOrDefault(File.Exists);

            if (candidate is not null)
            {
                _clientExecutable.Text = candidate;
                _clientType.SelectedItem = candidate.EndsWith("samp.exe", StringComparison.OrdinalIgnoreCase)
                    ? "SA-MP"
                    : "open.mp";
            }
        }
    }

    private void BrowseClientExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Selecciona el cliente multijugador",
            Filter = "Aplicaciones de Windows (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (File.Exists(_clientExecutable.Text))
            dialog.InitialDirectory = Path.GetDirectoryName(_clientExecutable.Text);

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _clientExecutable.Text = dialog.FileName;
        string name = Path.GetFileName(dialog.FileName);
        if (name.Equals("samp.exe", StringComparison.OrdinalIgnoreCase))
            _clientType.SelectedItem = "SA-MP";
        else if (name.Equals("omp-launcher.exe", StringComparison.OrdinalIgnoreCase))
            _clientType.SelectedItem = "open.mp";

        SaveSettings();
        Log("Cliente multijugador seleccionado.");
    }

    private async Task InstallBmxAsync()
    {
        if (!TryGetGamePaths(out string gameFolder, out string imgPath))
            return;

        if (IsGameRunning())
        {
            MessageBox.Show(this,
                "Cierra GTA San Andreas antes de instalar o actualizar el modelo.",
                "GTA está abierto", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirmation = MessageBox.Show(this,
            "Se reemplazarán las entradas bmx.dff y bmx.txd dentro de models/gta3.img.\n\n" +
            "El launcher guardará una copia original llamada gta3.img.horizon-original.bak.\n" +
            "No se instalará ningún ASI y no se tocará el servidor.\n\n¿Continuar?",
            "Instalar BMX de Horizon", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirmation != DialogResult.Yes)
            return;

        SetBusy(true);
        _progress.Value = 0;
        try
        {
            Log("Descargando bmx.dff y bmx.txd...");
            _status.Text = "Descargando y verificando los modelos...";
            var downloads = await Task.WhenAll(
                DownloadVerifiedAsync(DffUrl, DffSize, DffSha256),
                DownloadVerifiedAsync(TxdUrl, TxdSize, TxdSha256));

            Log("Archivos descargados. SHA-256 correcto.");
            _status.Text = "Actualizando el archivo IMG. No cierres el launcher.";

            var progress = new Progress<int>(value =>
            {
                _progress.Value = Math.Clamp(value, 0, 100);
                _status.Text = "Instalando BMX... " + _progress.Value + "%";
            });

            await ImgV2Archive.InstallBmxAsync(imgPath, downloads[0], downloads[1],
                progress, CancellationToken.None);

            _progress.Value = 100;
            _status.Text = "BMX instalada correctamente, sin ASI.";
            Log("BMX instalada en gta3.img. La copia original está guardada.");
            SaveSettings();
            MessageBox.Show(this,
                "La BMX de Horizon quedó instalada. El ID 481 se mantiene; no se modificaron los archivos del servidor.",
                "Instalación completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "No se pudo completar la instalación.";
            Log("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Error al instalar BMX",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static async Task<byte[]> DownloadVerifiedAsync(string url, long expectedSize, string expectedHash)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long contentLength && contentLength != expectedSize)
            throw new InvalidDataException("El tamaño publicado no coincide con el modelo esperado.");

        await using var input = await response.Content.ReadAsStreamAsync();
        using var memory = new MemoryStream(checked((int)expectedSize));
        byte[] buffer = new byte[256 * 1024];

        while (true)
        {
            int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read == 0)
                break;

            if (memory.Length + read > expectedSize)
                throw new InvalidDataException("El archivo descargado supera el tamaño esperado.");

            await memory.WriteAsync(buffer.AsMemory(0, read));
        }

        if (memory.Length != expectedSize)
            throw new InvalidDataException("La descarga quedó incompleta.");

        byte[] data = memory.ToArray();
        string actualHash = Convert.ToHexString(SHA256.HashData(data));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El SHA-256 del modelo descargado no coincide.");

        return data;
    }

    private async Task RestoreOriginalAsync()
    {
        if (!TryGetGamePaths(out _, out string imgPath))
            return;

        if (IsGameRunning())
        {
            MessageBox.Show(this,
                "Cierra GTA San Andreas antes de restaurar el archivo original.",
                "GTA está abierto", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string backup = ImgV2Archive.BackupPathFor(imgPath);
        if (!File.Exists(backup))
        {
            MessageBox.Show(this,
                "No se encontró la copia de seguridad. Primero debes instalar la BMX con este launcher.",
                "Copia de seguridad no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this,
            "Se restaurará la copia original de gta3.img. La BMX personalizada dejará de aparecer en este juego.\n\n¿Continuar?",
            "Restaurar GTA original", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetBusy(true);
        try
        {
            _status.Text = "Restaurando gta3.img original...";
            await Task.Run(() => ImgV2Archive.RestoreOriginal(imgPath));
            _progress.Value = 100;
            _status.Text = "Archivo original restaurado.";
            Log("Se restauró la copia original de gta3.img.");
        }
        catch (Exception ex)
        {
            _status.Text = "No se pudo restaurar el archivo.";
            Log("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Error al restaurar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void LaunchGame()
    {
        if (!TryGetGamePaths(out string gameFolder, out _))
            return;

        string clientPath = _clientExecutable.Text.Trim();
        if (!File.Exists(clientPath))
        {
            MessageBox.Show(this,
                "Selecciona el ejecutable del cliente multijugador (.exe). No selecciones gta_sa.exe.",
                "Falta el cliente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string host = _host.Text.Trim();
        if (string.IsNullOrWhiteSpace(host) || host.Any(char.IsWhiteSpace))
        {
            MessageBox.Show(this, "Escribe una dirección de servidor válida.",
                "Servidor no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_port.Text.Trim(), out int port) || port < 1 || port > 65535)
        {
            MessageBox.Show(this, "El puerto debe estar entre 1 y 65535.",
                "Puerto no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string nickname = _nickname.Text.Trim();
        if (nickname.Length < 1 || nickname.Length > 24)
        {
            MessageBox.Show(this, "El nombre de jugador debe tener entre 1 y 24 caracteres.",
                "Nombre no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SaveSettings();

            var start = new ProcessStartInfo
            {
                FileName = clientPath,
                WorkingDirectory = Path.GetDirectoryName(clientPath) ?? gameFolder,
                UseShellExecute = false
            };

            start.ArgumentList.Add("-h");
            start.ArgumentList.Add(host);
            start.ArgumentList.Add("-p");
            start.ArgumentList.Add(port.ToString());
            start.ArgumentList.Add("-n");
            start.ArgumentList.Add(nickname);

            if (string.Equals(_clientType.SelectedItem?.ToString(), "open.mp", StringComparison.OrdinalIgnoreCase))
            {
                start.ArgumentList.Add("-g");
                start.ArgumentList.Add(gameFolder);
            }

            Process.Start(start);
            _status.Text = "Cliente multijugador iniciado.";
            Log("Conectando a " + host + ":" + port + " con el nombre " + nickname + ".");
        }
        catch (Exception ex)
        {
            Log("ERROR al iniciar el cliente: " + ex.Message);
            MessageBox.Show(this, ex.Message, "No se pudo iniciar el cliente",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryGetGamePaths(out string gameFolder, out string imgPath)
    {
        gameFolder = _gameFolder.Text.Trim().Trim('"');
        imgPath = Path.Combine(gameFolder, "models", "gta3.img");

        if (!IsValidGameFolder(gameFolder))
        {
            MessageBox.Show(this,
                "Selecciona una carpeta válida que contenga gta_sa.exe y models/gta3.img.",
                "Instalación de GTA no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private static bool IsGameRunning()
    {
        try
        {
            return Process.GetProcessesByName("gta_sa").Length > 0;
        }
        catch
        {
            return true;
        }
    }

    private void SetBusy(bool busy)
    {
        _installButton.Enabled = !busy;
        _restoreButton.Enabled = !busy;
        _playButton.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Host = _host.Text.Trim();
            _settings.Port = int.TryParse(_port.Text, out int port) ? port : 7777;
            _settings.Nickname = _nickname.Text.Trim();
            _settings.GameFolder = _gameFolder.Text.Trim().Trim('"');
            _settings.ClientExecutable = _clientExecutable.Text.Trim().Trim('"');
            _settings.ClientType = _clientType.SelectedItem?.ToString() ?? "open.mp";
            _settings.Save();
        }
        catch (Exception ex)
        {
            Log("No se pudieron guardar los ajustes: " + ex.Message);
        }
    }

    private void Log(string message)
    {
        if (_log.IsDisposed)
            return;

        _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        string[] lines = _log.Lines;
        if (lines.Length > 20)
            _log.Lines = lines[^20..];
    }
}
