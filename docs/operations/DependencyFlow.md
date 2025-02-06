# ***Note that this document may be outdated as .NET 5 has already been released***

It is kept here as it may be useful in the future (.NET 6.0)

# Dependency Flow

We are aiming to ship YARP previews aligned with .NET 5, since we are working on changes to the runtime that will improve the experience for YARP. As part of that, our schedule for a preview milestone has three phases:

1. Open development and receiving new builds of .NET 5 - Commits are going to `main` and new builds of .NET 5 are coming regularly
2. Open development, .NET 5 builds frozen - .NET 5 branches about a month before release, so after they branch we will switch `main` to receive builds from that release branch. Our development will continue until approximately a week before release.
3. Branch and prepare for release - Prior to the release date, we'll branch and prepare for release. Note: in practice we haven't needed to create the release branch until the day before we plan to release. We've also not needed to set up dependency flow in our release branches because the runtime has finished producing new builds that close to release. Main always targets the runtime channel for the next release.

## Scheduling

We need to ship versions of our package that depend upon the **released** preview builds. In order to ensure we do that, we try to ship same-date or shortly after .NET 5 previews (see the [.NET Wiki (Internal)](https://aka.ms/dotnet-wiki) for schedule details).

## Dependency Flow Overview

*For full documentation on Arcade, Maestro and `darc`, see [the Arcade documentation](https://github.com/dotnet/arcade/tree/master/Documentation)*

We use the .NET Engineering System ([Arcade](https://github.com/dotnet/arcade)) to build this repo, since many of the contributors are part of the .NET team and we want to use nightly builds of .NET 5. Part of the engineering system is a service called "Maestro" which manages dependency flow between repositories. When one repository finishes building, it can automatically publish it's build to a Maestro "Channel". Other repos can subscribe to that channel to receive updated builds. Maestro will automatically open a PR to update dependencies in repositories that are subscribed to changes in dependent repositories.

Maestro can be queried and controlled using the `darc` command line tool. To use `darc` you will need to be a member of the [`dotnet/arcade-contrib` GitHub Team](https://github.com/orgs/dotnet/teams/arcade-contrib). To set up `darc`:

1. Run `.\eng\common\darc-init.ps1` to install the global tool.
2. Once installed, run `darc authenticate` and follow the instructions in the file opened in your editor to set up the necessary access token for Maestro. You should *only* need the Maestro token for the commands used here, but feel free to configure the other tokens as well.
3. Save and close the file, and `darc` will be ready to go.

Running `darc` with no args will show a list of commands. The `darc help [command]` command will give you help on a specific command.

Repositories can be configured to publish builds automatically to a certain channel, based on the branch. For example, most .NET repos are set up like this:

* Builds out of `main` are auto-published to the `.NET 5 Dev` channel
* Builds out of `release/5.0.0-preview.X` are auto-published to the `.NET 5 Preview X` channel (where `X` is some preview number)

To see the current mappings for a repository, you can run `darc get-default-channels --source-repo [repo]`, where `[repo]` is any substring that matches a full GitHub URL for a repo in the system. The easiest way to use `[repo]` is to just specify the `[owner]/[name]` form for a repo. For example:

```shell
> darc get-default-channels --source-repo dotnet/aspnetcore
(912)  https://github.com/dotnet/aspnetcore @ release/3.1 -> .NET Core 3.1 Release
(913)  https://github.com/dotnet/aspnetcore @ main -> .NET 5 Dev
(1160) https://github.com/dotnet/aspnetcore @ faster-publishing -> General Testing
(1003) https://github.com/dotnet/aspnetcore @ wtgodbe/NonStablev2 -> General Testing
(1089) https://github.com/dotnet/aspnetcore @ generate-akams-links -> General Testing
(1021) https://github.com/dotnet/aspnetcore @ NonStablePackageVersion -> General Testing
(1018) https://github.com/dotnet/aspnetcore @ wtgodbe/Checksum3x -> General Testing
(1281) https://github.com/dotnet/aspnetcore @ wtgodbe/FixChecksums -> General Testing
(914)  https://github.com/dotnet/aspnetcore @ blazor-wasm -> .NET Core 3.1 Blazor Features
(1252) https://github.com/dotnet/aspnetcore @ release/5.0-preview3 -> .NET 5 Preview 3
(1301) https://github.com/dotnet/aspnetcore @ release/5.0-preview4 -> .NET 5 Preview 4
(916)  https://github.com/dotnet/aspnetcore-tooling @ release/3.1 -> .NET Core 3.1 Release
(917)  https://github.com/dotnet/aspnetcore-tooling @ master -> .NET 5 Dev
(1178) https://github.com/dotnet/aspnetcore-tooling @ release/vs16.6-preview2 -> General Testing
(1282) https://github.com/dotnet/aspnetcore-tooling @ wtgodbe/DontPublishDebug -> General Testing
(1253) https://github.com/dotnet/aspnetcore-tooling @ release/5.0-preview3 -> .NET 5 Preview 3
(1302) https://github.com/dotnet/aspnetcore-tooling @ release/5.0-preview4 -> .NET 5 Preview 4
```

Subscriptions are managed using the `get-subscriptions`, `add-subscription` and `update-subscription` commands. You can view all subscriptions in the system by running `darc get-subscription`. You can also filter subscriptions by the source and target using the `--source-repo [repo]` and `--target-repo [repo]` arguments. For example, to see everything that `dotnet/yarp` is subscribed to:

```shell
> darc get-subscriptions --target-repo dotnet/yarp
https://github.com/dotnet/arcade (.NET Eng - Latest) ==> 'https://github.com/dotnet/yarp' ('main')
  - Id: 642e03bf-3679-4569-fcfc-08d7d0f045ee
  - Update Frequency: EveryWeek
  - Enabled: True
  - Batchable: False
  - Merge Policies:
    Standard
https://github.com/dotnet/runtime (.NET 5 Preview 4) ==> 'https://github.com/dotnet/yarp' ('main')
  - Id: 763f49c1-8016-44b6-8810-08d7e1727af8
  - Update Frequency: EveryBuild
  - Enabled: True
  - Batchable: False
  - Merge Policies:
    Standard
```

To add a new subscription, run `darc add-subscription` with no arguments. An editor window will open with a TODO script like this:

```
Channel: <required>
Source Repository URL: <required>
Target Repository URL: <required>
Target Branch: <required>
Update Frequency: <'none', 'everyDay', 'everyBuild', 'twiceDaily', 'everyWeek'>
Batchable: False
Merge Policies: []
```

A number of comments will also be present, describing available values and what they do. Fill these fields in, for example:

```
Channel: .NET 5 Dev
Source Repository URL: https://github.com/dotnet/runtime
Target Repository URL: https://github.com/dotnet/yarp
Target Branch: main
Update Frequency: everyBuild
Batchable: False
Merge Policies:
- Name: Standard
```

Save and exit the editor and the subscription will be created.

Similarly, you can edit an existing subscription by using `darc update-subscription --id [ID]` (get the `[ID]` value from `get-subscriptions`). This will open the same TODO script, but with the current values filled in. Just update them, then save and exit to update.

## Prerequisites

* Properly configured `darc` global tool, configured with a Maestro authentication token.

## When .NET 5 branches for a preview

When .NET 5 branches for a preview, we need to switch the "channel" from which we are getting builds. See [Dependency Flow Onboarding](https://github.com/dotnet/arcade/blob/master/Documentation/DependencyFlowOnboarding.md) for more information on channels.

To do this, run the following commands:

1. Run `darc get-subscriptions --target-repo dotnet/yarp --source-repo dotnet/runtime` to get the subscription from `dotnet/runtime` to `dotnet/yarp`
2. Copy the `Id` value
3. Run `darc update-subscription --id [Id]`. An editor will open.
4. Change the value of the `Channel` field to `.NET 5 Preview X` (where `X` is the preview we are remaining on)
4. Change the value of the `Update Frequency` field to `EveryBuild` (since there should be relatively few builds and we want to be sure we're on the latest).
5. Save and close the file in your editor, `darc` will proceed to make the update.
6. Answer `y` to `Trigger this subscription immediately?` and a PR will be opened to update versions to the latest ones in that channel.
7. Merge the PR as soon as feasible.

## When we are ready to branch

Follow the initial branching steps from [Branching.md](Branching.md).

Set up dependency flow so we continue getting new runtime bits if necessary:

1. Run `darc add-subscription`
2. Fill in the template that opens in your editor as follows:
    * `Channel` = `.NET 5 Preview X` where `X` is the .NET 5 preview that matches the YARP preview
    * `Source Repository URL` = `https://github.com/dotnet/runtime`
    * `Target Repository URL` = `https://github.com/dotnet/yarp`
    * `Target Branch` = `release/1.0.0-previewX` (where `X` is the YARP preview number; this should be the same as the branch you created above)
    * `Update Frequency` = `everyBuild` (Builds will be rare in this channel and we'll want every one)
    * `Merge Policies` is a multiline value, it should look like this:

```
Merge Policies:
- Name: Standard
  Properties: {}
```

3. Save and close the editor window.

Restore the `main` branch to pulling the latest bits from .NET 5:

1. Run `darc get-subscriptions --target-repo dotnet/yarp --source-repo dotnet/runtime` to get the subscription from `dotnet/runtime` to `dotnet/yarp`
2. Copy the `Id` value
3. Run `darc update-subscription --id [Id]`. An editor will open.
4. Change the value of the `Channel` field to `.NET 5 Dev`
4. Change the value of the `Update Frequency` field back to `EveryWeek` to reduce PR noise.
5. Save and close the file in your editor, `darc` will proceed to make the update.
6. Answer `y` to `Trigger this subscription immediately?` and a PR will be opened to update versions to the latest ones in that channel.
7. Merge the PR as soon as feasible.