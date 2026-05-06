using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;



using Newtonsoft.Json;
using System.Globalization;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;
using CUE4Parse.UE4.Localization;
using System.Collections.Concurrent;
using CUE4Parse.MappingsProvider;
using JSBeautifyLib;
using CUE4Parse.Compression;
using System.Reflection;

public class UnrealExporter
{
    public static void Main(string[] args)
    {

        var provider = new DefaultFileProvider("Z:\\Games\\Tower of Fantasy Global", SearchOption.AllDirectories, true, new VersionContainer((EGame)Enum.Parse(typeof(EGame), $"GAME_UE4_27")));
        provider.Initialize();

        var start = Now();
        // Check .usmap file load time, decide if we using universal keys.txt and /mappings folder

        // 4-5s for 1000 keys
        // ~38s for 1000 .usmap
        for (int i = 0; i < 1000; i++)
        {
            var hexBody = i.ToString("X").PadLeft(64, '0');
            var key = "0x" + hexBody;
            // var key = "0x78F3113C2023D2EBA7C863901A37891E7575C1C96D94338C0B0071A32DBCB2FD";
            // Console.WriteLine(key);
            // provider.SubmitKey(new FGuid(), new FAesKey(key));
            string pathToMapping = $".\\mappings\\Palworld.usmap";
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(pathToMapping);
        }

        AnsiConsole.MarkupLine($"{Elapsed(start, Now())} milliseconds elapsed");

        return;

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

    public static double Now()
    {
        return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }

    public static string Elapsed(double start, double end, int factor = 1)
    {
        return ((end - start) / factor).ToString("0.00");
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