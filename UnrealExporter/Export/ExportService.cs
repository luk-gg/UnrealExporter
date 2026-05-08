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

public class ExportService
{
    private static bool logOutputs { get; } = true;
    private static bool createNewCheckpoint { get; } = false;

    private static string language { get; } = "English";

    private static int totalChangedFiles = 0;
    private static int totalRegexMatches = 0;
    private static int totalExportedFiles = 0;

    private static bool useCheckpoint = false;

    public static EGame GetGameVersion(string versionString)
    {
        string version;

        // "4.27"
        if (versionString.Contains('.'))
        {
            version = $"UE{versionString.Replace('.', '_')}";
        }
        // "tower of fantasy"
        else if (versionString.Split(" ").Length > 1)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            version = textInfo.ToTitleCase(versionString).Replace(" ", "");
        }
        // "TowerOfFantasy"
        else
        {
            version = versionString;
        }

        EGame selectedVersion = (EGame)Enum.Parse(typeof(EGame), $"GAME_{version}");

        return selectedVersion;
    }

    public static AbstractFileProvider CreateProvider(ConfigObj config, EGame selectedVersion)
    {
        // Load CUE4Parse
        // TODO: Ignore mods (all folders within /Content/Paks)
        var provider = new DefaultFileProvider(
            config.GamePath,
            SearchOption.AllDirectories,
            new VersionContainer(selectedVersion),
            StringComparer.OrdinalIgnoreCase);

        provider.Initialize();

        // Decrypt
        if (config.AesKeys.Length > 0)
        {
            foreach (var key in config.AesKeys)
                provider.SubmitKey(new FGuid(), new FAesKey(key));
        }
        else
        {
            provider.SubmitKey(new FGuid(), new FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));
        }

        // Set locale if provided, otherwise English
        if (language?.Length > 0)
        {
            ELanguage selectedLang = (ELanguage)Enum.Parse(typeof(ELanguage), language);
            provider.LoadLocalization(selectedLang);
        }
        else
        {
            provider.LoadLocalization(ELanguage.English);
        }

        // TEMP (need to fix patchProvider for utoc/ucas support). For now it's not guaranteed that the patch paks will be reconciled correctly.
        string pathToMapping = $"{AppContext.BaseDirectory}\\mappings\\{config.ConfigTitle}.usmap";
        if (File.Exists(pathToMapping))
        {
            Console.WriteLine($"Using mapping file: {pathToMapping}");
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(pathToMapping);
        }

        // Load files into PatchFileProvider so the patch uassets override original uassets
        // var patchProvider = new PatchFileProvider();
        // patchProvider.Load(provider);

        return provider;
    }

    public static void Export(AbstractFileProvider provider, ConfigObj config, double start)
    {
        // Load checkpoint if provided
        useCheckpoint = false;
        // Dictionary<string, long> loadedCheckpoint = CheckpointService.LoadCheckpoint(config);
        // ConcurrentDictionary<string, long> newCheckpointDict = [];

        Console.WriteLine($"Scanning {provider.Files.Count} files...{Environment.NewLine}");

        // Loop through all files and export the ones that match any of the config.export paths (converted to regex)
        Parallel.ForEach(provider.Files, file =>
        {
            // "Hotta/Content/Resources/UI/Activity/Activity/DT_Activityquest_Balance.uasset"
            // file.Value.Path

            // "Hotta\Content\Resources\UI\Activity\Activity"
            var fileDir = Path.GetDirectoryName(file.Value.Path);

            // "DT_Activityquest_Balance"
            var fileName = Path.GetFileNameWithoutExtension(file.Value.Path);

            // "Hotta\Content\Resources\UI\Activity\Activity\DT_Activityquest_Balance"
            var filePath = Path.Combine(fileDir, fileName);

            // "D:\UnrealExporter\output\Hotta\Content\Resources\UI\Activity\Activity"
            var outputDir = Path.Combine(config.OutputPath, fileDir);

            // "D:\UnrealExporter\output\Hotta\Content\Resources\UI\Activity\Activity\DT_Activityquest_Balance"
            var outputPath = Path.Combine(outputDir, fileName);

            string regexMatch =
                config.ExportPaths
                .FirstOrDefault(path => new Regex("^" + path[..path.LastIndexOf(':')] + "$", RegexOptions.IgnoreCase)
                .IsMatch(file.Value.Path), "");

            bool isExclude =
                config.ExcludePaths
                .Any(path => new Regex("^" + path + "$", RegexOptions.IgnoreCase)
                .IsMatch(file.Value.Path));

            bool isChanged = true;

            // If checkpoint is specified, skip files whose sizes are the same as in the checkpoint
            // if (useCheckpoint && loadedCheckpoint.TryGetValue(file.Value.Path, out long fileSize))
            // {
            //     isChanged = fileSize != file.Value.Size;
            //     if (isChanged) Interlocked.Increment(ref totalChangedFiles);
            // }

            // if (createNewCheckpoint) newCheckpointDict.TryAdd(file.Value.Path, file.Value.Size);

            if (regexMatch.Length > 0 && !isExclude && isChanged)
            {
                // "uasset"
                var fileType = file.Value.Path.SubstringAfterLast('.').ToLower();

                // "json" etc.
                var outputType = regexMatch.SubstringAfterLast(':').ToLower();

                try
                {
                    switch (fileType)
                    {
                        // Referencing CUE4ParseViewModel.cs from Fmodel source code
                        case "uasset":
                        case "umap":
                            {
                                var allObjects = provider.LoadPackage(file.Value.Path).GetExports();

                                if (outputType == "png")
                                {
                                    foreach (var obj in allObjects)
                                    {
                                        // Only exports the first object that is a valid bitmap
                                        if (obj is UTexture2D texture)
                                        {
                                            var bitmap = texture.Decode(ETexturePlatform.DesktopMobile);

                                            if (bitmap != null)
                                            {
                                                if (logOutputs) Console.WriteLine("=> " + outputPath + ".png");
                                                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                                                var encoded = bitmap.Encode(ETextureFormat.Png, false, out _);
                                                File.WriteAllBytes(outputPath + ".png", encoded);

                                                Interlocked.Increment(ref totalExportedFiles);
                                                break;
                                            }
                                            else
                                            {
                                                Console.WriteLine($"ERROR: Failed to export {file.Value.Path} (not a valid image bitmap).");
                                            }
                                        }
                                        else
                                        {
                                            // Not necessarily an error
                                            // Console.WriteLine($"ERROR: Failed to export {file.Value.Path} (object is not of type UTexture2D).");
                                        }
                                    }
                                }

                                else if (outputType == "json")
                                {
                                    // Serialize to JSON, then write to file
                                    if (logOutputs) Console.WriteLine("=> " + outputPath + ".json");
                                    var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
                                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                    File.WriteAllText(outputPath + ".json", json);
                                    Interlocked.Increment(ref totalExportedFiles);
                                }

                                // Referenced from FModel's ExportData(). uexp is tied to the uasset file.
                                // https://github.com/4sval/FModel/blob/master/FModel/ViewModels/CUE4ParseViewModel.cs#L928
                                // Possible refactor to include TryGetValue
                                // https://github.com/FabianFG/CUE4Parse/blob/b3550db731303a6f383ca2b4f61737ca870deef2/CUE4Parse/FileProvider/AbstractFileProvider.cs#L562
                                else if (outputType == "uasset")
                                {
                                    if (provider.TrySavePackage(file.Value, out var assets))
                                    {
                                        Parallel.ForEach(assets, kvp =>
                                        {
                                            if (logOutputs) Console.WriteLine("=> " + outputPath + "." + kvp.Key.SubstringAfterLast('.'));
                                            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                            File.WriteAllBytes(outputPath + "." + kvp.Key.SubstringAfterLast('.'), kvp.Value);
                                            Interlocked.Increment(ref totalExportedFiles);
                                        });
                                    }
                                }

                                // else if (outputType == "uexp")
                                // {
                                //     if (logOutputs) Console.WriteLine("=> " + outputPath + ".uexp");
                                //     if (provider.TrySavePackage(file.Value, out var assets))
                                //     {
                                //         Parallel.ForEach(assets, kvp =>
                                //         {
                                //             lock (new object())
                                //             {
                                //                 if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                //                 File.WriteAllBytes(outputPath + ".uexp", kvp.Value);
                                //             }
                                //         });
                                //     }
                                //     Interlocked.Increment(ref totalExportedFiles);
                                // }

                                break;
                            }
                        case "locres":
                            {
                                if (outputType == "json" && provider.TryCreateReader(file.Value.Path, out var archive))
                                {
                                    if (logOutputs) Console.WriteLine("=> " + outputPath + ".json");
                                    var locres = new FTextLocalizationResource(archive);
                                    var json = JsonConvert.SerializeObject(locres, Formatting.Indented);
                                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                    File.WriteAllText(outputPath + ".json", json);
                                    Interlocked.Increment(ref totalExportedFiles);
                                }
                                break;
                            }
                        case "js":
                            {
                                if (outputType == fileType && provider.TrySaveAsset(file.Value.Path, out var data))
                                {
                                    if (logOutputs) Console.WriteLine("=> " + outputPath + "." + outputType);
                                    using var stream = new MemoryStream(data) { Position = 0 };
                                    using var reader = new StreamReader(stream);
                                    JSBeautifyOptions options = new() { };
                                    JSBeautify beautifier = new(reader.ReadToEnd(), options);
                                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                    File.WriteAllText(outputPath + ".js", beautifier.GetResult());
                                    Interlocked.Increment(ref totalExportedFiles);
                                }
                                break;
                            }
                        case "db":
                            {
                                if (outputType == fileType && provider.TrySaveAsset(file.Value.Path, out var data))
                                {
                                    if (logOutputs) Console.WriteLine("=> " + outputPath + "." + outputType);
                                    using var stream = new MemoryStream(data) { Position = 0 };
                                    using var reader = new StreamReader(stream);
                                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                    File.WriteAllBytes(outputPath + ".db", data);
                                    Interlocked.Increment(ref totalExportedFiles);
                                }
                                break;
                            }
                    }
                }
                catch (AggregateException ae)
                {
                    Console.WriteLine(ae.Message);
                    // Console.WriteLine($"ERROR: File cannot be opened: {file.Value.Path}. Possible issues include incorrect UE version in config.json, missing mapping file, or this file type is not supported.");
                }

                Interlocked.Increment(ref totalRegexMatches);
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
}