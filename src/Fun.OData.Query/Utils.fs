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


let generateSelectQueryByType lowerFirstCase sourceType =
    let ty =
        if FSharpType.IsRecord sourceType then
            sourceType
        elif FSharpType.IsRecordOption sourceType then
            sourceType.GenericTypeArguments.[0]
        else
            failwith "Only support fsharp record"

    FSharpType.GetRecordFields ty
    |> Seq.map (fun x -> x.Name)
    |> Seq.map (fun x ->
        if lowerFirstCase then
            if String.IsNullOrEmpty x then x
            elif x.Length = 1 then x.ToLower()
            else Char.ToLower(x.[0]).ToString() + x.Substring(1)
        else
            x
    )
    |> fun x -> String.Join(",", x)
