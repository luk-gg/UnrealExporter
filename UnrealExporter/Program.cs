using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

public class UnrealExporter
{
    public static void Main(string[] args)
    {
        var cli = new CommandApp<ExporterCli>();

        cli.Configure(conf =>
        {
            conf.SetApplicationName("unrealexporter");
            conf.SetExceptionHandler((ex, resolver) =>
            {
                AnsiConsole.MarkupLine(ex.Message);
                // AnsiConsole.MarkupLine($"[bold red][[Error]][/] {ex.Message}");
                return -1;
            });
        });

        cli.Run(args);
    }

    public sealed class CliSettings : CommandSettings
    {
        [CommandArgument(0, "[config]")]
        [Description("Config file name")]
        public string ConfigFile { get; set; } = "";

        [CommandOption("-t|--title")]
        [Description("Game title")]
        public string ConfigTitle { get; set; } = "";

        [CommandOption("-p|--path")]
        [Description("Game files directory")]
        public string GamePath { get; set; } = "";

        [CommandOption("-o|--out")]
        [Description("Output directory for extracted files")]
        public string OutputPath { get; set; } = "";

        [CommandOption("-v|--version")]
        [Description("Unreal Engine version")]
        public string EngineVersion { get; set; } = "";

        [CommandOption("--aes")]
        [Description("AES decryption key (repeatable)")]
        public string[] AesKeys { get; set; } = [];

        [CommandOption("--map")]
        [Description("Path to mapping file (.usmap) (repeatable)")]
        public string[] MappingFiles { get; set; } = [];

        [CommandOption("-e|--export")]
        [Description("Virtual paths to extract (regex) (repeatable)")]
        public string[] ExportPaths { get; set; } = [];

        [CommandOption("-x|--exclude")]
        [Description("Virtual paths to exclude from extraction (regex) (repeatable)")]
        public string[] ExcludePaths { get; set; } = [];
    }

    public class ExporterCli : Command<CliSettings>
    {
        public override int Execute(CommandContext context, CliSettings settings, CancellationToken cancellation)
        {
            AnsiConsole.Clear();
            ConfigObj config = new();

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                // Load config if provided
                if (!string.IsNullOrEmpty(settings.ConfigFile))
                {
                    var configFile = ConfigService.GetValidFileName(settings.ConfigFile, ".json");
                    var configPath = Path.Combine(ConfigService.ConfigsDirectory, configFile);
                    config = ConfigService.LoadConfig(configPath);
                    AnsiConsole.MarkupLine($"[green]:check_mark: Loaded config \"{Markup.Escape(config.ConfigTitle)}\" [dim]([underline]{Markup.Escape(configFile)}[/])[/][/]");
                }

                // Append CLI arguments (overwrite config keys if collision)
                AppendSettingsToConfig(settings, config);
            }
            else
            {
                // No args: interactive mode
                AnsiConsole.MarkupLine("[bold hotpink]Welcome to [link=https://github.com/luk-gg/unrealexporter]UnrealExporter[/], a simple data extraction CLI for Unreal Engine games![/]\n");
                config = ConfigService.PromptConfigSelection();
            }

            AnsiConsole.MarkupLine($"[dim]unrealexporter {ConfigService.StringifyConfig(config)}[/]");
            return 0;
        }
    }

    static void AppendSettingsToConfig(CliSettings settings, ConfigObj config)
    {
        var settingProps = typeof(CliSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var configProps = typeof(ConfigObj)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name);

        foreach (var sProp in settingProps)
        {

            if (!configProps.TryGetValue(sProp.Name, out var cProp))
                continue; // property doesn't exist in config

            var value = sProp.GetValue(settings);
            if (value is null)
                continue;

            if (value is string str && string.IsNullOrWhiteSpace(str))
                continue;

            if (value is Array arr && arr.Length == 0)
                continue;

            cProp.SetValue(config, value);
        }
    }
}