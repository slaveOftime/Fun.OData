#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open System

open Fake.Core
open Fake.DotNet
open Fake
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators


Target.initEnvironment ()


let serverPath          = __SOURCE_DIRECTORY__ </> "src/Server"
let clientPath          = __SOURCE_DIRECTORY__ </> "src/Client"

let deployDir           = __SOURCE_DIRECTORY__ </> "deploy"
let publishDir          = deployDir </> "publish"
let clientDeployPath    = clientPath </> "deploy"


let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " +
            "Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg


let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore


let node = runTool (platformTool "node" "node.exe")
let yarn = runTool (platformTool "yarn" "yarn.cmd")

let dotnet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir


let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "opening browser failed"
    |> Proc.run
    |> ignore


let clearDeployFolder targetDir =
    !! (targetDir </> "*/FSharp.Core.resources.dll")
    |> Seq.map Path.getDirectory
    |> Shell.deleteDirs

    !! (targetDir </> "*.pdb")
    |> Seq.iter Shell.rm_rf    


Target.create "Clean" <| fun _ ->
    [ publishDir
      clientDeployPath ]
    |> Shell.cleanDirs


Target.create "InstallPackages" <| fun _ ->
    printfn "Node version:"
    node "--version" clientPath
    printfn "Npm version:"
    yarn "--version" clientPath
    yarn "install" clientPath


Target.create "Build" <| fun _ ->
    dotnet "build" serverPath
    yarn "webpack -p" clientPath


Target.create "RunClient" <| fun _ ->
    let client = async {
        yarn "tailwind build public/css/tailwind-source.css -o public/css/tailwind-generated.css" clientPath
        yarn "webpack-dev-server" clientPath
    }
    let browser = async {
        do! Async.Sleep 10000
        openBrowser "http://localhost:8080"
    }

    let vsCodeSession  = Environment.hasEnvironVar "vsCodeSession"

    [
        yield client
        if not vsCodeSession then yield browser 
    ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore


Target.create "RunServer" <| fun _ ->
    async {
        dotnet "watch run" serverPath
    }
    |> Async.RunSynchronously


Target.create "Bundle" <| fun _ ->
    let publishArgs = sprintf "publish -c Release -o \"%s\"" publishDir
    dotnet publishArgs serverPath
    clearDeployFolder publishDir

    let clientDir = publishDir </> "wwwroot"
    Shell.copyDir clientDir clientDeployPath FileFilter.allFiles

    !!(publishDir </> "**/*.*")
    |> Zip.zip publishDir (deployDir </> DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".zip")


open Fake.Core.TargetOperators

"Clean"
    ==> "InstallPackages"
    ==> "Build"
    ==> "Bundle"

"Clean"
    ==> "InstallPackages"
    ==> "RunClient"

"Clean"
    ==> "InstallPackages"
    ==> "RunServer"


Target.runOrDefaultWithArguments "Build"
