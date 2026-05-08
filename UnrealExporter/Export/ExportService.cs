using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Localization;
using CUE4Parse.MappingsProvider;
using CUE4Parse.Utils;
using SkiaSharp;
using Newtonsoft.Json;
using JSBeautifyLib;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Globalization;
using CUE4Parse.FileProvider.Objects;

record ExportMatch(string OutputType);

// TODO: check if DefaultFileProvider actually does reconcile patch paks correctly.
public class ExportService
{
    private static bool createNewCheckpoint { get; } = false;
    private static string language { get; } = "English";

    private static int totalRegexMatches = 0;
    private static int totalExportedFiles = 0;

    public static void InitExporter(ConfigObj config)
    {
        try
        {
            InitService.InitOodle();
            AbstractFileProvider provider = CreateProvider(config);
            ExportFiles(provider, config);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nExiting UnrealExporter.");
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"ERROR: no config files found.");
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

    public static void ExportFiles(AbstractFileProvider provider, ConfigObj config)
    {
        double start = TimeHelpers.Now();
        totalRegexMatches = 0;
        totalExportedFiles = 0;
        // int totalChangedFiles = 0;
        // bool useCheckpoint = false;
        // Dictionary<string, long> loadedCheckpoint = CheckpointService.LoadCheckpoint(config);
        // ConcurrentDictionary<string, long> newCheckpointDict = [];

        Console.WriteLine($"Scanning {provider.Files.Count} files...{Environment.NewLine}");

        Parallel.ForEach(provider.Files, file =>
        {
            var match = GetRegexMatch(file.Value.Path, config);
            if (match == null || IsExcluded(file.Value.Path, config)) return;
            Interlocked.Increment(ref totalRegexMatches);
            string outputPath = Path.Combine(config.OutputPath, Path.GetDirectoryName(file.Value.Path)!, Path.GetFileNameWithoutExtension(file.Value.Path));

            try
            {
                ExportFile(provider, file.Value, outputPath, match.OutputType);
            }
            catch (AggregateException ae)
            {
                Console.WriteLine(ae.Message);
            }
        });

        // Create checkpoint
        // if (createNewCheckpoint) CheckpointService.CreateCheckpoint(newCheckpointDict, config);

        // Log results
        // if (logOutputs && totalExportedFiles > 0 && !createNewCheckpoint) Console.WriteLine();
        // Console.WriteLine($"Scanned {provider.Files.Count} files{(useCheckpoint ? $" ({totalChangedFiles} changed, {provider.Files.Count - totalChangedFiles} unchanged)" : "")}");
        Console.WriteLine($"Regex matched {totalRegexMatches} files {(totalRegexMatches > totalExportedFiles ? $"(skipped {totalRegexMatches - totalExportedFiles} incompatible file types)" : "")}");
        Console.WriteLine($"Exported {totalExportedFiles} files in {TimeHelpers.Elapsed(start, TimeHelpers.Now(), 1000)} seconds");
        Console.WriteLine();
    }

    static ExportMatch? GetRegexMatch(string filePath, ConfigObj config) =>
        config.ExportPaths
            .Select(p => new { Pattern = p[..p.LastIndexOf(':')], OutputType = p.SubstringAfterLast(':') })
            .FirstOrDefault(p => Regex.IsMatch(filePath, "^" + p.Pattern + "$", RegexOptions.IgnoreCase))
            is { } match ? new ExportMatch(match.OutputType.ToLower()) : null;

    static bool IsExcluded(string filePath, ConfigObj config) =>
        config.ExcludePaths.Any(p => Regex.IsMatch(filePath, "^" + p + "$", RegexOptions.IgnoreCase));

    static void ExportFile(AbstractFileProvider provider, GameFile file, string outputPath, string outputType)
    {
        var fileType = file.Path.SubstringAfterLast('.').ToLower();

        switch (fileType)
        {
            case "uasset":
            case "umap":
                ExportUasset(provider, file, outputPath, outputType);
                break;
            case "locres":
                ExportLocres(provider, file, outputPath, outputType);
                break;
            case "js":
                ExportJs(provider, file, outputPath, outputType);
                break;
            case "db":
                ExportDb(provider, file, outputPath, outputType);
                break;
        }
    }

    static void ExportUasset(AbstractFileProvider provider, GameFile file, string outputPath, string outputType)
    {
        var allObjects = provider.LoadPackage(file.Path).GetExports();
        switch (outputType)
        {
            case "png":
                foreach (var obj in allObjects)
                {
                    // Only exports the first object that is a valid bitmap
                    if (obj is UTexture2D texture)
                    {
                        var bitmap = texture.Decode(ETexturePlatform.DesktopMobile);

                        if (bitmap != null)
                        {
                            var encoded = bitmap.Encode(ETextureFormat.Png, false, out _);
                            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                            File.WriteAllBytes(outputPath + ".png", encoded);
                            Interlocked.Increment(ref totalExportedFiles);
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"ERROR: Failed to export {file.Path} (not a valid image bitmap).");
                        }
                    }
                    else
                    {
                        // Not necessarily an error
                        // Console.WriteLine($"ERROR: Failed to export {file.Path} (object is not of type UTexture2D).");
                    }
                }
                break;

            case "json":
                var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath + ".json", json);
                Interlocked.Increment(ref totalExportedFiles);
                break;

            case "uasset":
                // Referenced from FModel's ExportData(). uexp is tied to the uasset file.
                // https://github.com/4sval/FModel/blob/master/FModel/ViewModels/CUE4ParseViewModel.cs#L928
                // Possible refactor to include TryGetValue
                // https://github.com/FabianFG/CUE4Parse/blob/b3550db731303a6f383ca2b4f61737ca870deef2/CUE4Parse/FileProvider/AbstractFileProvider.cs#L562
                if (provider.TrySavePackage(file, out var assets))
                {
                    Parallel.ForEach(assets, kvp =>
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                        File.WriteAllBytes(outputPath + "." + kvp.Key.SubstringAfterLast('.'), kvp.Value);
                        Interlocked.Increment(ref totalExportedFiles);
                    });
                }
                break;

                // case "uexp":
                //     if (provider.TrySavePackage(file, out var assets))
                //     {
                //         Parallel.ForEach(assets, kvp =>
                //         {
                //             lock (new object())
                //             {
                //                 Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                //                 File.WriteAllBytes(outputPath + ".uexp", kvp.Value);
                //             }
                //         });
                //     }
                //     Interlocked.Increment(ref totalExportedFiles);
                //     break;
        }
    }

    static void ExportLocres(AbstractFileProvider provider, GameFile file, string outputPath, string outputType)
    {
        if (outputType == "json" && provider.TryCreateReader(file.Path, out var archive))
        {
            var locres = new FTextLocalizationResource(archive);
            var json = JsonConvert.SerializeObject(locres, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath + ".json", json);
            Interlocked.Increment(ref totalExportedFiles);
        }
    }

    static void ExportJs(AbstractFileProvider provider, GameFile file, string outputPath, string outputType)
    {
        if (outputType == "js" && provider.TrySaveAsset(file.Path, out var data))
        {
            using var stream = new MemoryStream(data) { Position = 0 };
            using var reader = new StreamReader(stream);
            JSBeautifyOptions options = new() { };
            JSBeautify beautifier = new(reader.ReadToEnd(), options);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath + ".js", beautifier.GetResult());
            Interlocked.Increment(ref totalExportedFiles);
        }
    }

    static void ExportDb(AbstractFileProvider provider, GameFile file, string outputPath, string outputType)
    {
        if (outputType == "db" && provider.TrySaveAsset(file.Path, out var data))
        {
            using var stream = new MemoryStream(data) { Position = 0 };
            using var reader = new StreamReader(stream);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath + ".db", data);
            Interlocked.Increment(ref totalExportedFiles);
        }
    }
}