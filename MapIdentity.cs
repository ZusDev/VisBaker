public static class MapIdentity
{
  public static bool Token(string value) => value.Length is > 0 and <= 128 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

  public static (string Map, string Addon) Parse(string key)
  {
    if (Token(key))
      return (key, "");

    string[] parts = key.Split('/');

    if (parts.Length == 3 && parts[0] == "workshop" && Token(parts[1]) && Token(parts[2]))
      return (parts[2], parts[1]);

    throw new ArgumentException("Use <map> or workshop/<addon-id>/<map>.");
  }

  public static string Archive(string root, string key)
  {
    var (map, addon) = Parse(key);

    if (addon.Length == 0)
      return Path.Combine(root, "maps", map + ".vpk");

    var folders = new List<string> { Path.Combine(root, "maps", "workshop", addon) };
    string? current = Path.GetFullPath(root);

    for (int depth = 0; depth < 5 && current != null; depth++, current = Path.GetDirectoryName(current))
    {
      folders.Add(Path.Combine(current, "csgo_addons", addon));
      folders.Add(Path.Combine(current, "csgo_community_addons", addon));
      folders.Add(Path.Combine(current, "steamapps", "workshop", "content", "730", addon));

      if (Path.GetFileName(current) == "steamapps")
        folders.Add(Path.Combine(current, "workshop", "content", "730", addon));

      string bin = Path.Combine(current, "bin");

      if (Directory.Exists(bin))
      {
        foreach (string platform in Directory.GetDirectories(bin))
          folders.Add(Path.Combine(platform, "steamapps", "workshop", "content", "730", addon));
      }
    }

    // Ordinal comparison matches the Linux server filesystem.
    var candidates = folders.Where(Directory.Exists).Distinct(StringComparer.Ordinal)
      .SelectMany(folder => Directory.EnumerateFiles(folder, "*.vpk", SearchOption.AllDirectories))
      .Where(path =>
      {
        string name = Path.GetFileNameWithoutExtension(path);
        int split = name.LastIndexOf('_');

        return !(split >= 0 && name.Length - split == 4 && name[(split + 1)..].All(char.IsAsciiDigit));
      }).Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).ToArray();

    if (candidates.Length == 1)
      return candidates[0];

    throw new IOException($"Workshop archive missing or ambiguous for {key}");
  }
}
