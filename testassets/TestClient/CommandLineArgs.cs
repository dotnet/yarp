// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace SampleClient;

internal sealed class CommandLineArgs
{
    private CommandLineArgs()
    {
    }

    public bool Help { get; private set; }

    public string Scenario { get; private set; }

    public string Target { get; private set; } = "https://localhost:1443/";

    public static CommandLineArgs Parse(string[] args)
    {
        var result = new CommandLineArgs();

        var i = 0;
        for (i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                case "-?":
                case "/?":
                    result.Help = true;
                    break;
                case "--scenario":
                case "-s":
                    result.Scenario = args[++i];
                    break;
                case "--target":
                case "-t":
                    result.Target = args[++i];
                    break;
            }
        }

        if (i < args.Length)
        {
            return ParseRemainder(result, args.AsSpan().Slice(i));
        }

        return result;

        static CommandLineArgs ParseRemainder(CommandLineArgs result, Span<string> remainder)
        {
            if (remainder.Length == 0)
            {
                throw new ArgumentException("Expected additional args.");
            }

            if (remainder.Length > 1)
            {
                throw new ArgumentException($"Unexpected arg '{remainder[1]}'.");
            }

            result.Scenario = remainder[0];
            return result;
        }
    }

    public static void ShowHelp()
    {
        Console.WriteLine("ReverseProxy SampleClient.\n");
        Console.WriteLine("--scenario <name>, -s <name>: Runs only the specified scenario.");
        Console.WriteLine(
            "--target <uri>, -t <uri>: Sets the target uri. By default, 'https://localhost:1443/' is used.");
        Console.WriteLine("--help, -h, -?, /?: Shows this help information.");
    }
}
