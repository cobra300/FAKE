﻿/// Contains tasks to generate AssemblyInfo files for C# and F#.
/// There is also a tutorial about the [AssemblyInfo tasks](../assemblyinfo.html) available.
module Fake.AssemblyInfoFile

open System
open System.IO
open System.Text.RegularExpressions

let internal assemblyVersionRegex = getRegEx @"([0-9]+.)+[0-9]+"

// matches [assembly: name(value)] and captures "name" and "value" as named captures. Variations for C#, F#, C++ and VB
let private regexAttrNameValueCs = @"^\s*\[\s*assembly:\s*(?<name>\w+?)\s*\((?<value>.*)\)\s*\]\s*$"
let private regexAttrNameValueFs = @"^\s*\[\<\s*assembly:\s*(?<name>\w+?)\s*\((?<value>.*)\)\s*\>\]\s*$"
let private regexAttrNameValueCpp = @"^\s*\[\s*assembly:\s*(?<name>\w+?)\s*\((?<value>.*)\)\s*\]\s*;\s*$"
let private regexAttrNameValueVb = @"^\s*\<\s*assembly:\s*(?<name>\w+?)\s*\((?<value>.*)\)\s*\>\s*$"

// matches [assembly: name(value)] but only captures the value. Variations for C#, F#, C++ and VB
let private regexAttrValueCs name =
    @"(?<=^\s*\[\s*assembly:\s*" + name + @"(?:Attribute)?\s*\()" // look-behind for "[assembly: name[Attribute]("
    + @"(.*)"                                       // value
    + @"(?=\)\s*\]\s*$)"                            // look-ahead for ")]"

let private regexAttrValueFs name =
    @"(?<=^\s*\[\<\s*assembly:\s*" + name + @"(?:Attribute)?\s*\()" // look-behind for "[<assembly: name[Attribute]("
    + @"(.*)"                                         // value
    + @"(?=\)\s*\>\]\s*$)"                            // look-ahead for ")>]"

let private regexAttrValueCpp name =
    @"(?<=^\s*\[\s*assembly:\s*" + name + @"(?:Attribute)?\s*\()" // look-behind for "[assembly: name[Attribute]("
    + @"(.*)"                                       // value
    + @"(?=\)\s*\]\s*;\s*$)"                        // look-ahead for ")];"

let private regexAttrValueVb name =
    @"(?<=^\s*\<\s*assembly:\s*" + name + @"(?:Attribute)?\s*\()" // look-behind for "<assembly: name[Attribute]("
    + @"(.*)"                                       // value
    + @"(?=\)\s*\>\s*$)"                            // look-ahead for ")>"

let private NormalizeVersion version =
    let m = assemblyVersionRegex.Match(version)
    if m.Captures.Count > 0 then m.Captures.[0].Value
    else version

/// Represents options for configuring the emission of AssemblyInfo
type AssemblyInfoFileConfig
    ( // If true, a module (for F#) or static class (for C#), which contains the assembly version, will be generated
      generateClass : bool,
      // The optional namespace into which assembly info will be generated; defaults to "System".
      ?useNamespace : string ) =
        member x.GenerateClass = generateClass
        member x.UseNamespace =
            match useNamespace with
            | Some n -> n
            | None -> "System"
        static member Default = AssemblyInfoFileConfig(true)

/// Represents AssemblyInfo attributes
type Attribute(name, value, inNamespace, staticPropName, staticPropType, staticPropValue) =
    member this.Name = name
    member this.Value = value
    member this.Namespace = inNamespace
    member this.StaticPropertyName = staticPropName
    member this.StaticPropertyType = staticPropType
    member this.StaticPropertyValue = staticPropValue

    new(name, value, inNamespace, staticPropType) =
        Attribute(name, value, inNamespace, name, staticPropType, value)

    /// Creates a simple attribute with string values. Used as base for other attributes
    static member StringAttribute(name, value, inNamespace, ?staticName, ?staticValue) =
        let quotedValue = sprintf "\"%s\"" value
        Attribute(name, quotedValue, inNamespace, defaultArg staticName name, typeof<string>.FullName, defaultArg staticValue quotedValue)

    /// Creates a simple attribute with boolean values. Used as base for other attributes
    static member BoolAttribute(name, value, inNamespace) = Attribute(name, sprintf "%b" value, inNamespace, typeof<bool>.FullName)

    /// Creates an attribute which holds the company information
    static member Company(value) = Attribute.StringAttribute("AssemblyCompany", value, "System.Reflection")

    /// Creates an attribute which holds the product name
    static member Product(value) = Attribute.StringAttribute("AssemblyProduct", value, "System.Reflection")

    /// Creates an attribute which holds the copyright information
    static member Copyright(value) = Attribute.StringAttribute("AssemblyCopyright", value, "System.Reflection")

    /// Creates an attribute which holds the product title
    static member Title(value) = Attribute.StringAttribute("AssemblyTitle", value, "System.Reflection")

    /// Creates an attribute which holds the product description
    static member Description(value) = Attribute.StringAttribute("AssemblyDescription", value, "System.Reflection")

    /// Creates an attribute which holds the assembly culture information
    static member Culture(value) = Attribute.StringAttribute("AssemblyCulture", value, "System.Reflection")

    /// Creates an attribute which holds the assembly configuration
    static member Configuration(value) = Attribute.StringAttribute("AssemblyConfiguration", value, "System.Reflection")

    /// Creates an attribute which holds the trademark
    static member Trademark(value) = Attribute.StringAttribute("AssemblyTrademark", value, "System.Reflection")

    /// Creates an attribute which holds the assembly version
    static member Version(value) =
        Attribute.StringAttribute("AssemblyVersion", NormalizeVersion value, "System.Reflection")

    /// Creates an attribute which holds the assembly key file
    static member KeyFile(value) = Attribute.StringAttribute("AssemblyKeyFile", value, "System.Reflection")

    /// Creates an attribute which holds the assembly key name
    static member KeyName(value) = Attribute.StringAttribute("AssemblyKeyName", value, "System.Reflection")

    /// Creates an attribute which holds the "InternalVisibleTo" data
    static member InternalsVisibleTo(value) =
        Attribute.StringAttribute("InternalsVisibleTo", value, "System.Runtime.CompilerServices")

    /// Creates an attribute which holds the assembly file version
    static member FileVersion(value) =
        Attribute.StringAttribute("AssemblyFileVersion", NormalizeVersion value, "System.Reflection")

    /// Creates an attribute which holds an assembly information version
    static member InformationalVersion(value) =
        Attribute.StringAttribute("AssemblyInformationalVersion", value, "System.Reflection")

    /// Creates an attribute which holds the Guid
    static member Guid(value) = Attribute.StringAttribute("Guid", value, "System.Runtime.InteropServices")

    /// Creates an attribute which specifies if the assembly is visible via COM
    static member ComVisible(?value) =
        Attribute.BoolAttribute("ComVisible", defaultArg value false, "System.Runtime.InteropServices")

    /// Creates an attribute which specifies if the assembly is CLS compliant
    static member CLSCompliant(?value) = Attribute.BoolAttribute("CLSCompliant", defaultArg value false, "System")

    /// Creates an attribute which specifies if the assembly uses delayed signing
    static member DelaySign(value) =
        Attribute.BoolAttribute("AssemblyDelaySign", defaultArg value false, "System.Reflection")

    /// Create an attribute which specifies metadata about the assembly
    static member Metadata(name,value) =
        Attribute.StringAttribute("AssemblyMetadata", sprintf "%s\",\"%s" name value, "System.Reflection", sprintf "AssemblyMetadata_%s" (name.Replace(" ", "_")), sprintf "\"%s\"" value)

let private writeToFile outputFileName (lines : seq<string>) =
    let fi = fileInfo outputFileName
    if fi.Exists then fi.Delete()
    let dirName = System.IO.Path.GetDirectoryName(outputFileName)
    if not (isNullOrEmpty dirName) then
        System.IO.Directory.CreateDirectory(dirName) |> ignore
    use writer = new System.IO.StreamWriter(outputFileName, false, System.Text.Encoding.UTF8)
    lines |> Seq.iter writer.WriteLine
    tracefn "Created AssemblyInfo file \"%s\"." outputFileName

let private getDependencies attributes =
    attributes
    |> Seq.map (fun (attr : Attribute) -> attr.Namespace)
    |> Set.ofSeq
    |> Seq.toList

let private getAssemblyVersionInfo attributes =
    match attributes |> Seq.tryFind (fun (attr : Attribute) -> attr.Name = "AssemblyVersion") with
    | Some attr -> attr.Value
    | None _ -> "\"" + buildVersion + "\""

let private getAssemblyInformationalVersion attributes =
    match attributes |> Seq.tryFind (fun (attr : Attribute) -> attr.Name = "AssemblyInformationalVersion") with
    | Some attr -> attr.Value
    | None _ -> getAssemblyVersionInfo attributes

let private getSortedAndNumberedAttributes (attrs: seq<Attribute>) =
    attrs
    |> Seq.mapi (fun index a -> index,a)
    |> Seq.groupBy (fun (_,attr) -> attr.StaticPropertyName)
    |> Seq.collect (fun (_,group) -> group |> Seq.mapi (fun i a -> i,a))
    |> Seq.sortBy (fun (_,(index,_)) -> index)
    |> Seq.map (fun (id,(_,attr)) ->
        let name = if id = 0 then attr.StaticPropertyName else sprintf "%s_%d" attr.StaticPropertyName id
        (name, attr.StaticPropertyType, attr.StaticPropertyValue))
   

/// Creates a C# AssemblyInfo file with the given attributes and configuration.
/// The generated AssemblyInfo file contains an AssemblyVersionInformation class which can be used to retrieve the current version no. from inside of an assembly.
let CreateCSharpAssemblyInfoWithConfig outputFileName attributes (config : AssemblyInfoFileConfig) =
    traceStartTask "AssemblyInfo" outputFileName
    let generateClass, useNamespace = config.GenerateClass, config.UseNamespace

    let attributeLines =
        "// <auto-generated/>" :: (getDependencies attributes |> List.map (sprintf "using %s;"))
        @ [ "" ]
          @ (attributes
             |> Seq.toList
             |> List.map (fun (attr : Attribute) -> sprintf "[assembly: %sAttribute(%s)]" attr.Name attr.Value))

    let sourceLines =
        if generateClass then
            let consts =
                attributes
                |> getSortedAndNumberedAttributes 
                |> Seq.map (fun (name, attrtype, value) ->
                    sprintf "        internal const %s %s = %s;" attrtype name value)
                |> Seq.toList
            [ sprintf "namespace %s {" useNamespace
              "    internal static class AssemblyVersionInformation {" ]
            @ consts @ [ "    }"; "}" ]
        else []

    attributeLines @ sourceLines
    |> writeToFile outputFileName
    traceEndTask "AssemblyInfo" outputFileName

/// Creates a F# AssemblyInfo file with the given attributes and configuration.
/// The generated AssemblyInfo file contains an AssemblyVersionInformation class which can be used to retrieve the current version no. from inside of an assembly.
let CreateFSharpAssemblyInfoWithConfig outputFileName attributes (config : AssemblyInfoFileConfig) =
    traceStartTask "AssemblyInfo" outputFileName
    let generateClass, useNamespace = config.GenerateClass, config.UseNamespace

    let sourceLines =
        [
            yield "// Auto-Generated by FAKE; do not edit"
            yield sprintf "namespace %s" useNamespace
            yield! getDependencies attributes |> Seq.map (sprintf "open %s")
            yield ""
            yield!
                attributes
                |> Seq.map (fun (attr : Attribute) -> sprintf "[<assembly: %sAttribute(%s)>]" attr.Name attr.Value)
                  
            yield "do ()"; yield ""

            if generateClass then
                yield "module internal AssemblyVersionInformation ="
                yield!
                    // it might be that a specific assembly has multiple attributes of the same name
                    // if more than one occurences appear, append numerical suffixes to avoid compile errors
                    attributes 
                    |> getSortedAndNumberedAttributes 
                    |> Seq.map (fun (name, _, value) -> sprintf "    let [<Literal>] %s = %s" name value) 
        ]

    sourceLines |> writeToFile outputFileName
    traceEndTask "AssemblyInfo" outputFileName

/// Creates a VB AssemblyInfo file with the given attributes and configuration.
/// The generated AssemblyInfo file contains an AssemblyVersionInformation class which can be used to retrieve the current version no. from inside of an assembly.
let CreateVisualBasicAssemblyInfoWithConfig outputFileName attributes (config : AssemblyInfoFileConfig) =
    traceStartTask "AssemblyInfo" outputFileName
    let generateClass, useNamespace = config.GenerateClass, config.UseNamespace

    let attributeLines =
        "' <auto-generated/>" :: (getDependencies attributes |> List.map (sprintf "Imports %s"))
        @ [ "" ]
          @ (attributes
             |> Seq.toList
             |> List.map (fun (attr : Attribute) -> sprintf "<assembly: %sAttribute(%s)>" attr.Name attr.Value))

    let sourceLines =
        if generateClass then
            let consts =
                attributes
                |> getSortedAndNumberedAttributes 
                |> Seq.map(fun (name, attrtype, value) -> sprintf "    Friend Const %s As %s = %s" name attrtype value)
                |> Seq.toList
            "Friend NotInheritable Class AssemblyVersionInformation"::consts @ [ "End Class" ]
        else []

    attributeLines @ sourceLines
    |> writeToFile outputFileName
    traceEndTask "AssemblyInfo" outputFileName

/// Creates a C++/CLI AssemblyInfo file with the given attributes and configuration.
/// Does not generate an AssemblyVersionInformation class.
let CreateCppCliAssemblyInfoWithConfig outputFileName attributes (config : AssemblyInfoFileConfig) =
    traceStartTask "AssemblyInfo" outputFileName
    let generateClass, useNamespace = config.GenerateClass, config.UseNamespace
    //C++/CLI namespaces cannot be fully qualified; you must
    // namespace Namespace1 {  namespace Namespace2 { }} //etc

    let attributeLines =
        "// <auto-generated/>" :: (getDependencies attributes |> List.map (String.collect(fun c -> if c = '.' then "::" else c.ToString()) >> sprintf "using namespace %s;" ))
        @ [ "" ]
          @ (attributes
             |> Seq.toList
             |> List.map (fun (attr : Attribute) -> sprintf "[assembly:%sAttribute(%s)];" attr.Name attr.Value))

    attributeLines
    |> writeToFile outputFileName
    traceEndTask "AssemblyInfo" outputFileName

/// Creates a C# AssemblyInfo file with the given attributes.
/// The generated AssemblyInfo file contains an AssemblyVersionInformation class which can be used to retrieve the current version no. from inside of an assembly.
let CreateCSharpAssemblyInfo outputFileName attributes =
    CreateCSharpAssemblyInfoWithConfig outputFileName attributes AssemblyInfoFileConfig.Default

/// Creates a F# AssemblyInfo file with the given attributes.
/// The generated AssemblyInfo file contains an AssemblyVersionInformation class which can be used to retrieve the current version no. from inside of an assembly.
let CreateFSharpAssemblyInfo outputFileName attributes =
    CreateFSharpAssemblyInfoWithConfig outputFileName attributes AssemblyInfoFileConfig.Default

/// Creates a VB AssemblyInfo file with the given attributes.
/// The generated AssemblyInfo file contains an AssemblyVersionInformation class which can be used to retrieve the current version no. from inside of an assembly.
let CreateVisualBasicAssemblyInfo outputFileName attributes =
    CreateVisualBasicAssemblyInfoWithConfig outputFileName attributes AssemblyInfoFileConfig.Default

///  Creates a C++/CLI AssemblyInfo file with the given attributes.
let CreateCppCliAssemblyInfo outputFileName attributes =
    CreateCppCliAssemblyInfoWithConfig outputFileName attributes AssemblyInfoFileConfig.Default

let private removeAtEnd (textToRemove:string) (text:string) =
    if text.EndsWith(textToRemove) then
        text.Substring(0, text.Length - textToRemove.Length)
    else
        text

/// Read attributes from an AssemblyInfo file and return as a sequence of Attribute.
/// ## Parameters
///  - `assemblyInfoFile` - The file to read attributes from. Language C#, F#, VB or C++ is determined from the extension.
let GetAttributes assemblyInfoFile =
    let text = File.ReadAllText assemblyInfoFile

    // VB.NET is case-insensitive. Handle assembly attributes accordingly
    let (regex, additionalRegexOptions) =
        if assemblyInfoFile.ToLower().EndsWith(".cs") then (regexAttrNameValueCs, RegexOptions.None)
        elif assemblyInfoFile.ToLower().EndsWith(".fs") then (regexAttrNameValueFs, RegexOptions.None)
        elif assemblyInfoFile.ToLower().EndsWith(".vb") then (regexAttrNameValueVb, RegexOptions.IgnoreCase)
        elif assemblyInfoFile.ToLower().EndsWith(".cpp") then (regexAttrNameValueCpp, RegexOptions.None)
        else
            failwithf "Assembly info file type not supported: %s" assemblyInfoFile

    let combinedRegexOptions = RegexOptions.Multiline ||| additionalRegexOptions

    Regex.Matches(text, regex, combinedRegexOptions)
        |> Seq.cast<Match>
        |> Seq.map
            (fun m ->
                let v = m.Groups.["value"].Value
                let t = if v = "true" || v = "false" then typeof<bool>.FullName else typeof<string>.FullName
                Attribute(m.Groups.["name"].Value |> removeAtEnd "Attribute", v.Trim([|'"'|]), "", t)
            )

/// Read a single attribute from an AssemblyInfo file.
/// ## Parameters
///  - `attrName` - Name of the attribute without "Attribute" at the end.
///  - `assemblyInfoFile` - The file to read from. Language C#, F#, VB or C++ is determined from the extension.
let GetAttribute attrName assemblyInfoFile =
    assemblyInfoFile |> GetAttributes |> Seq.tryFind (fun a -> a.Name = attrName)

/// Read the value of a single attribute from an AssemblyInfo file. Note that string values are returned with surrounding "".
/// ## Parameters
///  - `attrName` - Name of the attribute without "Attribute" at the end.
///  - `assemblyInfoFile` - The file to read from. Language C#, F#, VB or C++ is determined from the extension.
let GetAttributeValue attrName assemblyInfoFile =
    match GetAttribute attrName assemblyInfoFile with
    | Some attr -> Some attr.Value
    | None -> None

let private updateAttr regexFactory additionalRegexOptions text (attribute:Attribute) =
    let regex = regexFactory attribute.Name

    let m = Regex.Match(text, regex, RegexOptions.Multiline ||| additionalRegexOptions)

    // Replace if found with different value
    if m.Success && m.Value <> attribute.Value then
        tracefn "Attribute '%s' updated: %s" attribute.Name attribute.Value
        Regex.Replace(text, regex, attribute.Value, RegexOptions.Multiline ||| additionalRegexOptions)

    // Do nothing if found with the same value
    elif m.Success then
        tracefn "Attribute '%s' is already correct: %s" attribute.Name attribute.Value
        text

    // Fail if not found
    else
        failwithf "Attribute '%s' not found" attribute.Name

/// Update a set of attributes in an AssemblyInfo file. Fails if any attribute is not found.
/// ## Parameters
///  - `assemblyInfoFile` - The file to update. Language C#, F#, VB or C++ is determined from the extension.
///  - `attributes` - The Attributes that should be updated matched on Name (Namespace is not used).
let UpdateAttributes assemblyInfoFile (attributes: seq<Attribute>) =
    tracefn "Updating attributes in: %s" assemblyInfoFile

    // VB.NET is case-insensitive. Handle assembly attributes accordingly
    let (regexFactory, additionalRegexOptions) =
        if assemblyInfoFile.ToLower().EndsWith(".cs") then (regexAttrValueCs, RegexOptions.None)
        elif assemblyInfoFile.ToLower().EndsWith(".fs") then (regexAttrValueFs, RegexOptions.None)
        elif assemblyInfoFile.ToLower().EndsWith(".vb") then (regexAttrValueVb, RegexOptions.IgnoreCase)
        elif assemblyInfoFile.ToLower().EndsWith(".cpp") then (regexAttrValueCpp, RegexOptions.None)
        else
            failwithf "Assembly info file type not supported: %s" assemblyInfoFile

    let text = File.ReadAllText assemblyInfoFile
    let newText = attributes |> Seq.fold (updateAttr regexFactory additionalRegexOptions) text

    File.WriteAllText(assemblyInfoFile, newText)
