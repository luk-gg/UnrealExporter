# UnrealExporter
A batch file exporter for Unreal Engine games using [CUE4Parse](https://github.com/FabianFG/CUE4Parse). Great for automating UE datamining, just specify the files you want and don't want, and speed up your exports by only exporting what's changed.

This project can be used as-is or as a reference, since CUE4Parse documentation is incomplete. Heavily references the source code of [FModel](https://github.com/4sval/FModel) for CUE4Parse usage.

## [Features](#features)
- [x] Multiple game support (via multiple config objects or multiple config files)
- [x] Regex paths for bulk exporting
- [x] Path exclusions to avoid crashing
- [x] Patch .pak reconciliation (courtesy of [MCMrARM](https://github.com/MCMrARM))
- [x] Checkpoint support (only export new/changed files!)
- [x] Parallel-processing files
- [x] Apply mapping files
- [x] CLI args support (point to config files; no support for passing individual keys/values)
- [ ] Log file (errors, which files were skipped, which files were outputted, config settings, etc.)
- [ ] Automatic AES key finding
- [ ] Automatic binary releases (GitHub actions)

### [Supported file types](#supported-file-types)
When listing an export file path, specify the desired output type at the end, such as `:json`.
- [x] uasset (as json, png, uasset + uexp)
- [x] umap (as json)
- [x] locres (as json)
- [x] js (as js)
- [x] db (as db)
- [x] upluginmanifest, uproject, manifest, uplugin, archive, vmodule, verse, html, json, ini, txt, log, bat, dat, cfg, ide, ipl, zon, xml, css, csv, pem, tps, lua, po, h (as their respective type)
- [ ] everything else

## [Usage](#usage)
> [!TIP]
> If you know how to use git and the terminal, I recommend cloning the repo instead of downloading a release so that you can quickly get the latest updates with `git pull`.

### Easy setup (download and run)
1. Download latest [release](https://github.com/whotookzakum/UnrealExporter/releases)
2. Configure `config.json` (see Config Options below)
3. Run `UnrealExporter.exe`

### Manual setup (recommended)
1. Download and install .NET SDK 8.0
2. Clone the repo including all submodules, i.e. `git clone https://github.com/whotookzakum/UnrealExporter --recursive`
3. Configure `/configs/config.json` (see Config Options below)
4. Open terminal and execute `dotnet run --project UnrealExporter`

If you wish to build the project as a executable binary, use the following command:

```sh
dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

## [Config Options](#config-options)
Configure the `config.json` file in the `/configs` folder based on the game(s) you wish to export from. Config files must be an array at the top level.

> [!TIP]
> You can add multiple objects to export from different games, or different files from the same game. For different games, I recommend using [multiple configs](#multiple-configs) so you don't have to edit the same config file when you want to change games.

Example config files can be found in `/configs/examples`. The excluded paths in the example configs are a non-exhaustive list of game files that are known to crash CUE4Parse.

| Key | Type | Description |
|-----|-----------|-----------|
| gameTitle              | `string`        | Title of the UE game. Can be any arbitrary string that will work as a file name (not allowed: `\ / : * ? " \| < >`). Used for checkpoint naming, matching mapping files, and for your own recordkeeping. |
| version                | `string`        | Unreal Engine version. [UE versions supported by CUE4Parse](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs). Accepts strings separated by one period, like `"4.27"`, and supported game titles formatted camel-case or separated by spaces, such as `"TowerOfFantasy"` or `"tower of fantasy"`. Can be found by right clicking the game's .exe > Properties tab > Details (if removed, check the [FModel discord](https://discord.com/channels/637265123144237061/1090586945412931734)). |
| paksDir                | `string`        | An __absolute path__ to the game directory containing the .pak file(s). |
| outputDir              | `string`        | A __relative path__ to where you want to place the exported files. |
| aes                    | `string`        | The AES-256 decryption key to access game files. [Guide on how to obtain](https://github.com/Cracko298/UE4-AES-Key-Extracting-Guide). Leave blank if not needed. |
| logOutputs             | `bool`          | If set to `true`, every exported file's path will be logged. If set to `false`, these logs are skipped. Note: The logging occurs **before** attempting to export the file, so if the program crashes, check the last few logged files (it will not always be the last file if you have multithreading enabled). | 
| keepDirectoryStructure | `bool`          | If set to `true`, folders will be made matching those found in the .paks. If set to `false`, all files will be output at the root level of the `outputDir`.     |
| lang                   | `string`        | Language that strings should be output in. [Supported languages](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/ELanguage.cs). Useful for specifying the target language for localized resources. Will only work if the game supports the specified localization. Defaults to `English`. |
| createNewCheckpoint    | `bool`          | If set to `true`, will output a new checkpoint file in the `/checkpoints` directory. If set to `false`, will not create a checkpoint file. More details about checkpoints below. |
| useCheckpointFile      | `string`        | A __relative path__ to a checkpoint file in the `/checkpoints` directory, i.e. `/checkpoints/Tower of Fantasy 02-26-2024 06-08.ckpt`. If set to `latest`, the program will look for the latest checkpoint in the `/checkpoints` folder that contains the `gameTitle` provided, i.e, between `/checkpoints/Palworld 02-26-2024 00-00.ckpt` and `/checkpoints/Palworld 05-30-2024 00-00.ckpt`, the latter will be used (will not work if you changed the file name structure). More details about checkpoints below. |
| export        | `Array(string)` | A list of files to export. Supports regex. Add a colon with the desired output type at the end, such as `:json` or `:png` (see [supported file types](#supported-file-types)). |
| exclude      | `Array(string)` | A list of files to skip exporting. Supports regex. Useful for avoiding files that crash CUE4Parse. Note: the program will try to automatically skip files that cannot be parsed by CUE4Parse, however files causing issues such as segmentation faults and heap corruption will not be skipped as they are not technically a failed parse, so they will need to be added to the excluded paths. |

> [!NOTE]
> File paths for `export` and `exclude` reside **inside the game files** (virtual file system). Use [FModel](https://github.com/4sval/FModel) to verify the paths that you wish to export. For example, Tower of Fantasy starts at `Hotta/Content/...`

I recommend specifying file extensions to avoid getting useless/unexportable files in your output. For example, you may only need a `.uasset` to be exported as JSON, but if you don't specify the file extension, it can export other files such as `.umap`, `.ubulk`, etc. which may be undesired.

### [Multiple Configs](#multiple-configs)
While you can always export from multiple games in one config, you may want to target only one game without having to modify the config file every time. Multiple configs makes this easy.

Create multiple JSONs in the `configs` folder, naming them something easy for you to remember **without spaces**, i.e. `blue-protocol.json`, `tof.json`, and `kartrider.json`. The file names (without extensions) can be appended as arguments to the `dotnet run` command (see table).

| Command                        | Selected configs                                                    |
|--------------------------------|---------------------------------------------------------------------|
| `dotnet run --project UnrealExporter`                   | `config.json`                                                       |
| `dotnet run --project UnrealExporter all`               | Every JSON directly in the `/configs` folder                        |
| `dotnet run --project UnrealExporter blue-protocol tof` | `blue-protocol.json`, `tof.json`                                    |
| `dotnet run --project UnrealExporter --list`            | Lists all configs found in the `/configs` folder                    |
| `dotnet run --project UnrealExporter --list bp tof`     | Lists all configs, with `bp.json` and `tof.json` checked by default |

<!-- `--project UnrealExporter` can be omitted if cd into the project -->

#### [Config List](#config-list)
If you pass the config list flag `--list`, the program will prompt you to select the configs you wish to use, listing the `gameTitle` for each object in the config. **This is enabled by default in the binary executable** unless an argument is passed.

Example:
```
Multiple config files detected. Select the ones you wish to execute with arrows keys, space to select, enter to confirm, or escape to exit.
  Select all
  [x] bp.json      (Blue Protocol)
> [ ] config.json  (Palworld)
  [ ] kr.json      (KartRider Drift)
  [x] tof.json     (Tower of Fantasy Global, Tower of Fantasy Global)
```

## [Checkpoints](#checkpoints)
Similar to FModel's `.fbkp` system, checkpoints allow you to export only new/modified files and skip unchanged files, reducing the amount of time needed to export. 

A `.ckpt` file is a JSON that maps each file's path to its size, i.e. `"Hotta/Content/Resources/FB/FB_Gulan/Warning.uexp": 3513`. They are outputted to the `/checkpoints` folder by default, and named via `gameTitle` and a timestamp.

If a valid checkpoint is provided, the program will only export files that have different file sizes than the one in the checkpoint (modified files), or do not have an entry in the checkpoint (new files).

**Checkpoint files will always cover all the files in all paks, regardless of what your export/regexes.** That way, you can export a new checkpoint while using an older checkpoint, effectively only exporting the changed files in every game update.

> [!TIP]
> If you want to create a checkpoint but don't want to re-export existing files, set `"createNewCheckpoint": true"` and change the export paths to empty arrays.

## [Supported Games](#supported-games)
By default, all games supported by FModel should technically be supported by UnrealExporter, as both use CUE4Parse under the hood. You can find working configs for games that have been tested and confirmed to be working in the `/configs/examples` folder. Mileage may vary depending on the files you wish to export, so check for any error messages and exclude paths accordingly. You can also open an issue to request support for other file types.

### How to fix no files loading due to missing mapping file
If you are loading a game like Palworld, you will need the correct `.usmap` file so that the game files will correctly load into CUE4Parse. If your game's mapping file is not already provided in the `mappings` folder, follow the instructions below to obtain the file.

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
4. In the UE4SS GUI, click on the Dumper tab and output the usmap file. It'll be in the same location (`.../Binaries/Win64/Mappings.usmap`). This file can be used with FModel if needed.
5. Copy or move the `.usmap` file to UnrealExporter's `mappings` folder
6. Rename the file to match `gameTitle` from inside your `config.json`. For example, if `"gameTitle": "MyFunGame"`, rename the file `MyFunGame.usmap`.

The exporter should now be able to detect the game files.

### How to fix no files loading due to incorrect AES key
If you have the wrong AES key, refer to [this guide](https://github.com/Cracko298/UE4-AES-Key-Extracting-Guide) or [this tool (untested)](https://github.com/mmozeiko/aes-finder) to get the correct key.