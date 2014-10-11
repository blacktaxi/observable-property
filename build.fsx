// include Fake lib
#r @"packages/FAKE/tools/FakeLib.dll"
open System

open Fake
open Fake.FileUtils

let inline (---) (x : Fake.FileSystem.FileIncludes) (patterns : string list) =
    List.fold (--) x patterns

let buildDir = "./bin"

// Target for test assemblies
let testDir = buildDir + "/test"

let srcRoot = "./src"

let srcProjects =
    !! (srcRoot + "/ObservableProperty/ObservableProperty.fsproj")

let testProjects = 
    !! (srcRoot + "/**/*.Test.csproj") ++ (srcRoot + "/**/*.Test.fsproj")

// targets
Target "Clean" <| fun _ ->
    CleanDirs [buildDir; testDir]

Target "Build" <| fun _ ->
    srcProjects
    |> MSBuild buildDir "Build" ["Configuration", "Release"; "Platform", "AnyCPU"]
    |> Log ("Build-Release" + "-Output: ")

Target "BuildTests" <| fun _ ->
    testProjects
    |> MSBuildRelease testDir "Build"
    |> Log "TestBuild-Output: "

Target "RunTests" <| fun _ ->
    !! (testDir + "/*.Test.dll")
    |> xUnit (fun p ->
        { p with
            XmlOutput = false
            OutputDir = testDir
            NUnitXmlOutput = false
            Verbose = true
            ErrorLevel = DontFailBuild })

Target "All" DoNothing

"Clean"
    ==> "Build"
    ==> "RunTests"
    ==> "All"

"BuildTests" ==> "RunTests"

// start build
RunTargetOrDefault "All"