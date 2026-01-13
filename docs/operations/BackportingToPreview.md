# Backporting changes to a preview branch

Backporting changes is very similar to a regular release. Changes are made on the preview branch, the builds are validated and ultimately released.

- Checkout the preview branch

  `git checkout release/1.0.0-previewX`
- Make and commit any changes
- Push the changes **to your own fork** and submit a PR against the preview branch
- Once the PR is merged, wait for the internal [`microsoft-reverse-proxy-official`](https://dev.azure.com/dnceng/internal/_build?definitionId=809&_a=summary&view=branches) pipeline to produce a build
- Validate the build the same way you would for a regular release [docs](https://github.com/dotnet/yarp/blob/main/docs/operations/Release.md#validate-the-final-build)
- Package Artifacts from this build can be shared to validate the patch. Optionally, the artifacts from the [public pipeline](https://dev.azure.com/dnceng/public/_build?definitionId=807&view=branches) can be used
- Continue iterating on the preview branch until satisfied with the validation of the change
- [Release the build](https://github.com/dotnet/yarp/blob/main/docs/operations/Release.md#release-the-build) from the preview branch
- Create a new git tag for the released commit

  **While still on the preview branch:**
  - `git tag v1.0.0-previewX.build.d`
  - `git push upstream --tags`
- Create a new [release](https://github.com/dotnet/yarp/releases).

## Internal fixes

Issues with significant security or disclosure concerns need to be fixed privately first. All of this work will happen on the internal Azdo repo and be merged to the public github repo at the time of disclosure.

- Make a separate clone of https://dev.azure.com/dnceng/internal/_git/dotnet-yarp to avoid accidentally pushing to the public repo.
- Create a branch named `internal/release/{version being patched}` starting from the tagged commit of the prior release.
- Update versioning as needed.
- Create a feature branch, fix the issue, and send a PR using Azdo.
- Release the build from the internal branch.
- Tag the commit and push it to the public repo. Do not push to regular `main` or `release/*` branches in the internal mirror.
- Cherry pick the changes to public main as needed.
- Finish the standard release checklist.
