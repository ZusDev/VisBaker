using System.Text.Json;
using ByDexter.Shared.Source2;

public static class NativeTable
{
  public static bool IsCurrent(string path, string root, string map)
  {
    try
    {
      var (_, addon) = MapIdentity.Parse(map);
      string archive = MapIdentity.Archive(root, map);
      string sourceRoot = addon.Length == 0 ? root : Path.GetDirectoryName(archive)!;
      string expectedSource = Path.GetRelativePath(sourceRoot, archive).Replace('\\', '/');

      using var stream = File.OpenRead(path);
      using var reader = new BinaryReader(stream);

      int length = reader.ReadInt32();

      if (length <= 0 || length > 65536)
        return false;

      using var json = JsonDocument.Parse(reader.ReadBytes(length));
      var data = json.RootElement;

      if (data.GetProperty("version").GetInt32() != 1 || data.GetProperty("map").GetString() != map || data.GetProperty("cellSize").GetSingle() != 64)
        return false;

      int nx = data.GetProperty("nx").GetInt32();
      int ny = data.GetProperty("ny").GetInt32();
      int nz = data.GetProperty("nz").GetInt32();
      int count = data.GetProperty("count").GetInt32();

      if (nx <= 0 || ny <= 0 || nz <= 0 || nx > 4096 || ny > 4096 || nz > 4096 || count <= 0 || count > 32768)
        return false;

      long size = (long)nx * ny * nz * 4 + (long)count * 12 + (long)count * ((count + 63) / 64) * 8;

      if (size > 256L * 1024 * 1024 || stream.Length != 4 + length + size)
        return false;

      var sources = data.GetProperty("sources");

      if (sources.GetArrayLength() == 0 || sources.GetArrayLength() > 128)
        return false;

      bool hasMap = false;

      foreach (var source in sources.EnumerateArray())
      {
        string name = source.GetProperty("path").GetString()!;

        if (string.IsNullOrEmpty(name) || name.Contains('\\') || name.Contains(':') || Path.IsPathRooted(name) || name.Split('/').Any(part => part is "." or ".."))
          return false;

        if (addon.Length != 0 && name.Contains('/'))
          return false;

        hasMap |= name == expectedSource;

        var fingerprint = VpkArchive.Fingerprint(Path.Combine(sourceRoot, name));

        if (fingerprint.Size != source.GetProperty("size").GetInt64() || fingerprint.Hash != source.GetProperty("hash").GetUInt64())
          return false;
      }

      if (!hasMap)
        return false;

      var minimum = data.GetProperty("min");

      if (minimum.GetArrayLength() != 3 || minimum.EnumerateArray().Any(v => !float.IsFinite(v.GetSingle())))
        return false;

      long payloadStart = stream.Position;

      for (long index = 0; index < (long)nx * ny * nz; index++)
      {
        int cell = reader.ReadInt32();

        if (cell < -1 || cell >= count)
          return false;
      }

      for (int i = 0; i < count * 3; i++)
      {
        if (!float.IsFinite(reader.ReadSingle()))
          return false;
      }

      stream.Position = payloadStart;

      ulong hash = 14695981039346656037UL;
      byte[] buffer = new byte[65536];
      int read;

      while ((read = stream.Read(buffer)) != 0)
      {
        for (int i = 0; i < read; i++)
          hash = unchecked((hash ^ buffer[i]) * 1099511628211UL);
      }

      return hash == data.GetProperty("checksum").GetUInt64();
    }
    catch
    {
      return false;
    }
  }
}
