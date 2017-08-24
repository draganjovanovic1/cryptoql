// include Fake libs
#r "./packages/FAKE/tools/FakeLib.dll"

open System
open Fake
open Fake.XMLHelper

// Helpers
let getBuildNo () =
    match buildServer with
    | TeamCity | Jenkins | CCNet -> buildVersion
    | _ -> "0"

// Targets
Target "Clean" <| fun _ ->
    !! "src/*/*/bin"
     ++ "src/*/*/obj"
     ++ "test/*/*/obj"
     ++ "test/*/*/obj"
    |> CleanDirs

Target "Restore" <| fun _ ->
    DotNetCli.Restore <| fun p ->
        { p with
            NoCache = false }

Target "Build" <| fun _ ->
    DotNetCli.Build <| fun p ->
        { p with
            Configuration = "Release"
            AdditionalArgs = ["--no-restore"] }

Target "Test" <| fun _ ->
    !! "test/**/*.fsproj"
    |> Seq.iter (fun proj ->
        DotNetCli.Test <| fun p ->
            { p with
                Project = proj
                AdditionalArgs = ["--no-restore"; "--no-build"] }
    )

Target "RebuildAll" DoNothing

// Build order
"Clean"
  ==> "Restore"

"Restore"
  ?=> "Build"
  ==> "RebuildAll"

"Restore"
  ?=> "Test"
  ==> "RebuildAll"

"Restore"
  ==> "RebuildAll"

RunTargetOrDefault "RebuildAll"