using SpecDrift.Cli;

var parseResult = CliOptionsParser.Parse(args, Console.Error);
if (parseResult.Options is null)
{
    return parseResult.ExitCode;
}

var options = parseResult.Options;

if (!options.NoLlm && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SPECDRIFT_API_KEY")))
{
    Console.Error.WriteLine(
        "SPECDRIFT_API_KEY is not set. Semantic review (LLM-assisted SD100) needs it; " +
        "set the environment variable, or pass --no-llm to skip semantic review.");
    return 2;
}

// Slices 2-9 (spec reader, code reader, comparer, report writer, LLM review) don't exist
// yet. This just proves the argument contract is wired end to end.
Console.WriteLine($"Spec:         {options.Spec.FullName}");
Console.WriteLine($"Project:      {options.Project.FullName}");
Console.WriteLine($"Dialect:      {(options.Dialect is null ? "(auto-detect)" : options.Dialect.ToString())}");
Console.WriteLine($"Output:       {(options.Out is null ? "(stdout)" : options.Out.FullName)}");
Console.WriteLine($"LLM enabled:  {!options.NoLlm}");
Console.WriteLine($"Min severity: {options.MinSeverity}");

return 0;
