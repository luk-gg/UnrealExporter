// Config/ConfigService.cs
using Spectre.Console;
using Slugify;
using Newtonsoft.Json;
using System.Diagnostics;

public class ConfigService
{
    public static string ConfigsDirectory = Path.Combine(AppContext.BaseDirectory, "configs");

    public static void CreateConfig()
    {
        // AnsiConsole.Clear();
        AnsiConsole.MarkupLine("Values in [dim]parentheses[/] are examples, and [green]green[/] values will auto-complete if left blank.\n");

        // TODO: Should this allow for blank strings? "(leave blank to use the file name)"?
        var config = new ConfigObj
        {
            ConfigTitle = Ask("Enter a title for your config file", "My Game"),
        };
        AnsiConsole.WriteLine("");

        config.GamePath = Ask("Where are the game's files?", Path.Combine("C:", "Program Files", "MyGame"));
        while (!Directory.Exists(config.GamePath))
        {
            AnsiConsole.MarkupLine($"[red]Path \"{config.GamePath}\" does not exist. [/]");
            config.GamePath = Ask();
        }
        AnsiConsole.WriteLine("");

        config.OutputPath = Ask("Where should extracted files be saved?", Path.Combine(config.GamePath, "extracted"), true);
        while (!PathHelpers.IsDirectoryWritable(config.OutputPath, out _))
        {
            AnsiConsole.MarkupLine($"[red]Directory \"{config.OutputPath}\" is not writable: \"{Markup.Escape(config.OutputPath)}\"[/]");
            config.OutputPath = Ask();
        }
        AnsiConsole.WriteLine("");

        AnsiConsole.MarkupLine("[dim]Attempting to auto-detect Unreal Engine version from the game's .exe file...[/]");
        var detectedEngineVersion = DetectEngineVersion(config.GamePath);
        config.EngineVersion = Ask("Unreal Engine Version", detectedEngineVersion ?? "5.1", !string.IsNullOrEmpty(detectedEngineVersion));

        AnsiConsole.MarkupLine("[blue]Use spaces to separate multiple values for the following prompts.[/]");
        AnsiConsole.MarkupLine("[blue]Some games require additional decryption or mapping files.[/]");

        while (true)
        {
            config.AesKeys = Ask("AES keys", "0x...").Split(" ");
            if (config.AesKeys.Length < 2 || config.AesKeys.All(k => PathHelpers.IsValidAesKey(k))) break;
        }

        config.MappingFiles = Ask(
                "Paths to mapping files",
                Path.Combine(".", "mappings", "MyGame.usmap")
            ).Split(" ");

        config.ExportPaths = Ask(
                "Virtual paths to extract",
                $"{Path.Combine("MyGame", "DataTables", ".*.uasset:json")} {Path.Combine("MyGame", "UI", ".*.uasset:png")}"
            ).Split(" ");

        config.ExcludePaths = Ask(
                "Virtual paths to [bold]exclude[/]",
                Path.Combine("MyGame", "UI", "UserInterface", ".*")
            ).Split(" ");

        var fileName = GetValidFileName(
                Ask("Name your config file", GetValidFileName(config.ConfigTitle ?? "config", ".json"), true), ".json");

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
        AnsiConsole.MarkupLine($"[green]Added {config.ConfigTitle} [dim]({Markup.Escape(fileName)})[/][/]");
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
                .Title("Select a config file or re-run unrealexporter with flags [dim](unrealexporter --help)[/] to begin extraction:")
                .WrapAround()
                .EnableSearch()
                .UseConverter(option => option.Label)
                .AddChoices(options));

        if (selectedOption.Config == null)
        {
            CreateConfig();
            return PromptConfigSelection();
        }

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[green]:check_mark: Loaded config \"{Markup.Escape(selectedOption.Config.ConfigTitle ?? "")}\" [dim]([underline]{Markup.Escape(Path.GetFileName(selectedOption.ConfigPath))}[/])[/][/]");

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

        Add("-t", config.ConfigTitle);
        Add("-p", config.GamePath);
        Add("-o", config.OutputPath);
        AddMany("--aes", config.AesKeys ?? []);
        AddMany("--map", config.MappingFiles ?? []);
        AddMany("--export", config.ExportPaths ?? []);
        AddMany("--exclude", config.ExcludePaths ?? []);

        return result.Trim();
    }

    // Helpers
    public static string GetValidFileName(string fileName, string? extension)
    {
        SlugHelper slugHelper = new SlugHelper();
        var safeFileName = slugHelper.GenerateSlug(fileName);
        if (!string.IsNullOrEmpty(extension) && !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            safeFileName += extension;
        return safeFileName;
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
            AnsiConsole.MarkupLine("[dim]Unable to locate executable file[/]");
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
            AnsiConsole.MarkupLine($"[blue]Found version {version}, but [link=https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs]some games[/] require a custom version[/].");
            return version;
        }

        AnsiConsole.WriteLine("[dim]Executable didn't contain version info[/]");
        return null;
    }
}