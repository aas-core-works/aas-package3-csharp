# Introduction

This series of articles help you set up and build the solution, explain you how to test and check your code contribution and finally how to properly submit it.

If you don't like reading the documentation, just want to take a deep dive and start contributing, the following section "Quick Start" gives you a brief  overview of how you can get your code in.

## Quick Start

This is a brief list of steps explaining how to submit your code contribution.

### Development Tools

* Install the IDE of your choice, *e.g.*, [Visual Studio 2019 Community Edition].

### Create a feature branch

**If you are a member of [aas-core-works GitHub organization]**:
 
* Clone the Git repository:
  ```
  git clone https://github.com/aas-core-works/aas-package3-csharp
  ```
  
* Create your feature branch:

  ```
  git checkout -b yourUsername/Add-some-new-feature
  ``` 
  
  Please observe [our guideline to naming the branches] 
  (`{your-username}/{Describe-subject-of-the-commit}`).

**Otherwise**:

* Make the fork of your repository (see [this GitHub guide about forking])

* Clone the Git repository:
  ```
  git clone https://github.com/yourUsername/aas-package3-csharp
  ```

* Create your feature branch:

  ```
  git checkout -b yourUsername/Add-some-new-feature
  ``` 

### Dependencies

* We assume that you installed both [.NET 3.1 SDK] and [.NET 5 SDK] on your system.

* Change to the directory of your repository. Execute the following command to install all the development dependencies:

  ```
  .\src\InstallSolutionDependencies.ps1
  ```

### Write Your Code

* Make your code changes. 
* Do not forget to implement unit tests.

### Commit & Push

* Format your code to conform to the style guide:

  ```
  .\src\FormatCode.ps1
  ```

* Add files that you would like in your pull request:

  ```
  git add src/SomeProject/SomeFile.cs
  ```

* Commit locally:

  ```
  git commit
  ```

  Please observe our [guideline related to commit messages]:
  1) First line is a subject, max. 50 characters, starts with a verb in imperative mood
  2) Empty line
  3) Body, max. line width 72 characters, must not start with the first word of the subject

* Run the pre-commit checks and make sure they all pass:

  ```
  .\src\Check.ps1
  ```

* If needed, change your commit message:

  ```
  git commit --amend
  ```

* Set the upstream of your branch:

  ```
  git branch --set-upstream-to origin/yourUsername/Add-some-new-feature
  ```

* Push your changes:

  ```
  git push
  ```

### Pull Request
 
* Go to the [aas-package3-csharp GitHub Repository] and
  create the pull request in the web interface.
* Have it reviewed, if necessary
* Make sure all the remote checks pass
  
### Merge

**If you are a member of [aas-core-works GitHub organization]**:
 
* Squash & Merge (see 
  [this section of the GitHub documentation on squash & merge])  

**Otherwise**:

* Ask somebody from the organization to squash & merge the pull request for you 

[Visual Studio 2019 Community Edition]: https://visualstudio.microsoft.com/de/vs/community/
[aas-core-works GitHub organization]: https://github.com/aas-core-works
[our guideline to naming the branches]: https://aas-core-works.github.io/aas-package3-cshapr9-dotnet5/devdoc/getting-started/development-workflow.html#pull-requests
[this GitHub guide about forking]: https://guides.github.com/activities/forking/
[guideline related to commit messages]: https://aas-core-works.github.io/aas-package3-cshapr9-dotnet5/devdoc/getting-started/development-workflow.html#commit-messages
[.NET 3.1 SDK]: https://dotnet.microsoft.com/download/dotnet/3.1
[.NET 5 SDK]: https://dotnet.microsoft.com/download/dotnet/5.0
[aas-package3-csharp GitHub Repository]: https://github.com/aas-core-works/aas-package3-csharp
[this section of the GitHub documentation on squash & merge]: https://docs.github.com/en/github/collaborating-with-issues-and-pull-requests/merging-a-pull-request
