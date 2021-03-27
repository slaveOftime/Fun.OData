#r "nuget: Fake.IO.FileSystem,5.20.3"
#r "nuget: Fake.DotNet.Cli,5.20.3"
#r "nuget: Fake.JavaScript.Yarn,5.20.3"

open System.IO
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.DotNet
open Fake.JavaScript


let watchFile fn file =
    let watcher = new FileSystemWatcher(Path.getDirectory file, Path.GetFileName file)
    watcher.NotifyFilter <-  NotifyFilters.CreationTime ||| NotifyFilters.Size ||| NotifyFilters.LastWrite
    watcher.Changed.Add (fun _ -> fn())
    watcher.EnableRaisingEvents <- true

let private runFable projectDir isDebug isWatch =
    let mode = match isWatch with false -> "" | true -> "watch"
    let config = if isDebug then " --define DEBUG" else ""
    DotNet.exec (fun x -> { x with WorkingDirectory = projectDir }) "fable" $"{mode} . --outDir ./www/fablejs{config}" |> ignore

let private cleanGeneratedJs projectDir = Shell.cleanDir (projectDir </> "www/fablejs")

let private buildTailwindCss projectDir =
    printfn "Build client css"
    Yarn.exec "tailwindcss build css/app.css -o css/tailwind-generated.css" (fun x -> { x with WorkingDirectory = projectDir </> "www" })

let private watchTailwindCss projectDir =
    !!(projectDir </> "www/css/app.css")
    ++(projectDir </> "www/tailwind.config.js")
    |> Seq.toList
    |> List.iter (watchFile (fun () -> buildTailwindCss projectDir))


let startDev projectDir port =
    cleanGeneratedJs projectDir
    buildTailwindCss projectDir

    runFable projectDir true false

    [
        async {
            runFable  projectDir true true
        }
        async {
            watchTailwindCss projectDir
            Shell.cleanDir (projectDir </> "www/.dist")
            Yarn.exec $"parcel index.html --dist-dir .dist --port %d{port}" (fun x -> { x with WorkingDirectory = projectDir </> "www" })
            printfn "Clean up..."
        }
    ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore


let bundle projectDir outDir =
    cleanGeneratedJs projectDir
    buildTailwindCss projectDir
    runFable projectDir false false
    Shell.cleanDir outDir
    Yarn.exec $"parcel build index.html --dist-dir {outDir} --public-url ./ --no-source-maps --no-cache" (fun x -> { x with WorkingDirectory = projectDir </> "www" })
