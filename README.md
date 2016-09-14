# CSProjectAnalyser

Description:
Analyses csproj files to determine dependencies.  

```
Syntax:
The following argument prefix characters can be used: '-','/'
    --Path, -p
        The path to scan
        [Optional], Default:''

    --Assembly, -a
        The assembly to Analyse
        [Optional], Default:''

    --Extensions, -e
        A comma seperated list of project types to analyse; csproj
        support only now.
        [Optional], Default:'*.csproj'

    --Verbosity, -v
        How much information you want
        [Optional], Default:''

    --Summary, -s
        Include summary
        [Optional], Default:''

    --Recursive, -r
        Recursively scan sub directories
        [Optional], Default:'False'

    --RecurseDependencies, -d
        Recursively list a projects dependencies.
        [Optional], Default:'True'

    --MaxDepth, -m
        Maximum depth to recursively list a projects dependencies.
        [Optional], Default:'8'

    --IncludeSystemDependencies, -s
        Include System.* dependencies
        [Optional], Default:'False'
```
