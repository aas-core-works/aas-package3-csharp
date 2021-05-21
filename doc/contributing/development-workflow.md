# Development Workflow

We develop with Github using pull requests (see [GitHub's guide on pull requests] for a short introduction). 

[GitHub's guide on pull requests]: https://guides.github.com/introduction/flow/

**Development branch**. The development branch is always `main`. 

**Releases**. The releases mark the development milestones on the `main` branch with a certain feature completeness.

## Pull Requests

**Feature branches**. We develop using the feature branches, see [this section of the Git book].

[this section of the Git book]: https://git-scm.com/book/en/v2/Git-Branching-Branching-Workflows

If you are a member of the development team, create a feature branch directly within the repository.

Otherwise, if you are a non-member contributor, fork the repository and create the feature branch in your forked repository. 
See [this Github tuturial] for more guidance. 

[this GitHub tutorial]: https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/creating-a-pull-request-from-a-fork

**Branch Prefix**. Please prefix the branch with your Github user name (*e.g.,* `mristin/Add-some-feature`).

**Continuous Integration**. Github will run the continuous integration (CI) automatically through Github workflows.
The CI includes building the solution, running the test, inspecting the code *etc.* (see below the section "Pre-merge Checks").

Please note that running the Github workflows consumes computational resources which is often unnecessary if you are certain that some checks are not needed.
For example, there is no need to build the whole solution if you only make a minor change in a powershell Script unrelated to building. 
You can manually disable workflows by appending the following lines to the body of the pull request (corresponding to which checks you want to disable):

* `The workflow check was intentionally skipped.`

## Commit Messages

The commit messages follow the guidelines from 
https://chris.beams.io/posts/git-commit:

* Separate subject from body with a blank line
* Limit the subject line to 50 characters
* Capitalize the subject line
* Do not end the subject line with a period
* Use the imperative mood in the subject line
* Wrap the body at 72 characters
* Use the body to explain *what* and *why* (instead of *how*)

We automatically check the commit messages using [opinionated-commit-message].

[opinionated-commit-message]: https://github.com/mristin/opinionated-commit-message

Here is an example commit message:

```
Make DownloadSamples.ps1 use default proxy

Using the default proxy is necessary so that DownloadSamples.ps1 can
operate on enterprise networks which restrict the network traffic
through the proxy.

The workflow check was intentionally skipped.
```
