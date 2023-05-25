open System
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions
open System.Xml

let mermaidFriendlyGuid () : string = Guid.NewGuid().ToString().Replace("-", "")

let nixFriendlyPath (anyPath:string) : string = "./ScannedCode/" + anyPath.Replace("\\", "/")

let referencesFound (fileName: string) : string =
    let xml = File.ReadAllText(fileName)
    let doc = new XmlDocument() in doc.LoadXml xml
    doc.SelectNodes "/Project/ItemGroup/ProjectReference/@Include"
    |> Seq.cast<XmlNode>
    |> Seq.map (fun node -> node.Value)
    |> String.concat Environment.NewLine

let nugetPackages = new Dictionary<string, string>()

let readNugetPackages (startingPath:string) =
    Directory.GetFiles(startingPath, "paket.dependencies")
    |> Seq.iter (fun dependenciesPath -> 
        File.ReadAllText(dependenciesPath).Split('\n', StringSplitOptions.RemoveEmptyEntries)
        |> Seq.filter (fun dependency -> dependency.StartsWith("nuget "))
        |> Seq.iter (fun dependency ->
            let nugetKeyworkRemoved = dependency.Substring("nuget ".Length)
            let cleanedDependency = if nugetKeyworkRemoved.Contains(" ") then nugetKeyworkRemoved.Remove(nugetKeyworkRemoved.IndexOf(" ")) else nugetKeyworkRemoved.Remove(nugetKeyworkRemoved.Length - 1)
            nugetPackages.Add(mermaidFriendlyGuid(), cleanedDependency)))

// solution files
// k: unique identifier
// v: solution file path
let solutionFilePathsDictionary (startingPath:string) : Collections.Generic.IDictionary<string, string> =
    Directory.GetFiles(startingPath, "*.sln")
    |> Seq.map (fun solutionFileName -> (mermaidFriendlyGuid(), solutionFileName))
    |> dict

// project files
// k: unique identifier
// v: project file path
let projectFilePathsDictionary (solutionFilePath:string) : Collections.Generic.IDictionary<string, string> =
    let solutionFileContent = File.ReadAllText solutionFilePath
    Regex.Matches(solutionFileContent, "[a-zA-Z\\\.]+\.[c|f]{1}sproj")
        |> Seq.map (fun projectFileName -> (mermaidFriendlyGuid(), nixFriendlyPath projectFileName.Value))
        |> dict

// project dependency files
// k: unique identifier (which matches an existing project - all found dependencies are projects)
// v: project file path
let projectDependenciesDictionary (projectFilePath:string) (projects:Dictionary<string, string>) : Collections.Generic.IDictionary<string, string> =
    let xml = File.ReadAllText(projectFilePath)
    let doc = new XmlDocument() in doc.LoadXml xml
    doc.SelectNodes "/Project/ItemGroup/ProjectReference/@Include"
    |> Seq.cast<XmlNode>
    |> Seq.map (fun node ->
        let foundProject = projects |> Seq.find (fun projectIdPathPair ->
            let nixFriendlyDependencyPath = nixFriendlyPath node.Value
            let dependencyProjectName = nixFriendlyDependencyPath.Substring(nixFriendlyDependencyPath.LastIndexOf('/'))
            let projectName = projectIdPathPair.Value.Substring(projectIdPathPair.Value.LastIndexOf('/'))
            // mathing on project names as relative paths can be different:
            // we build the projects collection from solution's perspective and the dependencies collection from project perspective
            String.Equals(dependencyProjectName, projectName, StringComparison.CurrentCultureIgnoreCase))
        (foundProject.Key, foundProject.Value))
    |> dict

readNugetPackages "./ScannedCode/"

let humanReadablePackages = nugetPackages |> Seq.map (fun packageIdNamePair -> $"{packageIdNamePair.Key} | {packageIdNamePair.Value}") |> String.concat Environment.NewLine

let mermaidPackages = nugetPackages |> Seq.map (fun packageIdNamePair -> $"state \"{packageIdNamePair.Value}\" as {packageIdNamePair.Key}") |> String.concat Environment.NewLine

printfn $"{humanReadablePackages}"

let solutions = solutionFilePathsDictionary "./ScannedCode/"

let humanReadableSolutions = solutions |> Seq.map (fun solutionIdPathPair -> $"{solutionIdPathPair.Key} | {solutionIdPathPair.Value}") |> String.concat Environment.NewLine

let mermaidSolutions = solutions |> Seq.map (fun solutionIdPathPair -> $"state \"{solutionIdPathPair.Value}\" as {solutionIdPathPair.Key}") |> String.concat Environment.NewLine

printfn $"Solutions:{Environment.NewLine}{humanReadableSolutions}"

let projects = new Dictionary<string, string>()

let solutionToProjectsMapping = new Dictionary<string, string>()

solutions |> Seq.iter (fun solutionIdPathPair ->
    projectFilePathsDictionary solutionIdPathPair.Value
    |> Seq.iter (fun projectIdPathPair ->
        projects.Add(projectIdPathPair.Key, projectIdPathPair.Value)
        solutionToProjectsMapping.Add(projectIdPathPair.Key, solutionIdPathPair.Key)))

let humanReadableProjects = projects |> Seq.map (fun projectIdPathPair -> $"{projectIdPathPair.Key} | {projectIdPathPair.Value}") |> String.concat Environment.NewLine

let mermaidProjects = projects |> Seq.map (fun projectIdPathPair -> $"state \"{projectIdPathPair.Value}\" as {projectIdPathPair.Key}") |> String.concat Environment.NewLine

printfn $"Projects:{Environment.NewLine}{humanReadableProjects}"

let humanReadableSolutionToProjectsMapping = solutionToProjectsMapping |> Seq.map (fun projectIdSolutionIdPair -> $"{projectIdSolutionIdPair.Key} | {projectIdSolutionIdPair.Value}") |> String.concat Environment.NewLine

let mermaidSolutionToProjectsMapping = solutionToProjectsMapping |> Seq.map (fun projectIdSolutionIdPair -> $"{projectIdSolutionIdPair.Value} --> {projectIdSolutionIdPair.Key}") |> String.concat Environment.NewLine

printfn $"solution - projects mapping:{Environment.NewLine}{humanReadableSolutionToProjectsMapping}"

let projectToDependenciesMapping = new Dictionary<string, (string * string)>()

projects |> Seq.iter (fun projectIdPathPair ->
    projectDependenciesDictionary projectIdPathPair.Value projects
    |> Seq.iter (fun projectIdDependencyIdPair -> projectToDependenciesMapping.Add(mermaidFriendlyGuid(), (projectIdPathPair.Key, projectIdDependencyIdPair.Key))))

let humanReadableProjectToDependenciesMapping = projectToDependenciesMapping |> Seq.map (fun projectidDependencyIdPair -> $"{fst projectidDependencyIdPair.Value} | {snd projectidDependencyIdPair.Value}") |> String.concat Environment.NewLine

let mermaidDependenciesMapping = projectToDependenciesMapping |> Seq.map (fun projectidDependencyIdPair -> $"{fst projectidDependencyIdPair.Value} --> {snd projectidDependencyIdPair.Value}") |> String.concat Environment.NewLine

printfn $"project - dependencies mapping:{Environment.NewLine}{humanReadableProjectToDependenciesMapping}"

// printfn "```mermaid"

printfn "---"

printfn "title: dependencies"

printfn "---"

printfn "stateDiagram-v2"

printfn "direction lr"

printfn $"{mermaidPackages}"

printfn $"{mermaidSolutions}"

printfn $"{mermaidProjects}"

printfn $"{mermaidSolutionToProjectsMapping}"

printfn $"{mermaidDependenciesMapping}"

// printfn "```"
