using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

public class ExporterCli : Command<CliSettings>
{
    public override int Execute(CommandContext context, CliSettings settings, CancellationToken cancellation)
    {
        // AnsiConsole.Clear();
        ConfigObj config = new();

        if (Environment.GetCommandLineArgs().Length > 1)
        {
            // Load config if provided
            if (!string.IsNullOrEmpty(settings.ConfigFile))
            {
                var configFile = PathHelpers.GetValidFileName(settings.ConfigFile, ".json");
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

