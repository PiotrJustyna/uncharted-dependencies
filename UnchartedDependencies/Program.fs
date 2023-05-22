open System
open System.IO
open System.Text.RegularExpressions
open System.Xml

let buildDictionary (input: string seq) : Collections.Generic.IDictionary<string, string> =
    input
    |> Seq.map (fun x -> Guid.NewGuid().ToString().Replace("-", ""), x)
    |> dict

let fileName = "./ScannedCode/SosNet.Cache.Lookup.fsproj"

let xml = File.ReadAllText(fileName)

let doc = new XmlDocument() in doc.LoadXml xml;

let referencesFound =
    doc.SelectNodes "/Project/ItemGroup/ProjectReference/@Include"
    |> Seq.cast<XmlNode>
    |> Seq.map (fun node -> node.Value)
    |> String.concat Environment.NewLine

// printfn $"{fileName}:{Environment.NewLine}{referencesFound}"

printf $"scanning for a solution file...{Environment.NewLine}"

printfn "```mermaid"
printfn "---"
printfn "title: fcc dependencies"
printfn "---"
printfn "stateDiagram-v2"
printfn "direction lr"

let solutions =
    Directory.GetFiles("./ScannedCode/", "*.sln")
    |> Seq.map (fun x ->
        let solution = File.ReadAllText x
        let projects =
            Regex.Matches(solution, "[a-zA-Z\\\.]+\.[c|f]{1}sproj")
            |> Seq.map(fun y -> y.Value)
            |> buildDictionary
        Guid.NewGuid().ToString().Replace("-", ""), (x, projects))
    |> dict

let states =
    solutions
    |> Seq.map (fun x ->
        let projectStates =
            snd x.Value
            |> Seq.map(fun y -> $"state \"{y.Value}\" as {y.Key}")
            |> String.concat Environment.NewLine
        $"state \"{fst x.Value}\" as {x.Key}{Environment.NewLine}{projectStates}")
    |> String.concat Environment.NewLine

let connections =
    solutions
    |> Seq.map (fun x ->
        let projectSt =
            snd x.Value
            |> Seq.map (fun y -> $"{x.Key} --> {y.Key}")
            |> String.concat Environment.NewLine
        $"[*] --> {x.Key}{Environment.NewLine}{projectSt}")
    |> String.concat Environment.NewLine

printfn $"{states}"

printfn $"{connections}"

printfn "```"