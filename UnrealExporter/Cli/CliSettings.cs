using Spectre.Console.Cli;
using System.ComponentModel;
// TODO: proofread these
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

    [CommandOption("-m|--map")]
    [Description("Name of .usmap file in /mappings")]
    public string MappingFileName { get; set; } = "";

    [CommandOption("-e|--export")]
    [Description("Virtual paths to extract (regex) (repeatable)")]
    public string[] ExportPaths { get; set; } = [];

    [CommandOption("-x|--exclude")]
    [Description("Virtual paths to exclude from extraction (regex) (repeatable)")]
    public string[] ExcludePaths { get; set; } = [];

    [CommandOption("--create-checkpoint")]
    [Description("")]
    public bool CreateNewCheckpoint { get; set; } = false;

    [CommandOption("-c|--checkpoint")]
    [Description("")]
    public string? CheckpointFileName { get; set; } = null;
}