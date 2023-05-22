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

printfn $"{fileName}:{Environment.NewLine}{referencesFound}"