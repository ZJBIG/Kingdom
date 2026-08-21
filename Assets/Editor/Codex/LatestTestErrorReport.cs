using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

internal static class LatestTestErrorReportRegistration
{
    private const string ReportDirectoryName = "TestResults";
    private const string ReportFileName = "Latest-Test-Errors.txt";

    [InitializeOnLoadMethod]
    private static void Register()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>()
            .RegisterCallbacks(new LatestTestErrorReportCallbacks(), -100);
    }

    private sealed class LatestTestErrorReportCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                WriteReport(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }

    private static void WriteReport(ITestResultAdaptor result)
    {
        var failures = new List<ITestResultAdaptor>();
        CollectFailures(result, failures);

        var report = new StringBuilder();
        report.AppendLine("Kingdom Unity Test Runner - Latest Error Report");
        report.Append("Generated: ")
            .AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
        report.Append("Mode: ").AppendLine(result.Test.TestMode.ToString());
        report.Append("Result: ").AppendLine(result.ResultState);
        report.Append("Total: ").AppendLine(result.Test.TestCaseCount.ToString(CultureInfo.InvariantCulture));
        report.Append("Passed: ").AppendLine(result.PassCount.ToString(CultureInfo.InvariantCulture));
        report.Append("Failed: ").AppendLine(result.FailCount.ToString(CultureInfo.InvariantCulture));
        report.Append("Skipped: ").AppendLine(result.SkipCount.ToString(CultureInfo.InvariantCulture));
        report.Append("Inconclusive: ").AppendLine(result.InconclusiveCount.ToString(CultureInfo.InvariantCulture));
        report.Append("DurationSeconds: ")
            .AppendLine(result.Duration.ToString("0.###", CultureInfo.InvariantCulture));
        report.AppendLine();

        if (failures.Count == 0)
        {
            report.AppendLine("No failed tests.");
        }
        else
        {
            for (var index = 0; index < failures.Count; index++)
            {
                AppendFailure(report, failures[index], index + 1);
            }
        }

        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var reportDirectory = Path.Combine(projectRoot, ReportDirectoryName);
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, ReportFileName);
        File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
        Debug.Log($"Unity test error report overwritten: {reportPath}");
    }

    private static bool CollectFailures(
        ITestResultAdaptor result,
        ICollection<ITestResultAdaptor> failures)
    {
        var failedChildFound = false;
        if (result.HasChildren)
        {
            foreach (var child in result.Children)
            {
                failedChildFound |= CollectFailures(child, failures);
            }
        }

        if (result.TestStatus != UnityEditor.TestTools.TestRunner.Api.TestStatus.Failed)
        {
            return failedChildFound;
        }

        if (!failedChildFound)
        {
            failures.Add(result);
        }

        return true;
    }

    private static void AppendFailure(
        StringBuilder report,
        ITestResultAdaptor failure,
        int number)
    {
        report.Append("=== FAILURE ")
            .Append(number.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" ===");
        report.Append("Test: ").AppendLine(failure.FullName);
        report.Append("State: ").AppendLine(failure.ResultState);
        report.Append("DurationSeconds: ")
            .AppendLine(failure.Duration.ToString("0.###", CultureInfo.InvariantCulture));
        AppendSection(report, "Message", failure.Message);
        AppendSection(report, "StackTrace", failure.StackTrace);
        AppendSection(report, "Output", failure.Output);
        report.AppendLine();
    }

    private static void AppendSection(StringBuilder report, string heading, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        report.Append(heading).AppendLine(":");
        report.AppendLine(value.TrimEnd());
    }
}
