using Kingdom.NewEconomySimulator;
using Kingdom.NewEconomySimulator.Tests;

if (args.Any(argument => argument.StartsWith("--snapshot-", StringComparison.Ordinal) ||
                         argument is "--online-seconds" or "--offline-seconds"))
    return SnapshotParityCommand.Run(args);

var report = ValidationSuite.RunCore();
var json = args.Any(argument => StringComparer.OrdinalIgnoreCase.Equals(argument, "--json"));
var csv = args.Any(argument => StringComparer.OrdinalIgnoreCase.Equals(argument, "--csv"));
Console.WriteLine(csv ? ReportWriter.ToCsv(report) : json ? ReportWriter.ToJson(report) : ReportWriter.ToMarkdown(report));
return report.Passed ? 0 : 1;
