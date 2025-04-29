# Releasing YARP

This document provides a guide on how to release a preview of YARP.

To keep track of the process, open a [release checklist issue](https://github.com/dotnet/yarp/issues/new?title=Preview%20X%20release%20checklist&body=See%20%5BRelease.md%5D%28https%3A%2F%2Fgithub.com%2Fdotnet%2Fyarp%2Fblob%2Fmain%2Fdocs%2Foperations%2FRelease.md%29%20for%20detailed%20instructions.%0A%0A-%20%5B%20%5D%20Ensure%20there%27s%20a%20release%20branch%20created%20%28see%20%5BBranching%5D%28https%3A%2F%2Fgithub.com%2Fdotnet%2Fyarp%2Fblob%2Fmain%2Fdocs%2Foperations%2FBranching.md%29%29%0A-%20%5B%20%5D%20Ensure%20the%20%60Version.props%60%20has%20the%20%60PreReleaseVersionLabel%60%20updated%20to%20the%20next%20preview%0A-%20%5B%20%5D%20Identify%20and%20validate%20the%20build%20on%20the%20%60dotnet-yarp-official%60%20pipeline%0A-%20%5B%20%5D%20Release%20the%20build%0A-%20%5B%20%5D%20Tag%20the%20commit%0A-%20%5B%20%5D%20Draft%20release%20notes%0A-%20%5B%20%5D%20Publish%20release%20notes%0A-%20%5B%20%5D%20Close%20the%20%5Bold%20milestone%5D%28https%3A%2F%2Fgithub.com%2Fdotnet%2Fyarp%2Fmilestones%29%0A-%20%5B%20%5D%20Announce%20on%20social%20media%0A-%20%5B%20%5D%20Set%20the%20preview%20branch%20to%20protected%0A-%20%5B%20%5D%20Delete%20the%20%5Bprevious%20preview%20branch%5D%28https%3A%2F%2Fgithub.com%2Fdotnet%2Fyarp%2Fbranches%29%0A-%20%5B%20%5D%20Request%20source%20code%20archival).

## Versioning

Ensure the eng/Versions.props file has the expected versions and pre-release labels. For a final release set PreReleaseVersionLabel to `rtw`.

## Ensure there's a release branch created.

See [Branching](Branching.md):
- Make the next preview branch.
- Update the branding in main.
- Update the global.json runtime and SDK versions in main.

## Identify the Final Build

First, identify the final build of the [`dotnet-yarp-official` Azure Pipeline](https://dev.azure.com/dnceng/internal/_build?definitionId=809&_a=summary) (on dnceng/internal). The final build will be the latest successful build **in the relevant `release/x` branch**. Use the "Branches" tab on Azure DevOps to help identify it. If the branch hasn't been mirrored yet (see [`dotnet-mirror-dnceng` pipeline](https://dev.azure.com/dnceng/internal/_build?definitionId=1387)) and there are no outstanding changesets in the branch, the build of the corresponding commit from the main branch can be used.

Once you've identified that build, click in to the build details.

## Validate the Final Build

At this point, you can perform any validation that makes sense. At a minimum, we should validate that the sample can run with the candidate packages. You can download the final build using the "Artifacts" which can be accessed under "Related" in the header:

![image](https://github.com/user-attachments/assets/27ddf12d-f4b7-4faa-862e-d2d1d6eafea9)

The packages can be accessed from the `PackageArtifacts` artifact:

![image](https://github.com/user-attachments/assets/264b8c6d-8108-4536-a61b-421aa652df73)

### Consume .nupkg

- Visual Studio: Place it in a local folder and add that folder as a nuget feed in Visual Studio.
- Command Line: `dotnet nuget add source <directory> -n local`

Walk through the [Getting Started](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/getting-started) instructions and update them in the release branch as needed.

Also validate any major new scenarios this release and their associated docs.

## Release the build

Once validation has been completed, it's time to release.

Go to the [`dotnet-yarp-release` pipeline](https://dev.azure.com/dnceng/internal/_build?definitionId=1448) and select "Run Pipeline".

Under "Resources", select the pipeline run that you've validated artifacts for.

![image](https://github.com/user-attachments/assets/efa38319-2620-4deb-aca3-d2bf23c991cc)
![image](https://github.com/user-attachments/assets/e25419e6-498d-4cc2-bf98-b5b2bc77c251)
![image](https://github.com/user-attachments/assets/e2f7f965-b4f7-40dc-ba2f-6a91062271d5)

Triple-check the version numbers of the packages in the artifacts against whatever validation was done at this point.

![image](https://github.com/user-attachments/assets/187d65d9-3c0f-4418-ab13-a77e0ad1b8e9)

Select "Run". Unless you're a release approver, you're done here!

## Approve the release

The Azure Pipeline will send an email to all the release approvers asking one of them to approve the release.

![image](https://github.com/user-attachments/assets/e772e31c-8aea-4bca-a21f-8bd62f61365f)

Click "Review Manual Validation", or navigate to the release pipeline directly in Azure DevOps. You'll see that the stage is "Pending Approval"

Enter a comment such as "release for preview X" approve finalize the release.
**After approving, packages will be published automatically**. It *is* possible to cancel the pipeline, but it might be too late. See "Troubleshooting" below.

The packages will be pushed and when the "NuGet.org" stage turns green, the packages are published!

*Note: NuGet publishing is quick, but there is a background indexing process that can mean it will take up to several hours for packages to become available*

## Tag the commit

Create and push a git tag for the commit associated with the final build (not necessarily the HEAD of the current release branch). See prior tags for the preferred format. Use a lightweight tag, not annotated.

`git tag v1.0.0-previewX`

Push the tag change to the upstream repo (**not your fork**)

`git push upstream v1.0.0-previewX`

## Draft release notes

Create a draft release at https://github.com/dotnet/yarp/releases using the new tag. See prior releases for the recommended content and format.

## Publish the release notes

Publish the draft release notes. These should be referencing the latest docs, packages, etc..

## Close the old milestone

It should be empty now. If it's not, move the outstanding issues to the next one.

## Announce on social media

David Fowler has a lot of twitter followers interested in YARP. Tweet a link to the release notes and let him retweet it.

## Set the preview branch to protected

This is to avoid accidental pushes to/deletions of the preview branch.

## Delete the previous preview branch

There should only be one [preview branch on the repo](https://github.com/dotnet/yarp/branches) after this point.

## Request source code archival
1. Go to the internal https://dpsopsrequestforms.azurewebsites.net portal
2. Select "Source Code Archival"
3. Proceed through steps 1,2 till the step 3 (all prerequisites are already fulfilled)
4. Fill in the required fields on the steps 3, 4, 5. Please, see the recommended values below.
5. At the last step, check all the info and and submit the request
6. Go to "My Request" tab and wait for a new ticket to appear on the list
7. Copy the ticket link from "Ticket ID" column which will look like `https://prod.******`
8. Replace the `prod` word to `portal`
9. Navigate to the fixed link and check that the ticket is actually created
10. That's all the actions needed to be done immediately. Afterwards, periodically track the ticket progress. It might take many hours.
11. [Offline] Wait for the archival completion report to arrive. Check that the size and number of archived files match the YARP repo.

### Recommended fields' values for archival request form
| Field | Value |
| --- | --- |
| Team Alias | dotnetrp |
| Business Group Name | Devdiv |
| Product Name | YARP |
| Version | \<release version\> |
| Production Type | dotNET |
| Release Type | \<RC or Release\> |
| Operating System(s) | Cross Platform |
| Product Language(s) | English |
| Release Date | \<release date\> |
| File Count | \<rough number of files in repo\> |
| Back Up Type | Code Repo(Git URL/AzureDevOps) |
| Repo URL | \<link to the internal AzDo YARP repo\> |
| OwnerAlias | dotnetrp |
| File Collection | Build Scripts, Help Utility Source Code, Source Code |
| Data Size | \<rough total files size in MB\> |

## Troubleshooting

### Authentication Errors

The pipeline is authenticated via a "Service Connection" in Azure DevOps. If there are authentication errors, it's likely the API key is invalid. Follow these steps to update the API key:

1. Go to NuGet.org, log in with an account associated with an `@microsoft.com` address that has access to the `dotnetframework` organization.
2. Generate a new API key with "dotnetframework" as the Package Owner and "*" as the Package "glob".
3. Copy that API key and fill it in to the "nuget.org (dotnetframework organization)" [Service Connection](https://dev.azure.com/dnceng/internal/_settings/adminservices) in Azure DevOps.

In the event you don't have access, contact `dnceng@microsoft.com` for guidance.

### Accidental Overpublish

In the event you overpublish (publish a package that wasn't intended to be released), you should "unlist" the package on NuGet. It is not possible to delete packages on NuGet.org, by design, but you can remove them from search results. Users who reference the version you published directly will still be able to download it, but it won't show up in search queries or non-version-specific actions (like installing the latest).

1. Go to NuGet.org, log in with an account associated with an `@microsoft.com` address that has access to the `dotnetframework` organization.
2. Go to the package page and click "Manage package" on the "Info" sidebar on the right.
3. Expand "Listing"
4. Select the version that was accidentally published
5. Uncheck the "List in search results" box
6. Click "Save"

### Package was rejected

NuGet.org has special criteria for all packages starting `Microsoft.`. If the package is rejected for not meeting one of those criteria, go to the [NuGet @ Microsoft](http://aka.ms/nuget) page for more information on required criteria and guidance for how to configure the package appropriately.
