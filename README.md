# VisBaker

VisBaker prepares map visibility files for the AntiWallHack plugin.
You run this tool yourself, once for each map you want to prepare, 
and upload the finished files to your server.

VisBaker is a command-line program, not a game plugin. You can use it on a Windows
PC without installing Metamod or running a CS2 server. It supports official maps
and supported Workshop/addon maps.

## 1. Download the standalone tool

VisBaker is published separately at [ZusDev/VisBaker](https://github.com/ZusDev/VisBaker).
You do not need to download the AntiWallHack source project to build this tool.

Extract it and open the folder
containing `VisBaker.dll` in VS Code, then open a PowerShell terminal there.
Keep that entire folder together. The other DLLs, `.deps.json`, and
`.runtimeconfig.json` files are also needed, do not copy only `VisBaker.dll`.

For the framework-dependent tool, install the **.NET 10 Runtime**. The .NET 10 SDK
also works. Use a package intended for your operating system.

## 2. Check .NET and select your CS2 folder

The following examples assume your terminal is in the folder containing
`VisBaker.dll`, regardless of which option you chose above.

Check the installed runtimes:

```powershell
dotnet --list-runtimes
```

The output should include `Microsoft.NETCore.App 10.0.x`. If PowerShell says
`dotnet` is not recognized, install .NET and reopen VS Code.

### Find your own CS2 installation path

**There is no single CS2 path that works for everyone.** Steam libraries can be
on different drives, and dedicated servers can be installed in different folders.
Do not copy another user's installation path without checking it.

1. In Steam, open your Library and find **Counter-Strike 2**.
2. Use the game's **Browse local files** option to open its installed folder.
3. Inside that folder, open **game**, then **csgo**.
4. Copy the full path of that `csgo` folder from File Explorer's address bar.

Select the `game/csgo` folder, which should contain `maps`. Do not select the outer
Steam folder, the top game installation folder or an individual VPK file.

Run this command in the VisBaker terminal. When prompted, paste **your own copied
path** and press Enter. It can point to any drive:

```powershell
$csgo = (Read-Host 'Paste your full CS2 game/csgo folder path').Trim().Trim('"')

if (-not (Test-Path -LiteralPath (Join-Path $csgo 'maps') -PathType Container)) {
    throw 'The selected folder does not contain maps. Select the CS2 game/csgo folder.'
}
```

`$csgo` now means your selected folder in the remaining commands. It is remembered
only in this terminal; repeat this step if you open a new terminal. You do not
need to move VisBaker into the CS2 installation.

For a dedicated server or copied server files, select that installation's actual
`game/csgo` folder instead. Include the map VPKs and any required archive chunks.

## 3. Bake an official map

Start with one map:

```powershell
dotnet .\VisBaker.dll $csgo de_mirage 2
```

This command means:

| Part | Meaning |
| --- | --- |
| `dotnet .\VisBaker.dll` | Start the manual baker in the current folder. |
| `$csgo` | Read map files from the CS2 folder you selected. |
| `de_mirage` | Bake this map; do not add `.vpk`. |
| `2` | Use two worker threads. Use `1` to reduce CPU contention. |

Replace `de_mirage` with another installed map, such as `de_dust2`, and run the
command again. Each command prepares one map. For a small list:

```powershell
foreach ($map in @('de_mirage', 'de_dust2', 'de_inferno')) {
    dotnet .\VisBaker.dll $csgo $map 2
    if ($LASTEXITCODE -ne 0) { break }
}
```

The baker reads map geometry, calculates visibility, and writes a table. Wait for
a line beginning with `Saved`.

For Mirage, the output is inside the selected CS2 folder:

```text
addons/antiwallhack/vis/de_mirage.awvis
```

## 4. Bake a Workshop map

First make sure the Workshop map has finished downloading. You need two values:

- **Workshop item ID:** the number after `id=` in the item's Workshop page URL.
- **Internal map name:** the name used when loading the map, without `.vpk`.
  The Workshop page title and the outer addon VPK filename may be different.

Use this command, replacing both example values:

```powershell
dotnet .\VisBaker.dll $csgo workshop/1234567890/aim_map 2
```

`1234567890` and `aim_map` are placeholders, not a supplied map. Keep the
`workshop/ID/map_name` structure, including its forward slashes.

The output for that example is:

```text
addons/antiwallhack/vis/workshop/1234567890/aim_map.awvis
```

Workshop maps with the same internal name stay separate because each item has
its own ID folder. Names and IDs must contain only ASCII letters, digits,
underscores or hyphens. Multiple mounted addon lists are unsupported.

The tool searches the selected ID's folder under `maps/workshop`, `csgo_addons`,
`csgo_community_addons`, and Steam Workshop content directories. The usual PC
location `steamapps/workshop/content/730/ID` is supported when it is in the same
Steam library as CS2. Addon directories are searched at the selected csgo folder
and up to four ancestors, including `bin/<platform>/steamapps/workshop/content/730`.

One non-chunk VPK container must be found for the selected item. Numbered archive
chunks, such as `item_000.vpk`, are allowed and must remain beside their container.
The tool rejects multiple possible containers instead of guessing which is active.
The parser supports direct map resources and a nested `maps/<map>.vpk`.

## 5. Upload the finished files to your server

1. open the `addons/antiwallhack/vis` folder inside your selected
   `game/csgo` directory.
2. Upload its `.awvis` files and Workshop subfolders into the server's
   `game/csgo/addons/antiwallhack/vis` folder. Preserve the directory structure.
3. Use the updated AntiWallHack plugin with Workshop support on the server.
4. Load the matching map. During active filtering, AntiWallHack checks for new
   tables every five seconds and validates them in the background.
5. Look for `VIS activated for ...` in the server console.

You only need the finished `.awvis` files on the server for manual baking. Do not
upload unfinished `.tmp` files or `.lock` files. A `.lock` file may remain after a
successful bake, the operating-system lock, not its presence, prevents overlapping jobs.

Missing or rejected tables keep engine-ray filtering active. A completed bake
does not by itself confirm that the server accepted the table.

## Optional: skip a table that is already current

Add `--if-needed` after an explicit worker count:

```powershell
dotnet .\VisBaker.dll $csgo de_mirage 2 --if-needed
```

The tool checks the existing table, source fingerprints and payload. If they are
current, it prints `VIS already current` and skips the calculation. Otherwise it
bakes a replacement. Without this option, the tool bakes again.

General syntax:

```text
dotnet VisBaker.dll <csgo-directory> <map-or-workshop/ID/map> [workers] [--if-needed]
```

Worker count accepts 1–64. If omitted, it defaults to half the available CPU cores,
with a minimum of one. Specify a count when using `--if-needed`.

## Technical notes and limits

Tables contain sampled static `world_physics` geometry, not live doors, props or
smoke. Potentially visible pairs still use engine traces and separate smoke checks.
The grid is an approximation. Verify peeks, stairs, jumps, glass, moving doors and
destructible cover on each map before deployment.

The loader checks the format, dimensions, payload checksum, and full-file FNV-1a
fingerprints of the selected map/addon VPK and every archive opened by the baker.
These fingerprints detect stale local files; they are not security signatures.
Workshop source paths are relative to the selected archive's directory, so files
can be transferred between installations with identical archive contents. Tables
are capped at 256 MiB by the native loader. Existing FPS `.vis` files are incompatible;
generate `.awvis` files with this tool.

## Credits

MIT-licensed ByDexter FPS v6 grid baker and Source2 parsers
from CS2Plugins revision `413c38a21ff39382296430dbf3a0f829bf57da61`.
Original source: https://github.com/ByDexterTR/CS2Plugins/tree/main/FPS
