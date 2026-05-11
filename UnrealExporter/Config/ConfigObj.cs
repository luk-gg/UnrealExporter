public sealed class SelectionOption
{
    public string Label { get; set; } = "";
    public ConfigObj? Config { get; set; }
    public string ConfigPath { get; set; } = "";
}
// Keep these descriptions identical to UnrealExporter/Cli/CliSettings.cs
public sealed class ConfigObj
{
    /// <summary>
    /// Display name for this config. Used in the selection menu and log output.
    /// </summary>
    /// <remarks>
    /// When building a new config, this title will be used to look for AES keys and to detect a <a href="https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs">custom UE version</a>.
    /// </remarks>
    public string? ConfigTitle { get; set; }

    /// <summary>
    /// Absolute path to the game's root directory or paks folder.
    /// </summary>
    public string GamePath { get; set; } = "";

    /// <summary>
    /// Absolute path to the directory where extracted assets will be saved.
    /// </summary>
    public string OutputPath { get; set; } = "";

    /// <summary>
    /// Unreal Engine version string, e.g. <c>5.1</c>. Some games require a custom value from
    /// <a href="https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs">EGame.cs</a>,
    /// e.g. <c>TowerOfFantasy</c>.
    /// </summary>
    public string EngineVersion { get; set; } = "";

    /// <summary>
    /// One or more AES-256 decryption keys for encrypted pak files, in <c>0x</c> hex format.
    /// </summary>
    public string[] AesKeys { get; set; } = [];

    /// <summary>
    /// Filename of a <c>.usmap</c> mappings file placed in the <c>/mappings</c> folder, e.g. <c>Palworld.usmap</c>.
    /// Required for games that use unversioned properties.
    /// </summary>
    public string MappingFileName { get; set; } = "";

    /// <summary>
    /// Virtual paths of assets to extract, as regex patterns with a colon-separated output format.
    /// Example: <c>Pal/Content/Textures/.*.uasset:png</c>
    /// </summary>
    public string[] ExportPaths { get; set; } = [];

    /// <summary>
    /// Virtual paths to exclude from extraction, as regex patterns.
    /// Useful for skipping files known to cause errors.
    /// </summary>
    public string[] ExcludePaths { get; set; } = [];

    /// <summary>
    /// If true, saves a checkpoint file after extraction, tracking each asset's file size.
    /// Checkpoints can be used to save time by skipping the extraction of unchanged files.
    /// </summary>
    public bool CreateNewCheckpoint { get; set; } = false;

    /// <summary>
    /// Name of a checkpoint file in <c>/checkpoints</c> to load before extraction, e.g. <c>palworld_2026-01-15_13-45.json</c>.
    /// Use <c>latest</c> to automatically load the most recent checkpoint for this config.
    /// </summary>
    public string? CheckpointFileName { get; set; } = null;
}