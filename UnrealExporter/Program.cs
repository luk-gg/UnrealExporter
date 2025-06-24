using Newtonsoft.Json;
using System.Globalization;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;
using CUE4Parse.UE4.Localization;
using System.Collections.Concurrent;
using CUE4Parse.MappingsProvider;
using JSBeautifyLib;
using System.Runtime.InteropServices;
using OodleDotNet;
using CUE4Parse.Compression;

namespace UnrealExporter;

// TODO: CLI selection for selecting a checkpoint "Checkpoint files found. Select the one you would like to use."
// TODO: if outputType is unspecified, default to fileType

public class UnrealExporter
{
    private static int totalChangedFiles = 0;
    private static int totalRegexMatches = 0;
    private static int totalExportedFiles = 0;
    private static bool useCheckpoint = false;

    public static void Main(string[] args)
    {
        double trueStart = Now();

        // Initialize packages (from FModel's InitOodle())
        InitOodle();
        InitZlib();

        try
        {
            List<ConfigObj> configs = LoadAllConfigs(args);

            foreach (ConfigObj config in configs)
            {
                double start = Now();
                totalChangedFiles = 0;
                totalRegexMatches = 0;
                totalExportedFiles = 0;

                EGame selectedVersion = GetGameVersion(config.Version);
                Console.WriteLine($"Config: {config.ConfigFileName} (object #{config.ConfigObjectIndex + 1})");
                Console.WriteLine($"Game: {config.GameTitle}");
                Console.WriteLine($"Version: {selectedVersion}");
                Console.WriteLine($"Locale: {config.Lang}");
                Console.WriteLine($"Paks: {config.PaksDir}");
                Console.WriteLine($"Output: {config.OutputDir}");
                Console.WriteLine($"AES key: {config.Aes}");
                Console.WriteLine($"Log outputs: {config.LogOutputs}");
                Console.WriteLine($"Keep directory structure: {config.KeepDirectoryStructure}");
                Console.WriteLine($"Create new checkpoint: {config.CreateNewCheckpoint}");

                // Load CUE4Parse and export files
                AbstractFileProvider provider = CreateProvider(config, selectedVersion);
                Export(provider, config, start);
            }

            Console.WriteLine($"UnrealExporter finished in {Elapsed(trueStart, Now(), 1000)} seconds");
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

    public static async ValueTask InitOodle()
    {
        var oodlePath = Path.Combine(".", OodleHelper.OODLE_DLL_NAME);
        if (File.Exists(OodleHelper.OODLE_DLL_NAME))
        {
            File.Move(OodleHelper.OODLE_DLL_NAME, oodlePath, true);
        }
        else if (!File.Exists(oodlePath))
        {
            await OodleHelper.DownloadOodleDllAsync(oodlePath);
        }

        OodleHelper.Initialize(oodlePath);
    }

    public static async ValueTask InitZlib()
    {
        var zlibPath = Path.Combine(".", ZlibHelper.DLL_NAME);
        if (!File.Exists(zlibPath))
        {
            await ZlibHelper.DownloadDllAsync(zlibPath);
        }

        ZlibHelper.Initialize(zlibPath);
    }

    public static List<ConfigObj>? LoadConfigFile(string path)
    {
        try
        {
            string jsonString = File.ReadAllText(path);
            List<ConfigObj> configObjs = JsonConvert.DeserializeObject<List<ConfigObj>>(jsonString) ?? [];
            int index = 0;
            foreach (ConfigObj obj in configObjs)
            {
                obj.ConfigFileName = Path.GetFileName(path);
                obj.ConfigObjectIndex = index;
                index++;
            }
            return configObjs;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"ERROR: {path} not found.");
        }
        catch (JsonException)
        {
            Console.WriteLine($"ERROR: {path} is not a valid JSON format.");
        }
        return null;
    }

    public static List<ConfigObj> LoadConfigsFromSelector(string[] args, string[] allConfigFilePaths)
    {
        if (allConfigFilePaths.Length < 1)
        {
            throw new FileNotFoundException();
        }
        bool[] selectedOptions = new bool[allConfigFilePaths.Length + 1];
        int currentOption = 0;
        List<string> gameTitles = [];
        int longestFileName = 0;

        // Get longest file name for padding purposes
        // Also check items that were passed in args
        for (int i = 0; i < allConfigFilePaths.Length; i++)
        {
            string fileName = Path.GetFileName(allConfigFilePaths[i]);
            if (fileName.Length > longestFileName)
            {
                longestFileName = fileName.Length;
            }

            // If "bp" was passed as an arg, set "bp.json" to checked by default
            if (args.Any(arg => arg.Equals(fileName.Split(".").First())))
            {
                selectedOptions[i + 1] = true;
            }
        }

        List<string> paddedFileNames = [];
        foreach (string filePath in allConfigFilePaths)
        {
            string fileName = Path.GetFileName(filePath);
            paddedFileNames.Add(fileName.PadRight(longestFileName + 1, ' '));
        }

        for (int i = 0; i < allConfigFilePaths.Length; i++)
        {
            List<ConfigObj>? configObjsInFile = LoadConfigFile(allConfigFilePaths[i]);

            if (configObjsInFile != null)
            {
                List<string> gameTitlesInFile = [];
                foreach (ConfigObj configObj in configObjsInFile)
                {
                    gameTitlesInFile.Add(configObj.GameTitle);
                }
                gameTitles.Add($"({string.Join(", ", [.. gameTitlesInFile])})");
            }
            else
            {
                gameTitles.Add("");
            }
        }

        while (true)
        {
            Console.Clear(); // Clear the console screen before re-printing options
            Console.WriteLine($"{(allConfigFilePaths.Length > 1 ? "Multiple config files detected. Select the ones" : "Select the config files")} you wish to execute with arrows keys, space to select, enter to confirm, or escape to exit.");

            for (int i = 0; i < selectedOptions.Length; i++)
            {
                Console.Write(currentOption == i ? "> " : "  ");

                if (i > 0)
                {
                    Console.Write(selectedOptions[i] ? "[x] " : "[ ] ");
                    Console.WriteLine($"{paddedFileNames[i - 1]} {gameTitles[i - 1]}");
                }
                else if (i == 0 && selectedOptions[0])
                {
                    Console.WriteLine("Unselect all");
                }
                else
                {
                    Console.WriteLine("Select all");
                }
            }

            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    currentOption = (currentOption - 1 + selectedOptions.Length) % selectedOptions.Length;
                    break;
                case ConsoleKey.DownArrow:
                    currentOption = (currentOption + 1) % selectedOptions.Length;
                    break;
                case ConsoleKey.Spacebar:
                    if (currentOption == 0)
                    {
                        for (int i = 0; i < selectedOptions.Length; i++)
                        {
                            selectedOptions[i] = !selectedOptions[i];
                        }
                    }
                    else
                    {
                        selectedOptions[currentOption] = !selectedOptions[currentOption];
                    }
                    break;
                case ConsoleKey.Enter:
                    List<string> result = [];
                    for (int i = 1; i < selectedOptions.Length; i++)
                    {
                        if (selectedOptions[i])
                        {
                            result.Add(Path.GetFileNameWithoutExtension(allConfigFilePaths[i - 1]));
                        }
                    }
                    Console.WriteLine();
                    return LoadAllConfigs([.. result]);
                case ConsoleKey.Escape:
                    throw new OperationCanceledException();
            }
        }
    }

    public static List<ConfigObj> LoadAllConfigs(string[] args)
    {
        List<ConfigObj> allConfigObjs = [];
        string[] allConfigFilePaths = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "configs"));

        bool isReleaseMode = false;

#if !DEBUG
            isReleaseMode = true;
#endif

        if (args.Length > 0 || isReleaseMode)
        {
            // Show list of config files
            if (args.Any(arg => arg.Equals("--list")) || (isReleaseMode && args.Length < 1))
            {
                args = args.Where(arg => arg != "--list").ToArray();
                return LoadConfigsFromSelector(args, allConfigFilePaths);
            }

            int totalConfigFiles = 0;

            // Load all files
            if (args.Any(arg => arg.Equals("all")))
            {
                foreach (var filePath in allConfigFilePaths)
                {
                    List<ConfigObj>? configObjsInFile = LoadConfigFile(filePath);

                    if (configObjsInFile != null)
                    {
                        foreach (ConfigObj configObj in configObjsInFile)
                        {
                            allConfigObjs.Add(configObj);
                        }
                        totalConfigFiles++;
                        Console.WriteLine($"{Path.GetFileName(filePath)} ({configObjsInFile.Count} object{(configObjsInFile.Count > 1 ? "s" : "")})");
                    }
                }
            }
            // Load specified files
            else
            {
                foreach (var arg in args)
                {
                    List<ConfigObj>? configObjsInFile = LoadConfigFile(Path.Combine(Directory.GetCurrentDirectory(), "configs", $"{arg}.json"));

                    if (configObjsInFile != null)
                    {
                        foreach (ConfigObj configObj in configObjsInFile)
                        {
                            allConfigObjs.Add(configObj);
                        }
                        totalConfigFiles++;
                        Console.WriteLine($"{arg}.json ({configObjsInFile.Count} object{(configObjsInFile.Count > 1 ? "s" : "")})");
                    }
                }
            }

            Console.WriteLine($"Loaded {totalConfigFiles} config file(s) ({allConfigObjs.Count} total object{(allConfigObjs.Count > 1 ? "s" : "")})");
        }
        // Fallback to default config.json
        else
        {
            Console.WriteLine("No config file(s) specified. Defaulting to config.json...");
            List<ConfigObj>? configObjsInFile = LoadConfigFile(Path.Combine(Directory.GetCurrentDirectory(), "configs", "config.json"));

            if (configObjsInFile != null)
            {
                foreach (ConfigObj configObj in configObjsInFile)
                {
                    allConfigObjs.Add(configObj);
                }
                Console.WriteLine($"Loaded config.json ({allConfigObjs.Count} object{(allConfigObjs.Count > 1 ? "s" : "")})");
            }
        }
        Console.WriteLine();

        return allConfigObjs;
    }

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
        var provider = new DefaultFileProvider(config.PaksDir, SearchOption.AllDirectories, true, new VersionContainer(selectedVersion));
        provider.Initialize();

        // Decrypt
        string aes = config.Aes.Length > 0 ? config.Aes : "0x0000000000000000000000000000000000000000000000000000000000000000";
        provider.SubmitKey(new FGuid(), new FAesKey(aes));

        // Set locale if provided, otherwise English
        if (config.Lang?.Length > 0)
        {
            ELanguage selectedLang = (ELanguage)Enum.Parse(typeof(ELanguage), config.Lang);
            provider.LoadLocalization(selectedLang);
        }
        else
        {
            provider.LoadLocalization(ELanguage.English);
        }

        // TEMP (need to fix patchProvider for utoc/ucas support). For now it's not guaranteed that the patch paks will be reconciled correctly.
        string pathToMapping = Path.Combine(Directory.GetCurrentDirectory(), "mappings", $"{config.GameTitle}.usmap");
        if (File.Exists(pathToMapping))
        {
            Console.WriteLine($"Using mapping file: {pathToMapping}");
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(pathToMapping);
        }

        // Load files into PatchFileProvider so the patch uassets override original uassets
        var patchProvider = new PatchFileProvider();
        patchProvider.Load(provider);

        // Add mapping file based on GameTitle if provided
        string pathToMappingFile = Path.Combine(Directory.GetCurrentDirectory(), "mappings", $"{config.GameTitle}.usmap");
        if (File.Exists(pathToMappingFile))
        {
            Console.WriteLine($"Using mapping file: {pathToMappingFile}");
            patchProvider.MappingsContainer = new FileUsmapTypeMappingsProvider(pathToMappingFile);
        }

        return patchProvider;
    }

    public static void Export(AbstractFileProvider provider, ConfigObj config, double start)
    {
        // Load checkpoint if provided
        useCheckpoint = false;
        Dictionary<string, long> loadedCheckpoint = LoadCheckpoint(config);
        ConcurrentDictionary<string, long> newCheckpointDict = [];

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
            var outputDir = config.KeepDirectoryStructure ?
                Path.Combine(Path.GetFullPath(config.OutputDir), fileDir)
                : Path.GetFullPath(config.OutputDir);

            // "D:\UnrealExporter\output\Hotta\Content\Resources\UI\Activity\Activity\DT_Activityquest_Balance"
            var outputPath = Path.Combine(outputDir, fileName);

            string regexMatch =
                config.Export
                .FirstOrDefault(path => new Regex("^" + path[..path.LastIndexOf(':')] + "$", RegexOptions.IgnoreCase)
                .IsMatch(file.Value.Path), "");

            bool isExclude =
                config.Exclude
                .Any(path => new Regex("^" + path + "$", RegexOptions.IgnoreCase)
                .IsMatch(file.Value.Path));

            bool isChanged = true;

            // If checkpoint is specified, skip files whose sizes are the same as in the checkpoint
            if (useCheckpoint && loadedCheckpoint.TryGetValue(file.Value.Path, out long fileSize))
            {
                isChanged = fileSize != file.Value.Size;
                if (isChanged) Interlocked.Increment(ref totalChangedFiles);
            }

            if (config.CreateNewCheckpoint) newCheckpointDict.TryAdd(file.Value.Path, file.Value.Size);

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
                                var allObjects = provider.LoadAllObjects(file.Value.Path);

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
                                                if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".png");
                                                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                                                // Save the bitmap to a file
                                                using (SKImage image = SKImage.FromBitmap(bitmap))
                                                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                                                using (Stream stream = File.OpenWrite(outputPath + ".png"))
                                                {
                                                    data.SaveTo(stream);
                                                }
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
                                    if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".json");
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
                                            if (config.LogOutputs) Console.WriteLine("=> " + outputPath + "." + kvp.Key.SubstringAfterLast('.'));
                                            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                            File.WriteAllBytes(outputPath + "." + kvp.Key.SubstringAfterLast('.'), kvp.Value);
                                            Interlocked.Increment(ref totalExportedFiles);
                                        });
                                    }
                                }

                                // else if (outputType == "uexp")
                                // {
                                //     if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".uexp");
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
                                    if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".json");
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
                                    if (config.LogOutputs) Console.WriteLine("=> " + outputPath + "." + outputType);
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
                                    if (config.LogOutputs) Console.WriteLine("=> " + outputPath + "." + outputType);
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
        if (config.CreateNewCheckpoint) CreateCheckpoint(newCheckpointDict, config);

        // Log results
        if (config.LogOutputs && totalExportedFiles > 0 && !config.CreateNewCheckpoint) Console.WriteLine();
        Console.WriteLine($"Scanned {provider.Files.Count} files{(useCheckpoint ? $" ({totalChangedFiles} changed, {provider.Files.Count - totalChangedFiles} unchanged)" : "")}");
        Console.WriteLine($"Regex matched {totalRegexMatches} files {(totalRegexMatches > totalExportedFiles ? $"(skipped {totalRegexMatches - totalExportedFiles} incompatible file types)" : "")}");
        Console.WriteLine($"Exported {totalExportedFiles} files in {Elapsed(start, Now(), 1000)} seconds");
        Console.WriteLine();
    }

    public static Dictionary<string, long> LoadCheckpoint(ConfigObj config)
    {
        if (config?.UseCheckpointFile?.Length > 0)
        {
            string checkpointPath = Path.Combine(Directory.GetCurrentDirectory(), config.UseCheckpointFile);
            if (config.UseCheckpointFile.Equals("latest"))
            {
                string[] allCheckpointPaths = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "checkpoints"));
                var pathsForGameTitle = allCheckpointPaths.Where(path => path.Contains(config.GameTitle));

                if (!pathsForGameTitle.Any())
                {
                    Console.WriteLine($"ERROR: could not find any checkpoints for \"{config.GameTitle}\". Ignoring...");
                    return [];
                }

                var validCheckpoints = new List<(string Path, double UnixTime)>();
                foreach (var path in pathsForGameTitle)
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string pattern = @"(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})";
                    Match match = Regex.Match(fileName, pattern);

                    if (match.Success)
                    {
                        string dateTimeString = match.Groups[1].Value;
                        string format = "yyyy-MM-dd_HH-mm-ss";

                        if (DateTime.TryParseExact(dateTimeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                        {
                            validCheckpoints.Add((path, ((DateTimeOffset)parsedDate).ToUnixTimeSeconds()));
                        }
                    }
                }

                var latestCheckpoint = validCheckpoints
                    .OrderBy(c => c.UnixTime)
                    .LastOrDefault();

                if (latestCheckpoint.Path == null)
                {
                    Console.WriteLine($"ERROR: Found checkpoints for \"{config.GameTitle}\", but none have a valid timestamp. Ignoring...");
                    return [];
                }
                var latestCheckpointPath = latestCheckpoint.Path;


                if (File.Exists(latestCheckpointPath))
                {
                    useCheckpoint = true;
                    Console.WriteLine($"Using checkpoint: latest ({Path.GetFileName(latestCheckpointPath)})");
                    var fromFile = File.ReadAllText(latestCheckpointPath);
                    var loadedCheckpoint = JsonConvert.DeserializeObject<Dictionary<string, long>>(fromFile);
                    return loadedCheckpoint ?? [];
                }

                return [];
            }
            else if (File.Exists(checkpointPath))
            {
                useCheckpoint = true;
                Console.WriteLine($"Using checkpoint: {config.UseCheckpointFile}");
                var fromFile = File.ReadAllText(checkpointPath);
                var loadedCheckpoint = JsonConvert.DeserializeObject<Dictionary<string, long>>(fromFile);
                return loadedCheckpoint ?? [];
            }
            else
            {
                Console.WriteLine($"ERROR: checkpoint file at location \"{config.UseCheckpointFile}\" does not exist. Ignoring...");
                return [];
            }
        }
        else
        {
            Console.WriteLine($"No checkpoint file selected. Ignoring...");
            return [];
        }
    }

    public static void CreateCheckpoint(ConcurrentDictionary<string, long> newCheckpointDict, ConfigObj config)
    {
        Console.WriteLine();
        var newCheckpointJson = JsonConvert.SerializeObject(newCheckpointDict, Formatting.Indented);
        var dateStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        string checkpointsDirPath = Path.Combine(Directory.GetCurrentDirectory(), "checkpoints");
        if (!Directory.Exists(checkpointsDirPath))
        {
            Directory.CreateDirectory(checkpointsDirPath);
        }

        var checkpointFileName = $"{config.GameTitle}_{dateStamp}.ckpt";

        File.WriteAllText(Path.Combine(checkpointsDirPath, checkpointFileName), newCheckpointJson);
        Console.WriteLine($"Created checkpoint file: {checkpointFileName}");
    }

    public static double Now()
    {
        return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }

    public static string Elapsed(double start, double end, int factor = 1)
    {
        return ((end - start) / factor).ToString("0.00");
    }
}

public class ConfigObj
{
    public required string ConfigFileName { get; set; }
    public required int ConfigObjectIndex { get; set; }
    public required string GameTitle { get; set; }
    public required string Version { get; set; }
    public required string PaksDir { get; set; }
    public required string OutputDir { get; set; }
    public required string Aes { get; set; }
    public required bool LogOutputs { get; set; }
    public required bool KeepDirectoryStructure { get; set; }
    public string? Lang { get; set; }
    public bool CreateNewCheckpoint { get; set; }
    public string? UseCheckpointFile { get; set; }
    public required List<string> Export { get; set; }
    public required List<string> Exclude { get; set; }
}