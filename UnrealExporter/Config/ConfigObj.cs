public sealed class SelectionOption
{
    public string Label { get; set; } = "";
    public ConfigObj? Config { get; set; }
    public string ConfigPath { get; set; } = "";
}

public sealed class ConfigObj
{
    /// <summary>
    /// A name of the config file. Used when listing configs or in error messages.
    /// </summary>
    /// <remarks>
    /// If this title matches the game title, a <a href="https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs">custom UE version</a> may be automatically detected.
    /// </remarks>
    public string? ConfigTitle { get; set; }

    /// <summary>
    /// A path to the directory containing the game's files.
    /// </summary>
    public string GamePath { get; set; } = "";

    /// <summary>
    /// A path to a directory that will contain extracted assets.
    /// </summary>
    public string OutputPath { get; set; } = "";

    /// <summary>
    /// The Unreal Engine version used to compile the game.
    /// </summary>
    /// <remarks>
    /// Often found in the game's Win64-Shipping.exe file details. Some games use a <a href="https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs">custom offset</a>.
    /// </remarks>
    public string EngineVersion { get; set; } = "";

    /// <summary>
    /// A list of AES-256 encryption keys to load.
    /// </summary>
    public string[] AesKeys { get; set; } = [];

    /// <summary>
    /// An absolute path to a <c>.usmap</c> file to load.
    /// </summary>
    public string MappingFile { get; set; } = "";

    /// <summary>
    /// A list of virtual file paths to assets to be extracted.
    /// </summary>
    /// <remarks>
    /// Specify the desired file extension with a colon, such as <c>MyGame\DataTables\.*.uasset:json</c>, <c>MyGame\UI\.*.uasset:png</c>.
    /// </remarks>
    public string[] ExportPaths { get; set; } = [];

    /// <summary>
    /// A list of virtual file paths to assets to be <b>excluded</b> from extraction, useful for avoiding files that crash CUE4Parse.
    /// </summary>
    public string[] ExcludePaths { get; set; } = [];
}