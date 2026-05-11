<!-- Available under the Apache License 2.0 at https://github.com/luk-gg/UnrealExporter -->
# UnrealExporter
A CLI for extracting and datamining Unreal Engine game assets, powered by [CUE4Parse](https://github.com/FabianFG/CUE4Parse).

## Features
- [x] Regex paths for bulk extracting
- [x] Path exclusions to avoid crashing
- [x] [Checkpoint system](#checkpoints) (only extract new/changed files)
- [x] Parallel-processing files
- [x] Apply mapping files
- [x] Supports [40+ file types](/UnrealExporter/Export/ExportService.cs#L255) seen in UE games

All games supported by FModel are supported, as both use CUE4Parse.

## Usage
UnrealExporter can be used entirely with flags (run with `--help` or see [here](/UnrealExporter/Cli/CliSettings.cs)), or you can create reusable config files. If both are provided, flags will override config values. An interactive config builder is available when running the exe without any arguments (or see [example config](/UnrealExporter/examples/palworld-config.json)).

### Download and run
After using [FModel](https://github.com/4sval/FModel) to determine which paths you wish you extract, download the latest [release](https://github.com/whotookzakum/UnrealExporter/releases) and run `UnrealExporter.exe`. 

### Manual setup (recommended)
1. Download and install .NET SDK 9.0
2. Clone the repo, including all submodules, with `git clone https://github.com/whotookzakum/UnrealExporter --recursive`
3. In your terminal, run `dotnet run --project UnrealExporter --`

Example command:
```
dotnet run --project UnrealExporter -- pal.json -m MappingOverride.usmap --create-checkpoint --checkpoint latest
```

If you wish to build the project as a executable binary, use the following command:

```sh
dotnet publish UnrealExporter -c Release --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

## Checkpoints
Checkpoints allow you to extract only new/modified files and skip unchanged files by keeping track of file sizes (see [example checkpoint](/UnrealExporter/examples/palworld-checkpoint.json)), greatly increasing extraction speed. **Checkpoints will always track all game files, regardless of what you extract/exclude.** Thus it is safe to use an existing checkpoint *and* create a new checkpoint at the same time to repeatedly only extract changed files every time a game updates.

> [!TIP]
> You can quickly create a checkpoint without extracting anything by using the flags `--create-checkpoint --exclude "*"`

> [!TIP]
> `CreateNewCheckpoint` (bool) and `CheckpointFileName` (filename.json) can optionally be added to your config files (see the [example config](/UnrealExporter/examples/palworld-config.json)).

## Helpful resources
AES Keys:
- [cs.rin.ru UE4/5 Key Collection](https://cs.rin.ru/forum/viewtopic.php?f=10&t=100672)
- [UE4 AES Key Extracting Guide](https://github.com/Cracko298/UE4-AES-Key-Extracting-Guide)
- [AES Finder](https://github.com/mmozeiko/aes-finder)
- Check the `#game-compatibility` channel in the [Fmodel Discord](https://discord.com/invite/fmodel)

Mapping files:
- [Unreal-Mappings-Archive](https://github.com/TheNaeem/Unreal-Mappings-Archive)
- [UnrealMappingsDumper](https://github.com/TheNaeem/UnrealMappingsDumper)
- Check the `#game-compatibility` channel in the [Fmodel Discord](https://discord.com/invite/fmodel)

### Get your own mapping file with UE4SS
1. Download the latest release of [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) and extract the files to the same location as your game's `-Win64-Shipping.exe` file.
2. Modify the following sections in `UE4SS-settings.ini` 
```diff
[Debug]
+ GuiConsoleEnabled = 1
+ GuiConsoleVisible = 1
...
+ GraphicsAPI = dx11
```
3. Launch the game.
4. In the UE4SS GUI, click on the Dumper tab and output the usmap file. It'll be in the same location (`.../Binaries/Win64/Mappings.usmap`).
5. Copy or move the `.usmap` file to UnrealExporter's `mappings` folder
6. Update your config to match the file name of your mappings file, i.e. `Palworld.usmap`

The exporter should now be able to detect the game files.

<!-- 
  NOTES FOR CHECKING EDGE CASES:
  - Patch files (_0_P.pak, etc.) being applied in order can be validated with SWORD ART ONLINE Fractured Daydream: export "SEVEN_Product/Content/Product/DataTable/DT_PassiveSkillData.uasset" -- UCR004_FlightMaster (Leafa's passive) m_strength should be 25.0 (SAOFD-Windows_0_P.utoc), not 10.0 (SAOFD-Windows.utoc)
    - Also in Tower of Fantasy Global: "Hotta/Content/Localization/Game/en/.*\\.locres:json" exports twice (pakchunk0-WindowsNoEditor.pak and pakchunk0-WindowsNoEditor_0_P.pak; patch pak is larger and gets used, however it does count the regex match and export count twice)
  - zlib dll can be validated with Tower of Fantasy Global: try to extract anything
  - Detex dll can be validated with SWORD ART ONLINE Fractured Daydream: try to extract "SEVEN_Product/Content/Product/UI/Texture/Common/ButtonWindowBase/.*\\.uasset:png"
 -->

<!-- TODO: reimplement locale -->
<!-- TODO: implement extracting audio (.hca → .wav in SAOFD; .wem, .bnk, .pck in Fmodel Extract(); .ogg, .wav, .opus, .mp3 outputs) -->