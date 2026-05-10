using CUE4Parse.Compression;
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
using CUE4Parse_Conversion.Textures.BC;
using JSBeautifyLib;
using Newtonsoft.Json;
using SkiaSharp;
using Spectre.Console;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

record ExportMatch(string OutputType);

public class ExportService
{
    private static string outputBaseDir = "";

    private static int totalExportedFiles = 0;

    readonly static object _refreshLock = new();

    private static ConcurrentBag<string> errors = [];

    public static void InitExporter(ConfigObj config)
    {
        try
        {
            OodleHelper.Initialize(Path.Combine(AppContext.BaseDirectory, OodleHelper.OODLE_NAME_CURRENT));
            ZlibHelper.Initialize(Path.Combine(AppContext.BaseDirectory, ZlibHelper.DLL_NAME));
            DetexHelper.LoadDll(Path.Combine(AppContext.BaseDirectory, DetexHelper.DLL_NAME));
            DetexHelper.Initialize(Path.Combine(AppContext.BaseDirectory, DetexHelper.DLL_NAME));
            AbstractFileProvider provider = CreateProvider(config);

            double start = TimeHelpers.Now();
            int matchedFiles = 0;
            totalExportedFiles = 0;
            errors = [];
            outputBaseDir = config.OutputPath;

            var totalFiles = provider.Files.Count;
            int processedFiles = 0;
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
                            if (processedFiles > 0 && (processedFiles % (totalFiles / 100) == 0 || processedFiles >= totalFiles - (totalFiles / 100)))
                            {
                                var pct = (double)processedFiles / totalFiles;
                                var filled = (int)(pct * 40);
                                var bar = $"[green]{new string('━', filled)}[/][grey]{new string('━', 40 - filled)}[/]";
                                var stats = $"Matched [blue]{matchedFiles}[/]  Exported [blue]{totalExportedFiles}[/]  Elapsed [blue]{TimeHelpers.TimeSince(start)}[/]";
                                if (processedFiles == totalFiles) stats = "[dim]" + stats + "[/]";

                                table.Rows.Clear();
                                table.AddRow(new Markup($"[dim]{Markup.Escape(currentFile)}[/]"));
                                table.AddRow(new Markup($"{bar} [green]{Math.Round(pct * 100)}%[/]\n"));
                                table.AddRow(new Markup(stats));
                                ctx.Refresh();
                            }
                        }
                    }

                    // Main export loop
                    Parallel.ForEach(provider.Files, file =>
                    {
                        try
                        {
                            var matches = GetRegexMatches(file.Value.Path, config).ToList();

                            if (matches.Count > 0 && !IsExcluded(file.Value.Path, config))
                            {
                                Interlocked.Increment(ref matchedFiles);

                                var ext = Path.GetExtension(file.Value.Path).TrimStart('.').ToLower();

                                // Only load package objects once even if the file is exported in multiple formats (i.e. jpg + png)
                                IEnumerable<UObject>? packageObjects = (ext is "uasset" or "umap") && matches.Count > 1
                                    ? provider.LoadPackage(file.Value.Path).GetExports()
                                    : null;

                                foreach (var match in matches)
                                    ExportFile(provider, file.Value, ext, match.OutputType, packageObjects);
                            }

                            Interlocked.Increment(ref processedFiles);
                            Refresh();
                        }
                        catch (Exception e) { errors.Add($"{file.Value.Path} — {e.Message}"); }
                    });
                });

            string emoji = "[green]:check_mark:[/]";
            string errorTotal = "[green]0[/] errors";

            if (!errors.IsEmpty)
            {
                Console.WriteLine();
                emoji = "[yellow]:warning:[/]";
                errorTotal = $"[red]{errors.Count}[/] errors (see above)";
                foreach (var error in errors)
                    AnsiConsole.MarkupLine($"[dim red]{Markup.Escape(error)}[/]");
            }

            AnsiConsole.MarkupLine($"\n{emoji} UnrealExporter finished in [blue]{TimeHelpers.TimeSince(start)}[/] with {errorTotal} [dim](outputted to [underline link={new Uri(outputBaseDir).AbsoluteUri}]{outputBaseDir}[/])[/]");

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

        return provider;
    }

    static IEnumerable<ExportMatch> GetRegexMatches(string filePath, ConfigObj config) =>
        config.ExportPaths
            .Where(p => p.Contains(':'))
            .Select(p => new { Pattern = p[..p.LastIndexOf(':')], OutputType = p.SubstringAfterLast(':') })
            .Where(p => Regex.IsMatch(filePath, "^" + p.Pattern + "$", RegexOptions.IgnoreCase))
            .Select(p => new ExportMatch(p.OutputType.ToLower()));

    static bool IsExcluded(string filePath, ConfigObj config) =>
        config.ExcludePaths.Any(p => Regex.IsMatch(filePath, "^" + p + "$", RegexOptions.IgnoreCase));

    // https://github.com/4sval/FModel/blob/dev/FModel/ViewModels/CUE4ParseViewModel.cs#L635
    static void ExportFile(AbstractFileProvider provider, GameFile file, string ext, string outputType, IEnumerable<UObject>? packageObjects = null)
    {
        switch (ext)
        {
            case "uasset":
            case "umap":
                {
                    packageObjects ??= provider.LoadPackage(file.Path).GetExports();
                    switch (outputType)
                    {
                        case "json":
                            SerializeAndExportJson(packageObjects, file);
                            break;
                        case "png":
                        case "jpg":
                        case "jpeg":
                        case "tga":
                        case "webp":
                        case "hdr": // untested but fallback to png works
                            ExportImage(packageObjects, file, outputType);
                            break;
                    }

                    break;
                }

            case "locres":
                {
                    if (outputType == "json" && provider.TryCreateReader(file.Path, out var archive))
                        SerializeAndExportJson(new FTextLocalizationResource(archive), file);
                    break;
                }

            case "locmeta": // untested
                {
                    if (outputType == "json" && provider.TryCreateReader(file.Path, out var archive))
                        SerializeAndExportJson(new FTextLocalizationMetaDataResource(archive), file);
                    break;
                }

            case "upluginmanifest": // untested
            case "uproject": // untested
            case "manifest": // untested
            case "uplugin": // untested
            case "archive": // untested
            case "vmodule": // untested
            case "verse": // untested
            case "html": // untested
            case "json": // untested
            case "ini": // untested
            case "txt": // untested
            case "log": // untested
            case "bat": // untested
            case "dat": // untested
            case "cfg": // untested
            case "ide": // untested
            case "ipl": // untested
            case "zon": // untested
            case "xml": // untested
            case "css": // untested
            case "csv": // untested
            case "pem": // untested
            case "tps": // untested
            case "lua": // untested
            case "js":
            case "po": // untested
            case "h": // untested
            // Fonts may need to be renamed to change file extension
            case "ufont": // untested
            case "otf": // untested
            case "ttf": // untested
                {
                    if (provider.TrySaveAsset(file.Path, out var data))
                        ExportRaw(data, file);
                    break;
                }
        }
    }

    static void ExportImage(IEnumerable<UObject> packageObjects, GameFile file, string outputType)
    {
        foreach (var obj in packageObjects)
        {
            if (obj is UTexture2D texture)
            {
                var bitmap = texture.Decode(ETexturePlatform.DesktopMobile);
                if (bitmap == null) continue;

                string ext = outputType;
                byte[] encoded;

                if (outputType == "hdr")
                {
                    encoded = bitmap.Encode(ETextureFormat.Png, true, out var hdrExt);
                    ext = hdrExt; // will be "hdr" for HDR textures, "png" for non-HDR
                }
                else
                {
                    encoded = outputType switch
                    {
                        "png" => bitmap.Encode(ETextureFormat.Png, false, out _),
                        "jpg" or "jpeg" => bitmap.Encode(ETextureFormat.Jpeg, false, out _),
                        "tga" => bitmap.Encode(ETextureFormat.Tga, false, out _),
                        "webp" => EncodeWebp(bitmap),
                        _ => bitmap.Encode(ETextureFormat.Png, false, out _)
                    };
                }

                string outputFilePath = Path.Combine(outputBaseDir, Path.ChangeExtension(file.Path, $".{ext}"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
                File.WriteAllBytes(outputFilePath, encoded);
                Interlocked.Increment(ref totalExportedFiles);
                return;
            }
        }
    }

    static byte[] EncodeWebp(CTexture bitmap)
    {
        using var skBitmap = bitmap.ToSkBitmap();
        using var data = skBitmap.Encode(SKEncodedImageFormat.Webp, 100);
        return data.ToArray();
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