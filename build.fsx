#r "nuget: Fake.Core.Process,5.20.3"
#r "nuget: Fake.IO.FileSystem,5.20.3"
#r "nuget: Fake.DotNet.Cli,5.20.3"
#r "nuget: Fake.JavaScript.Yarn,5.20.3"
#r "nuget: BlackFox.Fake.BuildTask,0.1.3"

#load "./scripts/WebClient.fsx"


open Fake.IO.FileSystemOperators
open Fake.DotNet
open Fake.JavaScript
open BlackFox.Fake

let clientProjectDir = "./demo/Client"
let publishDir = __SOURCE_DIRECTORY__ </> "publish"


fsi.CommandLineArgs
|> Array.skip 1
|> BuildTask.setupContextFromArgv 


let checkEnv =
    BuildTask.create "CheckEnv" [] {
        Yarn.exec "--version" id
        Yarn.install (fun x -> { x with WorkingDirectory = clientProjectDir </> "www" })
        DotNet.exec id "tool restore" "" |> ignore
    }

let startCientDev =
    BuildTask.create "StartClientDev" [ checkEnv ] {
        WebClient.startDev clientProjectDir 8080
    }

let bundleClient =
    BuildTask.create "BundleClient" [ checkEnv ] {
        WebClient.bundle clientProjectDir (publishDir </> "client")
    }


BuildTask.runOrDefault startCientDev
