namespace FarmingCapitalist.Workers;

internal static class WorkerNamePolicy
{
    public const int MaximumLength = 20;

    private static readonly string[] DefaultNames =
    {
        "Avery", "Bailey", "Blair", "Cameron", "Casey", "Devon", "Ellis", "Emery",
        "Finley", "Harper", "Jules", "Kai", "Kendall", "Lane", "Marley", "Morgan",
        "Parker", "Quinn", "Reese", "Riley", "Rowan", "Sage", "Sawyer", "Wren",
    };

    public static string ChooseDefault(IEnumerable<string> existingNames, Random random)
    {
        HashSet<string> used = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] unused = DefaultNames.Where(name => !used.Contains(name)).ToArray();
        string[] choices = unused.Length > 0 ? unused : DefaultNames;
        return choices[random.Next(choices.Length)];
    }

    public static bool TryNormalize(string? input, out string name)
    {
        name = input?.Trim() ?? string.Empty;
        return name.Length is > 0 and <= MaximumLength && !name.Any(char.IsControl);
    }
}
