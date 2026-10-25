open System.Reflection
open BenchmarkDotNet.Running
open Fun.OData.Query.Benchmarks


[<EntryPoint>]
let main args =
    QueryGeneration().CustomQueryWithDU() |> printfn "DU: %s"
    QueryGeneration().CustomQueryWithCE() |> printfn "CE: %s"

    BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args) |> ignore
    0
