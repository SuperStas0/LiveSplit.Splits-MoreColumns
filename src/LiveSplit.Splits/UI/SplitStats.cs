using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using LiveSplit.Model;

namespace LiveSplit.UI.Components;

/// <summary>
/// Tracks per split how many attempts reached it and how many completed it.
/// The counters live in the splits file (.lss) as one permanent run variable keyed by split name,
/// so they follow a split when splits are reordered. Everything is recalculated from the
/// segment history when the variable is missing, or when attempts were made without this component.
/// </summary>
public class SplitStats
{
    public const string VariableName = "SplitsReachStats";

    private const string FormatVersion = "1";

    private sealed class Entry
    {
        public int Reached { get; set; }
        public int Completed { get; set; }
    }

    private IRun cachedRun;
    private string cachedText;
    private string[] cachedNames = [];
    private int cachedHistoryCount;
    private int cachedLastHistoryIndex;
    private Entry[] entries = [];
    private int attempts;
    private int lastAttempt = int.MinValue;

    public void Commit(IRun run)
    {
        if (Sync(run))
        {
            run.HasChanged = true;
        }
    }

    public void GetCounts(LiveSplitState state, int splitIndex, out int reached, out int completed, out int totalAttempts)
    {
        Sync(state.Run);

        int current = state.CurrentSplitIndex;
        reached = 0;
        completed = 0;
        totalAttempts = attempts + (current >= 0 ? 1 : 0);

        if (splitIndex >= 0 && splitIndex < entries.Length)
        {
            reached = entries[splitIndex].Reached + (splitIndex <= current ? 1 : 0);
            completed = entries[splitIndex].Completed + (splitIndex < current ? 1 : 0);
        }
    }

    private bool Sync(IRun run)
    {
        string text = ReadText(run);
        if (ReferenceEquals(run, cachedRun) && text == cachedText && NamesMatch(run) && HistoryMatches(run))
        {
            return false;
        }

        return Rebuild(run, text);
    }

    private bool NamesMatch(IRun run)
    {
        if (cachedNames.Length != run.Count)
        {
            return false;
        }

        for (int i = 0; i < cachedNames.Length; i++)
        {
            if (!string.Equals(cachedNames[i], run[i].Name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool HistoryMatches(IRun run)
    {
        int count = run.AttemptHistory.Count;
        return count == cachedHistoryCount
            && (count == 0 || run.AttemptHistory[count - 1].Index == cachedLastHistoryIndex);
    }

    private bool Rebuild(IRun run, string text)
    {
        Dictionary<string, Entry> stored = Parse(text, out int storedAttempts, out int storedLastAttempt);
        bool hasStored = stored != null;
        string[] keys = GetKeys(run);
        List<int> attemptIds = GetAttemptIds(run);

        var newEntries = new Entry[keys.Length];
        var seed = new bool[keys.Length];
        var catchUp = new bool[keys.Length];
        bool anySeed = false;
        for (int i = 0; i < keys.Length; i++)
        {
            if (hasStored && stored.TryGetValue(keys[i], out Entry known))
            {
                newEntries[i] = known;
                catchUp[i] = true;
            }
            else
            {
                newEntries[i] = new Entry();

                // Without stored data, estimate from the segment history. A split with no history
                // while other data exists is new and starts at zero.
                seed[i] = !hasStored || run[i].SegmentHistory.Count > 0;
                anySeed |= seed[i];
            }
        }

        int previousLastAttempt = hasStored ? storedLastAttempt : int.MinValue;
        int newAttempts = hasStored ? storedAttempts : 0;
        int counted = 0;
        var apply = new bool[keys.Length];
        foreach (int attemptId in attemptIds)
        {
            bool isNew = attemptId > previousLastAttempt;
            if (!isNew && !anySeed)
            {
                continue;
            }

            for (int i = 0; i < apply.Length; i++)
            {
                apply[i] = seed[i] || (isNew && catchUp[i]);
            }

            CountAttempt(run, attemptId, newEntries, apply);

            if (isNew)
            {
                newAttempts++;
                counted++;
            }
        }

        cachedRun = run;
        cachedNames = run.Select(x => x.Name).ToArray();
        cachedHistoryCount = run.AttemptHistory.Count;
        cachedLastHistoryIndex = cachedHistoryCount > 0 ? run.AttemptHistory[cachedHistoryCount - 1].Index : 0;
        entries = newEntries;
        attempts = newAttempts;
        lastAttempt = attemptIds.Count > 0 ? Math.Max(previousLastAttempt, attemptIds[^1]) : previousLastAttempt;

        string newText = Serialize(keys);
        if (newText != text && (hasStored || attemptIds.Count > 0))
        {
            WriteText(run, newText);
        }

        cachedText = ReadText(run);

        return counted > 0;
    }

    /// <summary>
    /// Adds one attempt to the counters using the segment history of the current split order.
    /// A split was passed when it has a history entry for the attempt (this includes skipped splits).
    /// A split was reached when it was passed, or when it is the first split that was not passed.
    /// </summary>
    private static void CountAttempt(IRun run, int attemptId, Entry[] targets, bool[] apply)
    {
        bool gapFound = false;
        for (int i = 0; i < targets.Length; i++)
        {
            bool passed = run[i].SegmentHistory.ContainsKey(attemptId);
            bool reached = passed || !gapFound;
            gapFound |= !passed;

            if (!apply[i])
            {
                continue;
            }

            if (reached)
            {
                targets[i].Reached++;
            }

            if (passed)
            {
                targets[i].Completed++;
            }
        }
    }

    /// <summary>
    /// Real attempt ids. Ids below 1 are pseudo attempts the core creates when importing history.
    /// </summary>
    private static List<int> GetAttemptIds(IRun run)
    {
        var ids = new SortedSet<int>(run.AttemptHistory.Select(x => x.Index));
        foreach (ISegment segment in run)
        {
            ids.UnionWith(segment.SegmentHistory.Keys);
        }

        return ids.Where(x => x >= 1).ToList();
    }

    private static string[] GetKeys(IRun run)
    {
        var occurrences = new Dictionary<string, int>();
        var keys = new string[run.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            string name = run[i].Name ?? string.Empty;
            occurrences.TryGetValue(name, out int occurrence);
            occurrences[name] = occurrence + 1;
            keys[i] = Uri.EscapeDataString(name) + "," + occurrence.ToString(CultureInfo.InvariantCulture);
        }

        return keys;
    }

    private string Serialize(string[] keys)
    {
        var builder = new StringBuilder();
        builder.Append(FormatVersion);
        builder.Append('|').Append(attempts.ToString(CultureInfo.InvariantCulture));
        builder.Append('|').Append(lastAttempt.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < keys.Length; i++)
        {
            builder.Append('|').Append(keys[i]);
            builder.Append(',').Append(entries[i].Reached.ToString(CultureInfo.InvariantCulture));
            builder.Append(',').Append(entries[i].Completed.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static Dictionary<string, Entry> Parse(string text, out int storedAttempts, out int storedLastAttempt)
    {
        storedAttempts = 0;
        storedLastAttempt = int.MinValue;

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        string[] parts = text.Split('|');
        if (parts.Length < 3
            || parts[0] != FormatVersion
            || !TryParseInt(parts[1], out storedAttempts)
            || !TryParseInt(parts[2], out storedLastAttempt))
        {
            return null;
        }

        var result = new Dictionary<string, Entry>();
        for (int i = 3; i < parts.Length; i++)
        {
            string[] fields = parts[i].Split(',');
            if (fields.Length != 4
                || !TryParseInt(fields[2], out int reached)
                || !TryParseInt(fields[3], out int completed))
            {
                return null;
            }

            result[fields[0] + "," + fields[1]] = new Entry { Reached = reached, Completed = completed };
        }

        return result;
    }

    private static bool TryParseInt(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string ReadText(IRun run)
    {
        return run.Metadata.CustomVariables.TryGetValue(VariableName, out CustomVariable variable) ? variable.Value : null;
    }

    private static void WriteText(IRun run, string text)
    {
        CustomVariable variable = run.Metadata.GetOrAddCustomVariable(VariableName);
        variable.AsPermanent();
        variable.Value = text;
    }
}