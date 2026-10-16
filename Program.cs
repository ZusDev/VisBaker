using ByDexter.Shared.Source2;
using FPS.Visibility;
using System.Numerics;

try
{
  if (args.Length < 2 || args.Length > 4 || (args.Length == 4 && args[3] != "--if-needed"))
    throw new ArgumentException("Usage: VisBaker <csgo-directory> <map-name> [threads] [--if-needed]");

  string root = Path.GetFullPath(args[0]);
  string key = args[1];
  var (map, addon) = MapIdentity.Parse(key);
  string vpk = MapIdentity.Archive(root, key);
  string sourceRoot = addon.Length == 0 ? root : Path.GetDirectoryName(vpk)!;

  if (!File.Exists(vpk))
    throw new FileNotFoundException("Map VPK missing", vpk);

  int threads = args.Length >= 3 ? int.Parse(args[2]) : Math.Max(1, Environment.ProcessorCount / 2);

  if (threads < 1 || threads > 64)
    throw new ArgumentException("threads must be 1-64");

  string output = Path.Combine(root, "addons", "antiwallhack", "vis", key + ".awvis");
  Directory.CreateDirectory(Path.GetDirectoryName(output)!);

  // OS lock prevents another plugin/process from writing the same map concurrently.
  using var bakeLock = new FileStream(output + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

  if (args.Length == 4 && NativeTable.IsCurrent(output, root, key))
  {
    Console.WriteLine($"VIS already current for {map}; skipping bake.");

    return 0;
  }

  Console.WriteLine($"Extracting {map} collision...");
  byte[] physics = MapPhysics.Read(vpk, map) ?? throw new InvalidDataException("world_physics missing");
  var nav = NavMesh.Polygons(MapPhysics.ReadNav(vpk, map));
  var world = WorldBvh.Solid(physics);

  // No live spawns are required offline: retain all valid floors, including isolated arenas.
  Console.WriteLine($"Baking {world.TriangleCount} triangles using {threads} threads...");
  var vis = VisMap.Bake(world, WorldBvh.PlayerSolid(physics), WorldBvh.Ladders(physics), Array.Empty<Vector3>(), nav, VisMap.HashOf(physics), threads, CancellationToken.None);

  var sources = VpkArchive.OpenedFiles.Order().Select(path =>
  {
    string relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');

    if (relative.StartsWith("../") || Path.IsPathRooted(relative))
      throw new InvalidDataException("Archive dependency outside csgo directory");

    var fingerprint = VpkArchive.Fingerprint(path);

    if (fingerprint != VpkArchive.Fingerprints[path])
      throw new InvalidDataException("Map archive changed during baking; retry after map updates finish.");

    return new { path = relative, size = fingerprint.Size, hash = fingerprint.Hash };
  }).ToArray();

  vis.ExportNative(output, key, sources);

  Console.WriteLine($"Saved {output}: {vis.Count} cells, {vis.Bytes / 1048576.0:F1} MiB table. AntiWallHack will detect the completed file.");
}
catch (Exception error)
{
  Console.Error.WriteLine(error.Message);

  return 1;
}

return 0;
