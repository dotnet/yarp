// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SampleClient;
using SampleClient.Scenarios;

CommandLineArgs parsedArgs = null;
try
{
    parsedArgs = CommandLineArgs.Parse(args);
}
catch (ArgumentException)
{
    // Do nothing, we will show help right after.
}

if (parsedArgs is null || parsedArgs.Help)
{
    CommandLineArgs.ShowHelp();
    return 1;
}

var scenarioFactories = new Dictionary<string, Func<IScenario>>(StringComparer.OrdinalIgnoreCase) {
    {"Http1", () => new Http1Scenario()},
    {"Http2", () => new Http2Scenario()},
    {"Http2PostExpectContinue", () => new Http2PostExpectContinueScenario()},
    {"RawUpgrade", () => new RawUpgradeScenario()},
    {"WebSockets", () => new WebSocketsScenario()},
    {"SessionAffinity", () => new SessionAffinityScenario()}
};

if (string.IsNullOrEmpty(parsedArgs.Scenario))
{
    // Execute all scenarios
    var success = true;
    foreach (var kvp in scenarioFactories.OrderBy(kvp => kvp.Key))
    {
        Console.WriteLine();
        Console.WriteLine($"Executing scenario '{kvp.Key}'...");
        try
        {
            var scenario = kvp.Value();
            await scenario.ExecuteAsync(parsedArgs, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected exception: {ex}");
            success = false;
        }
    }

    Console.WriteLine();
    Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"All scenarios completed {(success ? "successfully" : "with errors")}.");
    Console.ResetColor();
    Console.WriteLine("Press any key to exit.");
    Console.ReadKey();
    return success ? 0 : 1;
}

if (!scenarioFactories.TryGetValue(parsedArgs.Scenario, out var scenarioFactory))
{
    Console.WriteLine($"Unknown scenario '{parsedArgs.Scenario}'. Supported values: ");
    foreach (var scenarioName in scenarioFactories.Keys.OrderBy(k => k))
    {
        Console.WriteLine($"   {scenarioName}");
    }

    Console.WriteLine();
    return 1;
}

Console.WriteLine($"Executing scenario '{parsedArgs.Scenario}'.");
try
{
    var scenario = scenarioFactory();
    await scenario.ExecuteAsync(parsedArgs, CancellationToken.None);
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected exception: {ex}");
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("All scenarios completed successfully!");
Console.ResetColor();
Console.WriteLine("Press any key to exit.");
Console.ReadKey();
return 0;
