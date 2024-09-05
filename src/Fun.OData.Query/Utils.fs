[<AutoOpen>]
module Fun.OData.Query.Internal.Utils

open System
open System.Linq.Expressions
open Microsoft.FSharp.Reflection


type FSharpType =
    static member IsRecordArray(ty: Type) = ty.IsArray && FSharpType.IsRecord ty.GenericTypeArguments.[0]
    static member IsRecordList(ty: Type) =
        ty.FullName.StartsWith "Microsoft.FSharp.Collections.FSharpList" && FSharpType.IsRecord ty.GenericTypeArguments.[0]

    static member TryGetIEnumeralbleGenericType(ty: Type) =
        ty.GetInterfaces()
        |> Seq.tryPick (fun x ->
            if x.FullName.StartsWith "System.Collections.Generic.IEnumerable`" && FSharpType.IsRecord x.GenericTypeArguments.[0] then
                Some x.GenericTypeArguments[0]
            else
                None
        )

    static member IsRecordOption(ty: Type) =
        ty.FullName.StartsWith "Microsoft.FSharp.Core.FSharpOption" && FSharpType.IsRecord ty.GenericTypeArguments.[0]


let rec getExpressionName (exp: Expression) =
    match exp.NodeType with
    | ExpressionType.MemberAccess -> (exp :?> MemberExpression).Member.Name
    | ExpressionType.Lambda -> ((exp :?> LambdaExpression).Body :?> MemberExpression).Member.Name
    | ExpressionType.Convert -> getExpressionName (exp :?> UnaryExpression).Operand
    | _ -> failwith "Unsupported expression"

let getPropertiesForType sourceType =
    if FSharpType.IsRecord sourceType then
        FSharpType.GetRecordFields sourceType |> Seq.map (fun x -> x.Name)
    elif FSharpType.IsRecordOption sourceType then
        FSharpType.GetRecordFields sourceType.GenericTypeArguments.[0] |> Seq.map (fun x -> x.Name)
    else
        sourceType.GetProperties() |> Seq.map (fun x -> x.Name)
    |> Seq.toList

let generateSelectQueryByPropertyNames lowerFirstCase names =
    names
    |> Seq.map (fun x ->
        if lowerFirstCase then
            if String.IsNullOrEmpty x then x
            elif x.Length = 1 then x.ToLower()
            else Char.ToLower(x.[0]).ToString() + x.Substring(1)
        else
            x
    )
    |> fun x -> String.Join(",", x)

let generateSelectQueryByType lowerFirstCase sourceType =
    getPropertiesForType sourceType |> generateSelectQueryByPropertyNames lowerFirstCase
