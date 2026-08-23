using SpecDrift.Cli;
using SpecDrift.Core.CodeReading;
using SpecDrift.Core.Comparing;
using SpecDrift.Core.Model;
using SpecDrift.Core.Reporting;
using SpecDrift.Core.SemanticReview;
using SpecDrift.Core.SpecReading;

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

try
{
    var dialectSource = options.Dialect is null ? DialectSource.Detected : DialectSource.Specified;
    var dialect = options.Dialect ?? DialectDetector.Detect(options.Project.FullName);
    var descriptor = dialect switch
    {
        DialectChoice.WebApi2 => DialectDescriptors.WebApi2,
        DialectChoice.Core => DialectDescriptors.Core,
        _ => throw new NotSupportedException($"Unsupported dialect: {dialect}")
    };

    SpecReadResult specResult;
    using (var specStream = options.Spec.OpenRead())
    {
        specResult = SpecReader.Read(specStream);
    }

    var codeResult = CodeReader.Read(options.Project.FullName, descriptor);
    var comparison = Comparer.Compare(specResult, codeResult, descriptor.Dialect);

    var allFindings = new List<DriftFinding>(comparison.Findings);
    var limitations = new List<string>(codeResult.Limitations);

    if (options.NoLlm)
    {
        limitations.Add("Semantic review (SD100) was skipped (--no-llm).");
    }
    else
    {
        using var llmClient = new AnthropicLlmClient(Environment.GetEnvironmentVariable("SPECDRIFT_API_KEY")!);
        var semanticFindings = await SemanticReviewer.ReviewAsync(comparison.SemanticQuestions, llmClient, descriptor.Dialect);
        allFindings.AddRange(semanticFindings);
    }

    var findings = allFindings
        .Where(f => f.Severity >= options.MinSeverity)
        .OrderByDescending(f => f.Severity)
        .ThenBy(f => f.Code, StringComparer.Ordinal)
        .ThenBy(f => f.Title, StringComparer.Ordinal)
        .ToList();

    var report = ReportWriter.Write(new ReportContext(descriptor.Dialect, dialectSource, specResult.SpecVersion, findings, limitations));

    if (options.Out is null)
    {
        Console.Write(report);
    }
    else
    {
        File.WriteAllText(options.Out.FullName, report);
        Console.WriteLine($"Report written to {options.Out.FullName}");
    }

    return findings.Count > 0 ? 1 : 0;
}
catch (SpecParseException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (DialectDetectionException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (LlmClientException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Spec Drift failed: {ex.Message}");
    return 2;
}
