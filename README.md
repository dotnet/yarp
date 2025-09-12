![YARP Icon](assets/icon.png)
# Welcome to the YARP project

YARP (which stands for "Yet Another Reverse Proxy") is a project to create a reverse proxy server. We found a bunch of internal teams at Microsoft who were either building a reverse proxy for their service or had been asking about APIs and tech for building one, so we decided to get them all together to work on a common solution, this project.

YARP is a reverse proxy toolkit for building fast proxy servers in .NET using the infrastructure from ASP.NET and .NET. The key differentiator for YARP is that it's been designed to be easily customized and tweaked to match the specific needs of each deployment scenario. 

We expect YARP to ship as a library and project template that together provide a robust, performant proxy server. Its pipeline and modules are designed so that you can then customize the functionality for your needs. For example, while YARP supports configuration files, we expect that many users will want to manage the configuration programmatically based on their own backend configuration management system, YARP will provide a configuration API to enable that customization in-proc.  YARP is designed with customizability as a primary scenario, rather than requiring you to break out to script or having to rebuild from source.

# Getting started

- See our [Getting Started](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/getting-started) docs.
- Try our [previews](https://github.com/dotnet/yarp/releases).
- Try our latest [daily build](/docs/DailyBuilds.md).
- See our [support policy](/docs/roadmap.md).

# Updates

For regular updates, see our [releases page](https://github.com/dotnet/yarp/releases). Subscribe to release notifications on this repository to be notified of future updates (Watch -> Custom -> Releases).

If you want to live on the bleeding edge, you can pickup the [daily builds](/docs/DailyBuilds.md).

# Build

To build the repo, you should only need to run `build.cmd` (on Windows) or `build.sh` (on Linux or macOS). The script will download the .NET SDK and build the solution.

For VS on Windows, install the latest [VS 2022](https://visualstudio.microsoft.com/downloads/) release and then run the `startvs.cmd` script to launch Visual Studio using the appropriate local copy of the .NET SDK.

To set up local development with Visual Studio, Visual Studio for Mac or Visual Studio Code, you need to put the local copy of the .NET SDK in your `PATH` environment variable. Our `Restore` script fetches the latest build of .NET and installs it to a `.dotnet` directory *within* this repository.

We provide some scripts to set all this up for you. Just follow these steps:

1. Run the `restore.cmd`/`restore.sh` script to fetch the required .NET SDK locally (to the `.dotnet` directory within this repo)
1. "Dot-source" the `activate` script to put the local .NET SDK on the PATH
    1. For PowerShell, run: `. .\activate.ps1` (note the leading `. `, it is required!)
    1. For Linux/macOS/WSL, run: `. ./activate.sh`
    1. For CMD, there is no supported script. You can manually add the `.dotnet` directory **within this repo** to your `PATH`. Ensure `where dotnet` shows a path within this repository!
1. Launch VS, VS for Mac, or VS Code!

When you're done, you can run the `deactivate` function to undo the changes to your `PATH`.

If you're having trouble building the project, or developing in Visual Studio, please file an issue to let us know and we'll help out (and fix our scripts/tools as needed)!

# Testing

The command to build and run all tests: `build.cmd/sh -test`.
To run specific test you may use XunitMethodName property: `dotnet build /t:Test /p:XunitMethodName={FullyQualifiedNamespace}.{ClassName}.{MethodName}`.
The tests can also be run from Visual Studio if launched using `startvs.cmd`.

# Roadmap

see [docs/roadmap.md](/docs/roadmap.md)

# Reporting security issues and bugs

Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC) at `secure@microsoft.com`. You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including [the MSRC PGP key](https://www.microsoft.com/msrc/pgp-key-msrc), can be found at the [Microsoft Security Response Center](https://www.microsoft.com/msrc).

# Contributing

This project welcomes contributions and suggestions.
Check out the [contributing](CONTRIBUTING.md) page for more info.

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information, see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).
