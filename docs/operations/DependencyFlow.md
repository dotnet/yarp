# Dependency Flow

## Dependency Flow Overview

*For full documentation on Arcade, Maestro and `darc`, see [the Arcade documentation](https://github.com/dotnet/arcade/tree/main/Documentation)*

We use the .NET Engineering System ([Arcade](https://github.com/dotnet/arcade)) to build this repo. Part of the engineering system is a service called "Maestro" which manages dependency flow between repositories. When one repository finishes building, it can automatically publish it's build to a Maestro "Channel". Other repos can subscribe to that channel to receive updated builds. Maestro will automatically open a PR to update dependencies in repositories that are subscribed to changes in dependent repositories.

Maestro can be queried and controlled using the `darc` command line tool. To use `darc` you will need to be a member of the [`dotnet/arcade-contrib` GitHub Team](https://github.com/orgs/dotnet/teams/arcade-contrib). To set up `darc`:

1. Run `.\eng\common\darc-init.ps1` to install the global tool.
2. Once installed, run `darc authenticate` and follow the instructions.

Running `darc` with no args will show a list of commands. The `darc help [command]` command will give you help on a specific command.

Repositories can be configured to publish builds automatically to a certain channel, based on the branch.
To see the current mappings for a repository, you can run `darc get-default-channels --source-repo [repo]`, where `[repo]` is any substring that matches a full GitHub URL for a repo in the system. The easiest way to use `[repo]` is to just specify the `[owner]/[name]` form for a repo. For example:

```shell
> darc get-default-channels --source-repo dotnet/aspnetcore
(3796) https://github.com/dotnet/aspnetcore @ release/6.0 -> .NET 6
(5027) https://github.com/dotnet/aspnetcore @ release/8.0 -> .NET 8
(5731) https://github.com/dotnet/aspnetcore @ release/9.0 -> .NET 9
(5732) https://github.com/dotnet/aspnetcore @ main -> .NET 10
(6050) https://github.com/dotnet/aspnetcore @ release/10.0-preview1 -> .NET 10 Preview 1
```

Subscriptions are managed using the `get-subscriptions`, `add-subscription` and `update-subscription` commands. You can view all subscriptions in the system by running `darc get-subscription`. You can also filter subscriptions by the source and target using the `--source-repo [repo]` and `--target-repo [repo]` arguments. For example, to see everything that `dotnet/yarp` is subscribed to:

```shell
> darc get-subscriptions --target-repo dotnet/yarp
https://github.com/dotnet/arcade (.NET Eng - Latest) ==> 'https://github.com/dotnet/yarp' ('main')
  - Id: 1751e896-c0f1-4247-3909-08d8c8762e9e
  - Update Frequency: EveryWeek
  - Enabled: True
  - Batchable: False
  - PR Failure Notification tags:
  - Source-enabled: False
  - Merge Policies:
    Standard
https://github.com/dotnet/arcade (.NET Eng - Latest) ==> 'https://github.com/dotnet/yarp' ('release/2.2')
  - Id: ebd75d9f-8988-4f50-bd1d-83dfc79fb7ba
  - Update Frequency: EveryWeek
  - Enabled: True
  - Batchable: False
  - PR Failure Notification tags:
  - Source-enabled: False
  - Merge Policies:
    Standard
```

To add a new subscription, run `darc add-subscription` with no arguments. An editor window will open with a TODO script like this:

```yaml
Channel: <required>
Source Repository URL: <required>
Target Repository URL: <required>
Target Branch: <required>
Update Frequency: <'none', 'everyDay', 'everyBuild', 'twiceDaily', 'everyWeek'>
Batchable: False
Merge Policies: []
```

A number of comments will also be present, describing available values and what they do. Fill these fields in, for example:

```yaml
Channel: .NET Eng - Latest
Source Repository URL: https://github.com/dotnet/arcade
Target Repository URL: https://github.com/dotnet/yarp
Target Branch: release/42
Update Frequency: EveryWeek
Batchable: False
Merge Policies:
- Name: Standard
```

Save and exit the editor and the subscription will be created.

Similarly, you can edit an existing subscription by using `darc update-subscription --id [ID]` (get the `[ID]` value from `get-subscriptions`). This will open the same TODO script, but with the current values filled in. Just update them, then save and exit to update.

## Prerequisites

* Properly configured `darc` global tool, including having run `darc authenticate`.

## When we are ready to branch

Follow the initial branching steps from [Branching.md](Branching.md).

YARP uses darc to consume up-to-date Arcade bits every week.
Set up dependency flow for the new branch:

1. Run `darc add-subscription`
2. Fill in the template that opens in your editor as follows:
    * `Channel` = `.NET Eng - Latest`
    * `Source Repository URL` = `https://github.com/dotnet/arcade`
    * `Target Repository URL` = `https://github.com/dotnet/yarp`
    * `Target Branch` = `release/X` (where `X` is the YARP release version)
    * `Update Frequency` = `EveryWeek`
    * `Merge Policies` is a multiline value, it should look like this:
      ```yaml
      Merge Policies:
      - Name: Standard
        Properties: {}
      ```
3. Save and close the editor window.