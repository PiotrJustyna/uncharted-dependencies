open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Threading
open System.Xml

// this is for troubleshooting docker-related issues
// Thread.Sleep(60000)

let debuggingMode = false

let debugPrintfn (content: string) =
    if debuggingMode then
        printfn $"{content}"
    else
        ()

let mermaidFriendlyGuid () : string =
    Guid.NewGuid().ToString().Replace("-", "")

let nixFriendlyPath (anyPath: string) : string =
    "./ScannedCode/" + anyPath.Replace("\\", "/")

let readNugetPackages (startingPath: string) : Dictionary<string, string> =
    let nugetPackages =
        Dictionary<string, string>()

    Directory.GetFiles(startingPath, "paket.dependencies")
    |> Seq.iter (fun dependenciesPath ->
        File
            .ReadAllText(dependenciesPath)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        |> Seq.map (fun dependency -> dependency.TrimStart())
        |> Seq.filter (fun dependency -> dependency.StartsWith("nuget "))
        |> Seq.iter (fun dependency ->
            let nugetKeyworkRemoved =
                dependency.Substring("nuget ".Length)

            let cleanedDependency =
                if nugetKeyworkRemoved.Contains(" ") then
                    nugetKeyworkRemoved.Remove(nugetKeyworkRemoved.IndexOf(" "))
                else
                    nugetKeyworkRemoved.Remove(nugetKeyworkRemoved.Length - 1)

            nugetPackages.Add(mermaidFriendlyGuid (), cleanedDependency)))

    nugetPackages

// solution files
// k: unique identifier
// v: solution file path
let solutionFilePathsDictionary (startingPath: string) : IDictionary<string, string> =
    Directory.GetFiles(startingPath, "*.sln")
    |> Seq.map (fun solutionFileName -> (mermaidFriendlyGuid (), solutionFileName))
    |> dict

// project files
// k: unique identifier
// v: project file path
let projectFilePathsDictionary (solutionFilePath: string) : Collections.Generic.IDictionary<string, string> =
    let solutionFileContent =
        File.ReadAllText solutionFilePath

    Regex.Matches(solutionFileContent, "[a-zA-Z\\\.]+\.[c|f]{1}sproj")
    |> Seq.map (fun projectFileName -> (mermaidFriendlyGuid (), nixFriendlyPath projectFileName.Value))
    |> dict

// project dependency files
// k: unique identifier (which matches an existing project - all found dependencies are projects)
// v: project file path
let projectDependenciesDictionary
    (projectFilePath: string)
    (projects: Dictionary<string, string>)
    : IDictionary<string, string> =
    let xml = File.ReadAllText(projectFilePath)
    let doc = new XmlDocument() in
    doc.LoadXml xml

    doc.SelectNodes "/Project/ItemGroup/ProjectReference/@Include"
    |> Seq.cast<XmlNode>
    |> Seq.map (fun node ->
        let foundProject =
            projects
            |> Seq.find (fun projectIdPathPair ->
                let nixFriendlyDependencyPath =
                    nixFriendlyPath node.Value

                let dependencyProjectName =
                    nixFriendlyDependencyPath.Substring(nixFriendlyDependencyPath.LastIndexOf('/'))

                let projectName =
                    projectIdPathPair.Value.Substring(projectIdPathPair.Value.LastIndexOf('/'))
                // matching on project names as relative paths can be different:
                // we build the projects collection from solution's perspective and the dependencies collection from project perspective
                String.Equals(dependencyProjectName, projectName, StringComparison.CurrentCultureIgnoreCase))

        (foundProject.Key, foundProject.Value))
    |> dict

let nugetPackages =
    readNugetPackages "./ScannedCode/"

let humanReadablePackages =
    nugetPackages
    |> Seq.map (fun packageIdNamePair -> $"{packageIdNamePair.Key} | {packageIdNamePair.Value}")
    |> String.concat Environment.NewLine

let mermaidPackages =
    nugetPackages
    |> Seq.map (fun packageIdNamePair -> $"state \"{packageIdNamePair.Value}\" as {packageIdNamePair.Key}")
    |> String.concat Environment.NewLine

debugPrintfn $"{humanReadablePackages}"

let solutions =
    solutionFilePathsDictionary "./ScannedCode/"

let humanReadableSolutions =
    solutions
    |> Seq.map (fun solutionIdPathPair -> $"{solutionIdPathPair.Key} | {solutionIdPathPair.Value}")
    |> String.concat Environment.NewLine

let mermaidSolutions =
    solutions
    |> Seq.map (fun solutionIdPathPair -> $"state \"{solutionIdPathPair.Value}\" as {solutionIdPathPair.Key}")
    |> String.concat Environment.NewLine

debugPrintfn $"Solutions:{Environment.NewLine}{humanReadableSolutions}"

let projects = Dictionary<string, string>()

let solutionToProjectsMapping =
    Dictionary<string, string>()

solutions
|> Seq.iter (fun solutionIdPathPair ->
    projectFilePathsDictionary solutionIdPathPair.Value
    |> Seq.iter (fun projectIdPathPair ->
        projects.Add(projectIdPathPair.Key, projectIdPathPair.Value)
        solutionToProjectsMapping.Add(projectIdPathPair.Key, solutionIdPathPair.Key)))

let humanReadableProjects =
    projects
    |> Seq.map (fun projectIdPathPair -> $"{projectIdPathPair.Key} | {projectIdPathPair.Value}")
    |> String.concat Environment.NewLine

let mermaidProjects =
    projects
    |> Seq.map (fun projectIdPathPair -> $"state \"{projectIdPathPair.Value}\" as {projectIdPathPair.Key}")
    |> String.concat Environment.NewLine

debugPrintfn $"Projects:{Environment.NewLine}{humanReadableProjects}"

let humanReadableSolutionToProjectsMapping =
    solutionToProjectsMapping
    |> Seq.map (fun projectIdSolutionIdPair -> $"{projectIdSolutionIdPair.Key} | {projectIdSolutionIdPair.Value}")
    |> String.concat Environment.NewLine

let mermaidSolutionToProjectsMapping =
    solutionToProjectsMapping
    |> Seq.map (fun projectIdSolutionIdPair -> $"{projectIdSolutionIdPair.Value} --> {projectIdSolutionIdPair.Key}")
    |> String.concat Environment.NewLine

debugPrintfn $"Solution - Projects mapping:{Environment.NewLine}{humanReadableSolutionToProjectsMapping}"

let projectToInternalDependenciesMapping = Dictionary<string, (string * string)>()

let projectToExternalDependenciesMapping = Dictionary<string, (string * string)>()

projects
|> Seq.iter (fun projectIdPathPair ->
    // paket dependencies
    let paketReferencesPath =
        projectIdPathPair.Value.Remove(projectIdPathPair.Value.LastIndexOf("/"))
        + "/paket.references"

    let paketReferencesExistsForProject =
        File.Exists(paketReferencesPath)

    debugPrintfn
        $"paket.references ({paketReferencesPath}) file for project \"{projectIdPathPair.Value}\" found: {paketReferencesExistsForProject}"

    if paketReferencesExistsForProject then
        File.ReadAllLines(paketReferencesPath)
        |> Seq.filter (fun externalDependencyName ->
            String.IsNullOrWhiteSpace(externalDependencyName)
            |> not
            && externalDependencyName.StartsWith("#") |> not)
        |> Seq.iter (fun externalDependencyName ->
            let cleanedExternalDependencyName =
                if externalDependencyName.Contains(" ") then
                    externalDependencyName.Remove(externalDependencyName.IndexOf(" "))
                else
                    externalDependencyName

            let externalDependencyLookup =
                nugetPackages
                |> Seq.tryFind (fun nugetPackage -> String.Equals(nugetPackage.Value, cleanedExternalDependencyName, StringComparison.CurrentCultureIgnoreCase))

            match externalDependencyLookup with
            | None ->
                printfn
                    $"WARNING: {cleanedExternalDependencyName} could not be found in packet.dependencies for project {projectIdPathPair.Value}!"
            | Some x ->
                debugPrintfn $"* {x.Key} - {x.Value}"
                projectToExternalDependenciesMapping.Add(mermaidFriendlyGuid (), (projectIdPathPair.Key, x.Key)))
    else
        ()

    // solution-internal dependency projects
    projectDependenciesDictionary projectIdPathPair.Value projects
    |> Seq.iter (fun projectIdDependencyIdPair ->
        projectToInternalDependenciesMapping.Add(mermaidFriendlyGuid (), (projectIdPathPair.Key, projectIdDependencyIdPair.Key))))

let humanReadableProjectToInternalDependenciesMapping =
    projectToInternalDependenciesMapping
    |> Seq.map (fun projectIdDependencyIdPair ->
        $"{fst projectIdDependencyIdPair.Value} | {snd projectIdDependencyIdPair.Value}")
    |> String.concat Environment.NewLine

let humanReadableProjectToExternalDependenciesMapping =
    projectToExternalDependenciesMapping
    |> Seq.map (fun x -> (fst x.Value, snd x.Value))
    |> Seq.groupBy (fun x -> fst x)
    |> Seq.sortBy (fun x -> snd x |> Seq.length)
    |> Seq.map (fun x ->
        let humanReadableNugetDependencies = snd x |> Seq.map (fun y -> $"* {nugetPackages[snd y]}") |> String.concat Environment.NewLine
        $"## {projects[fst x]}{Environment.NewLine}{Environment.NewLine}{humanReadableNugetDependencies}{Environment.NewLine}")
    |> String.concat Environment.NewLine

let mermaidInternalDependenciesMapping =
    projectToInternalDependenciesMapping
    |> Seq.map (fun projectidDependencyIdPair ->
        $"{fst projectidDependencyIdPair.Value} --> {snd projectidDependencyIdPair.Value}")
    |> String.concat Environment.NewLine

let mermaidExternalDependenciesMapping =
    projectToExternalDependenciesMapping
    |> Seq.map (fun projectidDependencyIdPair ->
        $"{fst projectidDependencyIdPair.Value} --> {snd projectidDependencyIdPair.Value}")
    |> String.concat Environment.NewLine

debugPrintfn $"project - internal dependencies mapping:{Environment.NewLine}{humanReadableProjectToInternalDependenciesMapping}"

printfn $"project - external dependencies mapping:{Environment.NewLine}{humanReadableProjectToExternalDependenciesMapping}"

let mermaidContent =
    [|"```mermaid";
    "---";
    "title: dependencies";
    "---";
    "stateDiagram-v2";
    "direction lr";
    $"{mermaidPackages}";
    $"{mermaidSolutions}";
    $"{mermaidProjects}";
    $"{mermaidSolutionToProjectsMapping}";
    "state internaldependencies {";
    $"{mermaidInternalDependenciesMapping}";
    "}";
    $"{mermaidExternalDependenciesMapping}";
    "```"|]
    |> String.concat Environment.NewLine

debugPrintfn $"{mermaidContent}"

File.WriteAllText("/tmp/mermaid-output.md", mermaidContent)

File.WriteAllText("/tmp/markdown-output.md", humanReadableProjectToExternalDependenciesMapping)