using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Textures;
using Newtonsoft.Json;
using JSBeautifyLib;
using System.Text.RegularExpressions;
using Spectre.Console;
using CUE4Parse.Compression;

record ExportMatch(string OutputType);

// TODO: check if DefaultFileProvider actually does reconcile patch paks correctly.
public class ExportService
{
    private static string outputBaseDir = "";

    private static int totalRegexMatches = 0;

    private static int totalExportedFiles = 0;

    // public static async ValueTask InitZlib()
    // {
    //     var zlibPath = Path.Combine(".", ZlibHelper.DLL_NAME);
    //     if (!File.Exists(zlibPath))
    //     {
    //         await ZlibHelper.DownloadDllAsync(zlibPath);
    //     }

    //     ZlibHelper.Initialize(zlibPath);
    // }

    readonly static object _refreshLock = new();

    public static async Task InitExporter(ConfigObj config)
    {
        try
        {
            await OodleHelper.InitializeAsync();
            AbstractFileProvider provider = CreateProvider(config);

            double start = TimeHelpers.Now();
            totalRegexMatches = 0;
            totalExportedFiles = 0;
            outputBaseDir = config.OutputPath;

            var totalFiles = provider.Files.Count;
            int processed = 0;
            string currentFile = "";

            AnsiConsole.MarkupLine($"\nFound {totalFiles:N0} files.\n");

            var table = new Table()
                .HideHeaders()
                .Border(TableBorder.None)
                .AddColumn(new TableColumn("").NoWrap());

            AnsiConsole.Live(table)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Start(ctx =>
                {
                    void Refresh()
                    {
                        lock (_refreshLock)
                        {
                            // Refresh every 1%
                            if (processed % (totalFiles / 100) == 0 || processed == totalFiles)
                            {
                                var pct = (double)processed / totalFiles;
                                var filled = (int)(pct * 40);
                                var bar = $"[green]{new string('━', filled)}[/][grey]{new string('━', 40 - filled)}[/]";
                                var stats = $"Matched [blue]{totalRegexMatches}[/]  Exported [blue]{totalExportedFiles}[/]  Elapsed [blue]{TimeHelpers.TimeSince(start)}[/]";
                                if (processed == totalFiles) stats = "[dim]" + stats + "[/]";

                                table.Rows.Clear();
                                table.AddRow(new Markup($"[dim]{Markup.Escape(currentFile)}[/]"));
                                table.AddRow(new Markup($"{bar} [green]{Math.Round(pct * 100)}%[/]\n"));
                                table.AddRow(new Markup(stats));
                                ctx.Refresh();
                            }
                        }
                    }

                    Parallel.ForEach(provider.Files, file =>
                    {
                        try
                        {
                            var match = GetRegexMatch(file.Value.Path, config);
                            if (match != null && !IsExcluded(file.Value.Path, config))
                            {
                                currentFile = file.Value.Path;
                                Interlocked.Increment(ref totalRegexMatches);

                                try { ExportFile(provider, file.Value, match.OutputType); }
                                catch (AggregateException ae) { Console.WriteLine(ae.Message); }
                            }

                            Interlocked.Increment(ref processed);
                            Refresh();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"ERROR: {file.Value.Path} — {e.Message}");
                        }

                    });
                });

            AnsiConsole.MarkupLine($"\n[blue]:check_mark: UnrealExporter finished in {TimeHelpers.TimeSince(start)}[/]");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nExiting UnrealExporter.");
        }
    }

    public static EGame GetGameVersion(string engineVersion)
    {
        string version = engineVersion;

        if (engineVersion.Contains('.'))
            version = $"UE{engineVersion.Replace('.', '_')}";

        EGame selectedVersion = (EGame)Enum.Parse(typeof(EGame), $"GAME_{version}");

        return selectedVersion;
    }

    public static AbstractFileProvider CreateProvider(ConfigObj config)
    {
        // TODO: Ignore mods (all folders within /Content/Paks)
        var provider = new DefaultFileProvider(
            config.GamePath,
            SearchOption.AllDirectories,
            new VersionContainer(GetGameVersion(config.EngineVersion)),
            StringComparer.OrdinalIgnoreCase);

        provider.Initialize();

        foreach (var key in config.AesKeys.Length > 0 ? config.AesKeys : ["0x0000000000000000000000000000000000000000000000000000000000000000"])
            provider.SubmitKey(new FGuid(), new FAesKey(key));

        // provider.LoadLocalization(ELanguage.English);

        string usmapPath = $"{AppContext.BaseDirectory}\\mappings\\{config.MappingFile}";
        if (File.Exists(usmapPath)) provider.MappingsContainer = new FileUsmapTypeMappingsProvider(usmapPath);

        // Load files into PatchFileProvider so the patch uassets override original uassets
        // var patchProvider = new PatchFileProvider();
        // patchProvider.Load(provider);

        return provider;
    }

    static ExportMatch? GetRegexMatch(string filePath, ConfigObj config) =>
        config.ExportPaths
            .Where(p => p.Contains(':'))
            .Select(p => new { Pattern = p[..p.LastIndexOf(':')], OutputType = p.SubstringAfterLast(':') })
            .FirstOrDefault(p => Regex.IsMatch(filePath, "^" + p.Pattern + "$", RegexOptions.IgnoreCase))
            is { } match ? new ExportMatch(match.OutputType.ToLower()) : null;

    static bool IsExcluded(string filePath, ConfigObj config) =>
        config.ExcludePaths.Any(p => Regex.IsMatch(filePath, "^" + p + "$", RegexOptions.IgnoreCase));

    static void ExportFile(AbstractFileProvider provider, GameFile file, string outputType)
    {
        var originalType = file.Path.SubstringAfterLast('.').ToLower();

        switch (originalType)
        {
            case "uasset":
            case "umap":
                {
                    var allObjects = provider.LoadPackageObjects(file.Path);

                    if (outputType == "json")
                        SerializeAndExportJson(allObjects, file);
                    else if (outputType == "png")
                        ExportPng(allObjects, file);

                    break;
                }

            case "locres":
                {
                    if (outputType == "json" && provider.TryCreateReader(file.Path, out var archive))
                        SerializeAndExportJson(new FTextLocalizationResource(archive), file);
                    break;
                }

            case "locmeta":
                {
                    if (outputType == "json" && provider.TryCreateReader(file.Path, out var archive))
                        SerializeAndExportJson(new FTextLocalizationMetaDataResource(archive), file);
                    break;
                }

            case "upluginmanifest":
            case "uproject":
            case "manifest":
            case "uplugin":
            case "archive":
            case "vmodule":
            case "verse":
            case "html":
            case "json":
            case "ini":
            case "txt":
            case "log":
            case "bat":
            case "dat":
            case "cfg":
            case "ide":
            case "ipl":
            case "zon":
            case "xml":
            case "css":
            case "csv":
            case "pem":
            case "tps":
            case "lua":
            case "js":
            case "po":
            case "h":
                {
                    if (provider.TrySaveAsset(file.Path, out var data))
                        ExportRaw(data, file);
                    break;
                }
        }
    }

    static void ExportPng(IEnumerable<UObject> allObjects, GameFile file)
    {

        foreach (var obj in allObjects)
        {
            if (obj is UTexture2D texture)
            {
                var bitmap = texture.Decode(ETexturePlatform.DesktopMobile);
                if (bitmap == null) continue;

                string outputFilePath = Path.Combine(outputBaseDir, Path.ChangeExtension(file.Path, ".png"));
                var encoded = bitmap.Encode(ETextureFormat.Png, false, out _);

                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
                File.WriteAllBytes(outputFilePath, encoded);
                Interlocked.Increment(ref totalExportedFiles);
                return;
            }
        }
    }

    static void SerializeAndExportJson(object data, GameFile file)
    {
        string outputFilePath = Path.Combine(outputBaseDir, Path.ChangeExtension(file.Path, ".json"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
        File.WriteAllText(outputFilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        Interlocked.Increment(ref totalExportedFiles);
    }

    static void ExportRaw(byte[] data, GameFile file)
    {
        string ext = Path.GetExtension(file.Path);
        string outputFilePath = Path.Combine(outputBaseDir, file.Path);

        if (ext == ".js")
        {
            using var reader = new StreamReader(new MemoryStream(data));
            JSBeautify beautifier = new(reader.ReadToEnd(), new());
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
            File.WriteAllText(outputFilePath, beautifier.GetResult());
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
            File.WriteAllBytes(outputFilePath, data);
        }

        Interlocked.Increment(ref totalExportedFiles);
    }
}