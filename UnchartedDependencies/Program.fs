open System
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions
open System.Xml

let nixFriendlyPath (anyPath:string) : string = "./ScannedCode/" + anyPath.Replace("\\", "/")

let buildDictionary (input: string seq) : Collections.Generic.IDictionary<string, string> =
    input
    |> Seq.map (fun x -> Guid.NewGuid().ToString().Replace("-", ""), x)
    |> dict

let referencesFound (fileName: string) : string =
    let xml = File.ReadAllText(fileName)
    let doc = new XmlDocument() in doc.LoadXml xml
    doc.SelectNodes "/Project/ItemGroup/ProjectReference/@Include"
    |> Seq.cast<XmlNode>
    |> Seq.map (fun node -> node.Value)
    |> String.concat Environment.NewLine

// printfn "```mermaid"
// printfn "---"
// printfn "title: dependencies"
// printfn "---"
// printfn "stateDiagram-v2"
// printfn "direction lr"

// solution files
// k: unique identifier
// v: solution file path
let solutionFilePathsDictionary (startingPath:string) : Collections.Generic.IDictionary<string, string> =
    Directory.GetFiles(startingPath, "*.sln")
    |> Seq.map (fun solutionFileName -> (Guid.NewGuid().ToString(), solutionFileName))
    |> dict

// project files
// k: unique identifier
// v: project file path
let projectFilePathsDictionary (solutionFilePath:string) : Collections.Generic.IDictionary<string, string> =
    let solutionFileContent = File.ReadAllText solutionFilePath
    Regex.Matches(solutionFileContent, "[a-zA-Z\\\.]+\.[c|f]{1}sproj")
        |> Seq.map (fun projectFileName -> (Guid.NewGuid().ToString(), nixFriendlyPath projectFileName.Value))
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
            dependencyProjectName = projectName)
        (foundProject.Key, foundProject.Value))
    |> dict

let solutions = solutionFilePathsDictionary "./ScannedCode/"

let humanReadableSolutions = solutions |> Seq.map (fun solutionIdPathPair -> $"{solutionIdPathPair.Key} | {solutionIdPathPair.Value}") |> String.concat Environment.NewLine

printfn $"Solutions:{Environment.NewLine}{humanReadableSolutions}"

let projects = new Dictionary<string, string>()

let solutionToProjectsMapping = new Dictionary<string, string>()

solutions |> Seq.iter (fun solutionIdPathPair ->
    projectFilePathsDictionary solutionIdPathPair.Value
    |> Seq.iter (fun projectIdPathPair ->
        projects.Add(projectIdPathPair.Key, projectIdPathPair.Value)
        solutionToProjectsMapping.Add(projectIdPathPair.Key, solutionIdPathPair.Key)))

let humanReadableProjects = projects |> Seq.map (fun projectIdPathPair -> $"{projectIdPathPair.Key} | {projectIdPathPair.Value}") |> String.concat Environment.NewLine

printfn $"Projects:{Environment.NewLine}{humanReadableProjects}"

let humanReadableSolutionToProjectsMapping = solutionToProjectsMapping |> Seq.map (fun projectIdSolutionIdPair -> $"{projectIdSolutionIdPair.Key} | {projectIdSolutionIdPair.Value}") |> String.concat Environment.NewLine

printfn $"solution - projects mapping:{Environment.NewLine}{humanReadableSolutionToProjectsMapping}"

let projectToDependenciesMapping = new Dictionary<string, (string * string)>()

projects |> Seq.iter (fun projectIdPathPair ->
    projectDependenciesDictionary projectIdPathPair.Value projects
    |> Seq.iter (fun projectIdDependencyIdPair -> projectToDependenciesMapping.Add(Guid.NewGuid().ToString(), (projectIdPathPair.Key, projectIdDependencyIdPair.Key))))

let humanReadableProjectToDependenciesMapping = projectToDependenciesMapping |> Seq.map (fun projectidDependencyIdPair -> $"{fst projectidDependencyIdPair.Value} | {snd projectidDependencyIdPair.Value}") |> String.concat Environment.NewLine

printfn $"project - dependencies mapping:{Environment.NewLine}{humanReadableProjectToDependenciesMapping}"

// let solutions =
//     Directory.GetFiles("./ScannedCode/", "*.sln")
//     |> Seq.map (fun solutionFileName ->
//         let solution = File.ReadAllText solutionFileName
//         let projects =
//             Regex.Matches(solution, "[a-zA-Z\\\.]+\.[c|f]{1}sproj")
//             |> Seq.map (fun y ->
//                 let path = nixFriendlyPath y.Value
//                 Guid.NewGuid().ToString().Replace("-", ""), (path, referencesFound path))
//             |> dict
//         Guid.NewGuid().ToString().Replace("-", ""), (solutionFileName, projects))
//     |> dict

// let states =
//     solutions
//     |> Seq.map (fun x ->
//         let projectStates =
//             snd x.Value
//             |> Seq.map(fun y -> $"state \"{fst y.Value}\" as {y.Key}")
//             |> String.concat Environment.NewLine
//         $"state \"{fst x.Value}\" as {x.Key}{Environment.NewLine}{projectStates}")
//     |> String.concat Environment.NewLine

// let connections =
//     solutions
//     |> Seq.map (fun x ->
//         let projectSt =
//             snd x.Value
//             |> Seq.map (fun y -> $"{x.Key} --> {y.Key}")
//             |> String.concat Environment.NewLine
//         $"[*] --> {x.Key}{Environment.NewLine}{projectSt}")
//     |> String.concat Environment.NewLine

// printfn $"{states}"

// printfn $"{connections}"

// printfn "```"