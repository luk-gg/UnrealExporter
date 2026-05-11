using Spectre.Console.Cli;
using System.ComponentModel;
// Keep these descriptions identical to UnrealExporter/Config/ConfigObj.cs
public sealed class CliSettings : CommandSettings
{
    [CommandArgument(0, "[config]")]
    [Description("Filename of a config to load, e.g. [dim]palworld.json[/]. Flags will override config values, so you can do [dim]unrealexporter palworld.json --out \"D:\\OverrideOutputFolder\"[/].")]
    public string ConfigFile { get; set; } = "";

    [CommandOption("-t|--title")]
    [Description("Display name for this config. Used in the selection menu and log output.")]
    public string ConfigTitle { get; set; } = "";

    [CommandOption("-p|--path")]
    [Description("Absolute path to the game's root directory or paks folder.")]
    public string GamePath { get; set; } = "";

    [CommandOption("-o|--out")]
    [Description("Absolute path to the directory where extracted assets will be saved.")]
    public string OutputPath { get; set; } = "";

    [CommandOption("-v|--version")]
    [Description("Unreal Engine version string, e.g. 5.1. Some games require a custom value from [blue underline link=https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs]EGame.cs[/], e.g. [bold]TowerOfFantasy[/].")]
    public string EngineVersion { get; set; } = "";

    [CommandOption("--aes")]
    [Description("One or more AES-256 decryption keys for encrypted pak files, in [dim]0x[/] hex format (repeatable).")]
    public string[] AesKeys { get; set; } = [];

    [CommandOption("-m|--map")]
    [Description("Filename of a [dim].usmap[/] mappings file placed in the [dim]/mappings[/] folder, e.g. [dim]Palworld.usmap[/]. Required for games that use unversioned properties.")]
    public string MappingFileName { get; set; } = "";

    [CommandOption("-e|--export")]
    [Description("Virtual paths of assets to extract, as regex patterns with a colon-separated output format, e.g. [dim]Pal/Content/Textures/.*.uasset:png[/] (repeatable).")]
    public string[] ExportPaths { get; set; } = [];

    [CommandOption("-x|--exclude")]
    [Description("Virtual paths to exclude from extraction, as regex patterns (repeatable). Useful for skipping files known to cause errors.")]
    public string[] ExcludePaths { get; set; } = [];

    [CommandOption("--create-checkpoint")]
    [Description("If true, saves a checkpoint file after extraction, tracking each asset's file size. Checkpoints can be used to save time by skipping the extraction of unchanged files.")]
    public bool CreateNewCheckpoint { get; set; } = false;

    [CommandOption("-c|--checkpoint")]
    [Description("Filename of a checkpoint file in [dim]/checkpoints[/] to load before extraction, e.g. [dim]palworld_2026-01-15_13-45.json[/]. Use [blue]latest[/] to automatically load the most recent checkpoint for this config.")]
    public string? CheckpointFileName { get; set; } = null;
}