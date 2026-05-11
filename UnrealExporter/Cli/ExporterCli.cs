using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

public class ExporterCli : Command<CliSettings>
{
    public override int Execute(CommandContext context, CliSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        ConfigObj config = new();

        if (Environment.GetCommandLineArgs().Length > 1)
        {
            // Load config if provided
            if (!string.IsNullOrEmpty(settings.ConfigFile))
            {
                var configPath = Path.Combine(ConfigService.ConfigsDirectory, PathHelpers.ForceExtension(settings.ConfigFile, ".json"));
                config = ConfigService.LoadConfig(configPath);
            }
            
            // Append CLI arguments (overwrite config keys if collision)
            AppendSettingsToConfig(settings, config);
        }
        else
        {
            // No args: interactive mode
            AnsiConsole.MarkupLine("[bold hotpink]Welcome to [underline link=https://github.com/luk-gg/unrealexporter]UnrealExporter[/], a simple data extraction CLI for Unreal Engine games![/]\n");
            config = ConfigService.PromptConfigSelection();
        }

        // TODO: check if valid config/sufficient args, files exist, etc.

#if DEBUG
        AnsiConsole.MarkupLine($"[dim]unrealexporter {ConfigService.StringifyConfig(config)}[/]\n");
#endif

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[dim]Setting[/]").NoWrap())
            .AddColumn("[dim]Value[/]");

        table.AddRow("Title", Markup.Escape(config.ConfigTitle ?? ""));
        table.AddRow("Game path", Markup.Escape(config.GamePath));
        table.AddRow("Output path", Markup.Escape(config.OutputPath));
        table.AddRow("Engine version", Markup.Escape(config.EngineVersion));
        table.AddRow($"AES keys ({config.AesKeys.Length})", config.AesKeys.Length > 0 ? string.Join("\n", config.AesKeys.Select(k => Markup.Escape(k))) : "[dim]none[/]");
        table.AddRow("Mapping file", string.IsNullOrEmpty(config.MappingFileName) ? "[dim]none[/]" : PathHelpers.ForceExtension(config.MappingFileName, ".usmap"));
        table.AddRow($"Export paths ({config.ExportPaths.Length})", config.ExportPaths.Length > 0 ? string.Join("\n", config.ExportPaths.Select(p => Markup.Escape(p))) : "[dim]none[/]");
        table.AddRow($"Exclude paths ({config.ExcludePaths.Length})", config.ExcludePaths.Length > 0 ? string.Join("\n", config.ExcludePaths.Select(p => Markup.Escape(p))) : "[dim]none[/]");
        table.AddRow("Checkpoint file",
            string.IsNullOrEmpty(config.CheckpointFileName)
            ? "[dim]none[/]"
            : config.CheckpointFileName == "latest"
                ? $"[blue]{config.CheckpointFileName}[/]"
                : PathHelpers.ForceExtension(config.CheckpointFileName, ".json"));
        table.AddRow("Create new checkpoint", config.CreateNewCheckpoint ? "true" : "[dim]false[/]");

        AnsiConsole.Write(table);

        ExportService.InitExporter(config);

        return 0;
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
            if (value is null || value is false)
                continue;

            if (value is string str && string.IsNullOrWhiteSpace(str))
                continue;

            if (value is Array arr && arr.Length == 0)
                continue;

            cProp.SetValue(config, value);
        }
    }
}

