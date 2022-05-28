open BenchmarkDotNet.Running
open Fun.OData.Query.Benchmarks


QueryGeneration().CustomQueryWithDU() |> printfn "DU: %s"
QueryGeneration().CustomQueryWithCE() |> printfn "CE: %s"


BenchmarkRunner.Run(System.Reflection.Assembly.GetExecutingAssembly()) |> ignore
