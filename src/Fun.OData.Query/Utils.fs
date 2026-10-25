[<AutoOpen>]
module Fun.OData.Query.Internal.Utils

open System
open System.Collections.Generic
open System.Linq.Expressions
open System.Reflection
open Microsoft.FSharp.Reflection


let private propertiesCache = Dictionary<Type, PropertyInfo[]>()
let private propertyNamesCache = Dictionary<Type, string list>()
let private cacheLock = obj()


type FSharpType =
    static member IsRecordArray(ty: Type) = ty.IsArray && FSharpType.IsRecord(ty.GetElementType())
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
    let isTransparentValue (memberInfo: MemberInfo) =
        let declaringType = memberInfo.DeclaringType
        memberInfo.Name = "Value"
        && not (isNull declaringType)
        && (declaringType.FullName.StartsWith "Microsoft.FSharp.Core.FSharpOption"
            || declaringType.FullName.StartsWith "System.Nullable")

    match exp.NodeType with
    | ExpressionType.MemberAccess ->
        let memberExp = exp :?> MemberExpression
        match memberExp.Expression with
        | null -> memberExp.Member.Name
        | parent when parent.NodeType = ExpressionType.Parameter -> memberExp.Member.Name
        | parent when isTransparentValue memberExp.Member -> getExpressionName parent
        | parent -> sprintf "%s.%s" (getExpressionName parent) memberExp.Member.Name
    | ExpressionType.Lambda -> getExpressionName ((exp :?> LambdaExpression).Body)
    | ExpressionType.Convert -> getExpressionName ((exp :?> UnaryExpression).Operand)
    | _ -> failwith "Unsupported expression"

let getProperties (sourceType: Type) =
    lock cacheLock (fun () ->
        match propertiesCache.TryGetValue sourceType with
        | true, fields -> fields
        | _ ->
            let fields =
                if FSharpType.IsRecord sourceType then
                    FSharpType.GetRecordFields sourceType
                elif FSharpType.IsRecordOption sourceType then
                    FSharpType.GetRecordFields sourceType.GenericTypeArguments.[0]
                else
                    sourceType.GetProperties()

            propertiesCache[sourceType] <- fields
            fields
    )

let getPropertiesForType sourceType =
    lock cacheLock (fun () ->
        match propertyNamesCache.TryGetValue sourceType with
        | true, names -> names
        | _ ->
            let names = getProperties sourceType |> Array.map (fun x -> x.Name) |> Array.toList
            propertyNamesCache[sourceType] <- names
            names
    )

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
