using System.CommandLine;
using SpecDrift.Core.Model;

namespace SpecDrift.Cli;

/// <summary>
/// Builds the CLI contract from <c>PROMPT.md</c> and turns raw arguments into a
/// <see cref="CliParseResult"/>. Parse errors are reported and mapped to exit code 2
/// here rather than left to System.CommandLine's default (1), because 1 is reserved
/// for "there are findings at or above --min-severity".
/// </summary>
public static class CliOptionsParser
{
    public static CliParseResult Parse(string[] args, TextWriter error)
    {
        var specOption = new Option<FileInfo>("--spec")
        {
            Description = "Path to the OpenAPI specification (JSON or YAML).",
            Required = true
        }.AcceptExistingOnly();

        var projectOption = new Option<DirectoryInfo>("--project")
        {
            Description = "Path to the root of the C# web API project to analyse.",
            Required = true
        }.AcceptExistingOnly();

        var dialectOption = new Option<string>("--dialect")
        {
            Description = "Override dialect auto-detection: WebApi2 or Core."
        }.AcceptOnlyFromAmong("WebApi2", "Core");

        var outOption = new Option<FileInfo>("--out")
        {
            Description = "Write the report to this file instead of stdout."
        };

        var noLlmOption = new Option<bool>("--no-llm")
        {
            Description = "Skip the LLM-assisted semantic review stage (SD100)."
        };

        var minSeverityOption = new Option<string>("--min-severity")
        {
            Description = "Minimum severity that affects the exit code: Low, Medium, or High.",
            DefaultValueFactory = _ => "Low"
        }.AcceptOnlyFromAmong("Low", "Medium", "High");

        var root = new RootCommand(
            "Spec Drift - reports where an OpenAPI specification and its C# implementation have diverged.")
        {
            specOption, projectOption, dialectOption, outOption, noLlmOption, minSeverityOption
        };

        var parseResult = root.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            foreach (var parseError in parseResult.Errors)
            {
                error.WriteLine(parseError.Message);
            }

            return new CliParseResult(Options: null, ExitCode: 2);
        }

        if (parseResult.Action is not null)
        {
            // --help or --version: let System.CommandLine's own action print and exit.
            return new CliParseResult(Options: null, ExitCode: parseResult.Invoke());
        }

        var dialectValue = parseResult.GetValue(dialectOption);
        var minSeverityValue = parseResult.GetValue(minSeverityOption) ?? "Low";

        var options = new CliOptions(
            Spec: parseResult.GetValue(specOption)!,
            Project: parseResult.GetValue(projectOption)!,
            Dialect: dialectValue is null ? null : Enum.Parse<DialectChoice>(dialectValue),
            Out: parseResult.GetValue(outOption),
            NoLlm: parseResult.GetValue(noLlmOption),
            MinSeverity: Enum.Parse<Severity>(minSeverityValue));

        return new CliParseResult(options, ExitCode: 0);
    }
}
