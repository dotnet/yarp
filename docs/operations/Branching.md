# Branching Tasks

When we are ready to branch our code, we first need to create the branch:

1. In a local clone, run `git checkout main` and `git pull origin main` to make sure you have the latest `main`
1. Run `git checkout -b release/1.1.0-previewX` where `X` is the YARP preview number. When releasing a non-preview version, use `release/1.1` instead of `release/1.1.0` so that the branch can be used for future patches.
1. If you are releasing a non-preview version:
    - Set the `PreReleaseVersionLabel` in [`eng/Versions.props`] to `rtw`.
    - Run `build.cmd -pack`
    - Verify that the packages in `artifacts/packages/Debug/Shipping` do not have a suffix after the intended version. For example the name should be `Yarp.ReverseProxy.1.1.0.nupkg` and not `Yarp.ReverseProxy.1.1.0-dev.nupkg`.
1. Run `git push origin release/1.1.0-previewX` to push the branch to the server.

Update branding in `main`:

1. Edit the file [`eng/Versions.props`]
2. Set `PreReleaseVersionLabel` to `preview.X` (where `X` is the next preview number)
3. Send a PR and merge it ASAP (auto-merge is your friend).

Update the runtimes and SDKs in `global.json` in `main`:

Check that the global.json includes the latest 8.0 runtime versions from [here](https://dotnet.microsoft.com/download/dotnet/8.0), and 9.0 from [here](https://dotnet.microsoft.com/download/dotnet/9.0).

[`eng/Versions.props`]: ../../eng/Versions.props