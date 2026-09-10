using Kingdom.NewEconomySimulator;
using Kingdom.NewEconomySimulator.Tests;

var report = ValidationSuite.RunCore();
var json = args.Any(argument => StringComparer.OrdinalIgnoreCase.Equals(argument, "--json"));
var csv = args.Any(argument => StringComparer.OrdinalIgnoreCase.Equals(argument, "--csv"));
Console.WriteLine(csv ? ReportWriter.ToCsv(report) : json ? ReportWriter.ToJson(report) : ReportWriter.ToMarkdown(report));
return report.Passed ? 0 : 1;
