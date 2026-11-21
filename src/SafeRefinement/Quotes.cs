using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace SafeRefinement;

/// <summary>
/// Taunts shown under the message of a failed refinement, one per line in
/// BepInEx/config/rkmax.saferefinement.quotes.txt. The file is written with the defaults when missing and
/// read again whenever it changes. An empty file means no taunt.
/// </summary>
internal static class Quotes
{
    internal static readonly string[] Defaults =
    {
        "Odin saw that. He's still laughing.",
        "Even the Greydwarfs refine better than you.",
        "The gods demanded a sacrifice. They got your idol and your dignity.",
        "Your ancestors are watching. They look away.",
        "Brokkr would have kicked you out of the forge.",
        "Hit it harder next time. Or with your head.",
        "Valhalla called. They said no.",
        "The forge rejected you. Again.",
        "A troll with a log would have done better.",
        "That idol died for nothing.",
        "Skill issue, viking.",
        "Even Hugin is embarrassed for you.",
        "You swing a hammer like a boar swings a sword.",
        "The Elder pities you. And he's a tree.",
        "Somewhere, a Fuling is pointing at you and laughing.",
    };

    private static readonly Random Rng = new Random();
    private static string _path;
    private static DateTime _write;
    private static List<string> _quotes = new List<string>(Defaults);
    private static int _last = -1;

    internal static string Path => _path ??= System.IO.Path.Combine(Paths.ConfigPath, Plugin.Guid + ".quotes.txt");

    internal static void EnsureFile()
    {
        try
        {
            if (!File.Exists(Path))
                File.WriteAllLines(Path, Defaults);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"could not write {Path}: {e.Message}; using the default taunts");
        }
    }

    internal static string Next()
    {
        Reload();
        if (_quotes.Count == 0)
            return null;
        int i = Pick(_quotes.Count, _last, Rng);
        _last = i;
        return _quotes[i];
    }

    /// <summary>A random index in [0, count) other than <paramref name="last"/> when count allows it.</summary>
    internal static int Pick(int count, int last, Random rng)
    {
        if (count == 1)
            return 0;
        int i = rng.Next(count - 1);
        return last >= 0 && last < count && i >= last ? i + 1 : i;
    }

    internal static List<string> Parse(IEnumerable<string> lines) =>
        lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();

    private static void Reload()
    {
        try
        {
            if (!File.Exists(Path))
                return;
            DateTime write = File.GetLastWriteTimeUtc(Path);
            if (write == _write)
                return;
            _write = write;
            _quotes = Parse(File.ReadAllLines(Path));
            _last = -1;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"could not read {Path}: {e.Message}; keeping the last taunts");
        }
    }
}
