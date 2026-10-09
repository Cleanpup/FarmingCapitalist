using FarmingCapitalist.Workers;

static void Check(bool result, string message)
{
    if (!result) throw new Exception(message);
}

List<string> existing = new();
Random random = new(12);
for (int i = 0; i < 12; i++)
{
    string suggested = WorkerNamePolicy.ChooseDefault(existing, random);
    Check(!existing.Contains(suggested), "Suggested name repeated while unused names remain");
    existing.Add(suggested);
}
Check(WorkerNamePolicy.TryNormalize("  Maple  ", out string custom) && custom == "Maple",
    "Custom name was not preserved and trimmed");
Check(!WorkerNamePolicy.TryNormalize("   ", out _), "Blank name accepted");
Check(!WorkerNamePolicy.TryNormalize("Line\nBreak", out _), "Control character accepted");
Check(!WorkerNamePolicy.TryNormalize(new string('A', WorkerNamePolicy.MaximumLength + 1), out _),
    "Overlong name accepted");
Console.WriteLine("Worker name checks passed.");
