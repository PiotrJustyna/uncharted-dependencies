open System
open System.IO
open System.Xml

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

let files =
    Directory.GetFiles("./ScannedCode/", "*.sln")
    |> Seq.map (fun x -> Guid.NewGuid().ToString().Replace("-", ""), x)
    |> dict

let states =
    files
    |> Seq.map (fun x -> $"state \"{x.Value}\" as {x.Key}")
    |> String.concat Environment.NewLine

let connections =
    files
    |> Seq.map (fun x ->
        $"[*] --> {x.Key}{Environment.NewLine}{x.Key} --> [*]")
    |> String.concat Environment.NewLine

printfn $"{states}"

printfn $"{connections}"

printfn "```"