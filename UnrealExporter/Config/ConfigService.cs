using Spectre.Console;
using Newtonsoft.Json;
using System.Diagnostics;

public class ConfigService
{
    public static string ConfigsDirectory = Path.Combine(AppContext.BaseDirectory, "configs");

    public static void CreateConfig()
    {
        // AnsiConsole.Clear();
        AnsiConsole.MarkupLine("Values in [dim]parentheses[/] are examples, and [green]green[/] values will auto-complete if left blank.\n");


        var config = new ConfigObj
        {
            ConfigTitle = Ask("Enter the game title", "My Game")
        };
        while (string.IsNullOrWhiteSpace(config.ConfigTitle))
        {
            AnsiConsole.MarkupLine($"[red]Game title cannot be blank.[/]");
            config.ConfigTitle = Ask();
        }
        AnsiConsole.WriteLine("");


        config.GamePath = Ask("Where is the game folder?", Path.Combine("C:", "Program Files", "MyGame"));
        while (!Directory.Exists(config.GamePath))
        {
            AnsiConsole.MarkupLine($"[red]Path \"{config.GamePath}\" does not exist.[/]");
            config.GamePath = Ask();
        }
        AnsiConsole.WriteLine("");


        config.OutputPath = Ask("Where should extracted files be saved?", Path.Combine(config.GamePath, "extracted"), true);
        while (!PathHelpers.IsDirectoryWritable(config.OutputPath, out _))
        {
            AnsiConsole.MarkupLine($"[red]Directory \"{config.OutputPath}\" is not writable.[/]");
            config.OutputPath = Ask();
        }
        AnsiConsole.WriteLine("");


        AnsiConsole.MarkupLine("[dim]Attempting to auto-detect Unreal Engine version from the game's .exe file...[/]");
        var detectedEngineVersion = DetectEngineVersion(config.GamePath);

        config.EngineVersion = Ask("Unreal Engine Version", detectedEngineVersion ?? "5.1", !string.IsNullOrWhiteSpace(detectedEngineVersion));
        while (string.IsNullOrWhiteSpace(config.EngineVersion))
        {
            AnsiConsole.MarkupLine($"[red]Unreal Engine version cannot be blank.[/]");
            config.EngineVersion = Ask();
        }
        AnsiConsole.WriteLine("");


        AnsiConsole.MarkupLine("[blue]Some games require decryption keys or mapping files.[/]\n");
        AnsiConsole.MarkupLine($"[dim]Searching aes.txt and olderkeys.txt...[/]");
        var detectedKeys =
            LookupAesKeys(config.ConfigTitle, Path.Combine(AppContext.BaseDirectory, "aes.txt"))
            .Concat(LookupAesKeys(config.ConfigTitle, Path.Combine(AppContext.BaseDirectory, "olderkeys.txt")))
            .Distinct()
            .ToArray();

        if (detectedKeys.Length > 0)
        {
            AnsiConsole.MarkupLine($"[blue]Found {detectedKeys.Length} AES keys for \"{config.ConfigTitle}\".[/]\n");
            AnsiConsole.MarkupLine("If your game needs additional AES keys, enter them now [dim](0x...)[/]:");
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]No keys found for \"{config.ConfigTitle}\" in aes.txt and olderkeys.txt.[/]\n");
            AnsiConsole.MarkupLine("If your game needs AES keys, enter them now [dim](0x...)[/]:");
        }

        AnsiConsole.MarkupLine("[dim italic]One entry per line, leave blank to continue[/]");
        var aesKeys = new List<string>(detectedKeys);
        while (true)
        {
            var key = Ask();
            if (string.IsNullOrWhiteSpace(key)) break;
            if (!PathHelpers.IsValidAesKey(key))
            {
                AnsiConsole.MarkupLine($"[red]AES key \"{key}\" is not valid. Expected \"0x\" followed by 64 hexadecimal characters.[/]");
                continue;
            }
            aesKeys.Add(key);
        }
        config.AesKeys = aesKeys.Distinct().ToArray();
        AnsiConsole.WriteLine("");


        AnsiConsole.MarkupLine($"If your game needs a mapping file, enter the name of the file, which should be placed in UnrealExporter/mappings [dim](MyGame.usmap)[/]:");
        AnsiConsole.MarkupLine("[dim italic]Leave blank to skip[/]");
        config.MappingFileName = Ask();
        AnsiConsole.WriteLine("");


        AnsiConsole.MarkupLine($"Virtual paths to extract and their output file types [dim]({Path.Combine("MyGame", "DataTables", ".*.uasset:json")}, {Path.Combine("MyGame", "UI", ".*.uasset:png")})[/]:");
        AnsiConsole.MarkupLine("[dim italic]One entry per line, leave blank to continue[/]");
        var exportPaths = new List<string>();
        while (true)
        {
            var p = Ask();
            if (string.IsNullOrWhiteSpace(p)) break;
            exportPaths.Add(p);
        }
        config.ExportPaths = exportPaths.ToArray();
        AnsiConsole.WriteLine("");


        AnsiConsole.MarkupLine($"Virtual paths to [bold]exclude[/] [dim]({Path.Combine("MyGame", "UI", "UserInterface", ".*")})[/]:");
        AnsiConsole.MarkupLine("[dim italic]One entry per line, leave blank to continue[/]");
        var excludePaths = new List<string>();
        while (true)
        {
            var p = Ask();
            if (string.IsNullOrWhiteSpace(p)) break;
            excludePaths.Add(p);
        }
        config.ExcludePaths = exportPaths.ToArray();
        AnsiConsole.WriteLine("");


        var fileName = Ask(
            "Name your config file",
            PathHelpers.Slugify(PathHelpers.ForceExtension(config.ConfigTitle, ".json")),
            true
        );

        fileName = PathHelpers.Slugify(PathHelpers.ForceExtension(fileName, ".json"));

        var path = Path.Combine(ConfigsDirectory, fileName);

        var json = JsonConvert.SerializeObject(
            config,
            Formatting.Indented,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

        File.WriteAllText(path, json);

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[green]:check_mark: Added {config.ConfigTitle} [dim]([underline link={new Uri(path).AbsoluteUri}]{Markup.Escape(fileName)}[/])[/] to [underline link={new Uri(ConfigsDirectory).AbsoluteUri}]configs[/][/].\n");
    }

    public static ConfigObj LoadConfig(string path)
    {
        var fileLink = $"[gray]([underline]{Markup.Escape(Path.GetFileName(path))}[/])[/]";
        try
        {
            var jsonString = File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<ConfigObj>(jsonString)
                ?? throw new InvalidDataException($"[bold red]Error while loading config:[/] file is null {fileLink}");
            return config;
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"[bold red]Error while loading config:[/] file not found {fileLink}", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"[bold red]Error while loading config:[/] file is not valid JSON {fileLink}", ex);
        }
    }

    public static ValidationResult ValidateConfig(ConfigObj config)
    {
        if (string.IsNullOrWhiteSpace(config.GamePath))
            return ValidationResult.Error("Missing game directory");

        var gameDir = config.GamePath.Trim().Trim('"');

        try
        {
            gameDir = Path.GetFullPath(gameDir);
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"GamePath is not a valid path: [gray]({ex.Message})[/]");
        }

        if (!Directory.Exists(gameDir))
            return ValidationResult.Error($"GamePath [gray]\"{gameDir}\"[/] does not exist");

        if (string.IsNullOrWhiteSpace(config.OutputPath))
            return ValidationResult.Error("Missing output directory");

        var outDir = config.OutputPath.Trim().Trim('"');

        try
        {
            outDir = Path.GetFullPath(outDir);
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Output directory is not a valid path: {ex.Message}");
        }

        try
        {
            Directory.CreateDirectory(outDir);
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Output directory could not be created: [green]{outDir}[/]. {ex.Message}");
        }

        try
        {
            var testFile = Path.Combine(outDir, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Output directory is not writable: [green]{outDir}[/]. {ex.Message}");
        }

        config.GamePath = gameDir;
        config.OutputPath = outDir;

        return ValidationResult.Success();
    }

    public static ConfigObj PromptConfigSelection()
    {
        List<SelectionOption> options = [];

        if (Directory.Exists(ConfigsDirectory))
        {
            var configPaths = Directory
                .EnumerateFiles(ConfigsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .ToList();

            foreach (var path in configPaths)
            {
                var config = LoadConfig(path);
                if (config is null) continue;
                var fileName = Path.GetFileName(path);
                options.Add(new SelectionOption
                {
                    Label = $"{config.ConfigTitle} [dim]({fileName})[/]",
                    Config = config,
                    ConfigPath = path
                });
            }
        }
        else Directory.CreateDirectory(ConfigsDirectory);

        options.Add(new SelectionOption() { Label = "Create new config" });

        var selectedOption = AnsiConsole.Prompt(
            new SelectionPrompt<SelectionOption>()
                .Title("Select a config file or re-run unrealexporter with flags [dim](see unrealexporter --help)[/] to begin extraction:")
                .WrapAround()
                .EnableSearch()
                .UseConverter(option => option.Label)
                .AddChoices(options));

        if (selectedOption.Config == null)
        {
            CreateConfig();
            return PromptConfigSelection();
        }

        return selectedOption.Config;
    }

    public static string StringifyConfig(ConfigObj config)
    {
        string result = "";

        string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;
            if (value.Any(c => char.IsWhiteSpace(c) || c == '"'))
                return $"\"{value.Replace("\"", "\\\"")}\"";
            return value;
        }

        void Add(string flag, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            result += $"{flag} {QuoteIfNeeded(value)} ";
        }

        void AddMany(string flag, IEnumerable<string> values)
        {
            foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
                Add(flag, value);
        }

        Add("--title", config.ConfigTitle);
        Add("--path", config.GamePath);
        Add("--out", config.OutputPath);
        Add("--version", config.EngineVersion);
        AddMany("--aes", config.AesKeys);
        Add("--map", config.MappingFileName);
        if (config.CreateNewCheckpoint) result += "--create-checkpoint ";
        Add("--checkpoint", config.CheckpointFileName);
        AddMany("--export", config.ExportPaths);
        AddMany("--exclude", config.ExcludePaths);
        
        return Markup.Escape(result.Trim());
    }

    static string Ask(string? text = null, string hint = "", bool hintIsDefaultValue = false)
    {
        if (!string.IsNullOrEmpty(text))
        {
            if (!string.IsNullOrEmpty(hint))
            {
                string color = hintIsDefaultValue ? "green" : "dim";
                text += $" [{color}]({hint})[/]:";
            }
            AnsiConsole.MarkupLine(text);
        }

        var prompt = new TextPrompt<string>(">").AllowEmpty();
        if (hintIsDefaultValue) prompt.DefaultValue(hint).HideDefaultValue();
        return AnsiConsole.Prompt(prompt);
    }

    // TODO: reimplement custom version detection (i.e. GAME_TowerOfFantasy, GAME_NevernessToEverness)
    static string? DetectEngineVersion(string GamePath)
    {
        static string? FindGameExe(string gameDir)
        {
            var binaries = Path.Combine(gameDir, "Binaries", "Win64");
            if (Directory.Exists(binaries))
            {
                var shipping = Directory.EnumerateFiles(binaries, "*-Shipping.exe", SearchOption.TopDirectoryOnly)
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.Length)
                    .FirstOrDefault();

                if (shipping != null) return shipping.FullName;

                var anyExe = Directory.EnumerateFiles(binaries, "*.exe", SearchOption.TopDirectoryOnly)
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.Length)
                    .FirstOrDefault();

                if (anyExe != null) return anyExe.FullName;
            }

            var exes = Directory.EnumerateFiles(gameDir, "*.exe", SearchOption.AllDirectories)
                .Where(p => p.Contains($"{Path.DirectorySeparatorChar}Binaries{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.Length)
                .Take(10)
                .ToList();

            return exes.FirstOrDefault()?.FullName;
        }

        var gameExePath = FindGameExe(GamePath);

        if (string.IsNullOrEmpty(gameExePath))
        {
            AnsiConsole.MarkupLine("[dim]Unable to locate executable file.[/]\n");
            return null;
        }

        var fvi = FileVersionInfo.GetVersionInfo(gameExePath);

        string version = "";
        bool hasProductVersion = fvi.ProductMajorPart + fvi.ProductMinorPart > 0;
        bool hasFileVersion = fvi.FileMajorPart + fvi.FileMinorPart > 0;

        if (hasProductVersion) version = $"{fvi.ProductMajorPart}.{fvi.ProductMinorPart}";
        else if (hasFileVersion) version = $"{fvi.FileMajorPart}.{fvi.FileMinorPart}";

        if (!string.IsNullOrEmpty(version))
        {
            AnsiConsole.MarkupLine($"[blue]Found version {version}, but [underline link=https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs]some games[/] require a custom version.[/]\n");
            return version;
        }

        AnsiConsole.WriteLine("[dim]Executable didn't contain version info.[/]\n");
        return null;
    }

    public static string[] LookupAesKeys(string configTitle, string keysFilePath)
    {
        if (!File.Exists(keysFilePath)) return [];

        var slug = PathHelpers.Slugify(configTitle).ToLowerInvariant();

        return File.ReadAllLines(keysFilePath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l =>
            {
                var parts = l.Trim().Split("  ", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return ((string name, string key)?)null;
                var name = PathHelpers.Slugify(parts[0]).ToLowerInvariant();
                var key = parts[^1].Trim();
                return ((string name, string key)?)(name, key);
            })
            .Where(t => t != null && (t.Value.name.Contains(slug) || slug.Contains(t.Value.name)))
            .Where(t => PathHelpers.IsValidAesKey(t!.Value.key))
            .Select(t => t!.Value.key)
            .ToArray();
    }
}