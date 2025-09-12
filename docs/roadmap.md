# YARP Roadmap

### Supported YARP versions

[Latest releases](https://github.com/dotnet/yarp/releases)

| Version | Release Date | Latest Patch Version | End of Support |
| -- | -- | -- | -- |
| [YARP 2.3](https://github.com/dotnet/yarp/releases/tag/v2.3.0) | February 27, 2025 | [2.3.0](https://github.com/dotnet/yarp/releases/tag/v2.3.0) |                  |

### End-of-life YARP versions

| Version | Released date | Final Patch Version | End of support |
| -- | -- | -- | -- |
| [YARP 2.2](https://github.com/dotnet/yarp/releases/tag/v2.2.0) | September 3, 2024 | [2.2.0](https://github.com/dotnet/yarp/releases/tag/v2.2.0) | August 27, 2025  |
| [YARP 2.1](https://github.com/dotnet/yarp/releases/tag/v2.1.0) | November 17, 2023 | [2.1.0](https://github.com/dotnet/yarp/releases/tag/v2.1.0) | March 3, 2025    |
| [YARP 2.0](https://github.com/dotnet/yarp/releases/tag/v2.0.0) | February 14, 2023 | [2.0.1](https://github.com/dotnet/yarp/releases/tag/v2.0.1) | May 17, 2024     |
| [YARP 1.1](https://github.com/dotnet/yarp/releases/tag/v1.1.0) | May 2, 2022       | [1.1.2](https://github.com/dotnet/yarp/releases/tag/v1.1.2) | August 14, 2023  |
| [YARP 1.0](https://github.com/dotnet/yarp/releases/tag/v1.0.0) | November 9, 2021  | [1.0.1](https://github.com/dotnet/yarp/releases/tag/v1.0.1) | November 2, 2022 |

## Support

YARP support is provided by the product team - the engineers working on YARP - which is a combination of members from ASP.NET and the .NET library teams. We do not provide 24/7 support or 'carry pagers', but we generally have good coverage. Bugs should be reported in GitHub using the issue templates, and will typically be responded to within 24hrs. If you find a security issue we ask you to [report it via the Microsoft Security Response Center (MSRC)](https://github.com/dotnet/yarp/blob/main/SECURITY.md).

The support period for YARP releases is as follows:

| Release	| Issue Type | Support period |
| --- | ---| --- |
| Major or minor version | Security Bugs, Major behavior defects	| Until next GA + 6 Months |
| Patch version | Minor behavior defects	| Until next GA |
| Preview | Security Bugs, Major behavior defects | Until next preview |
| | All other | None - may be addressed by next preview |

For example, if 2 months after 1.3 (making up a number) is released, a security issue is found, then we will patch:
- 1.3 - its the latest release
- 1.2 - as it has 4 months of support remaining
- 1.1 - provided that 1.2 was released less than 6 months before

This support schedule is designed to provide a reasonable time period for customers to be able to update to new releases. 

### Building your own copy of a release

YARP is an open source project, so any customers that need fixes faster, or for older releases, are able to build their own copy of YARP. The build environment for YARP is included in the repo and maintained in sync with the source. Each release (GA and Preview) of YARP is tagged, which means that if you need to patch a specific release you can sync to the tag and build. For example you can rebuild v1.0.0 with:

```shell
git clone -b v1.0.0 https://github.com/dotnet/yarp.git yarp1_0_0
cd yarp1_0_0
restore.cmd 

<make your changes>

build.cmd -c release
```

This will produce the `Yarp.ReverseProxy.dll` into `artifacts/bin/Yarp.ReverseProxy/Release/net6.0`, and peer folder(s) for .NET 5 & Core 3.1. If you need to build a nuget package, that can be done with: 

```shell
pack.cmd -c release
```

The nuget package will be output to `artifacts/packages/release/Shipping`. 
