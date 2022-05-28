namespace ODataDemo.Server

open System
open System.Linq
open System.Collections.Generic
open Microsoft.OpenApi.Models
open Microsoft.AspNetCore.Mvc.ApiExplorer
open Microsoft.AspNetCore.Mvc.ModelBinding.Metadata
open Microsoft.AspNetCore.OData.Query
open Swashbuckle.AspNetCore.SwaggerGen


type OpenApiParameterIgnoreAttribute() =
    inherit System.Attribute()


type OpenApiOperationIgnoreFilter() =

    let shouldIgnore (parameterDescription: ApiParameterDescription) =
        match parameterDescription.ModelMetadata with
        | :? DefaultModelMetadata as metadata ->
            let isODataQuery () =
                if metadata.UnderlyingOrModelType <> null && metadata.UnderlyingOrModelType.FullName <> null then
                    metadata.UnderlyingOrModelType.FullName.StartsWith("Microsoft.AspNetCore.OData.Query.ODataQueryOptions")
                else
                    false

            let hasIgnoreAttribute () =
                if metadata.Attributes.ParameterAttributes <> null then
                    metadata.Attributes.ParameterAttributes.Any(fun attribute -> attribute.GetType() = typeof<OpenApiParameterIgnoreAttribute>)
                else
                    false

            isODataQuery () || hasIgnoreAttribute ()

        | _ -> false


    let makeQuery name ty =
        OpenApiParameter(
            Name = name,
            AllowReserved = true,
            AllowEmptyValue = false,
            Required = false,
            In = ParameterLocation.Query,
            Schema = OpenApiSchema(Type = ty)
        )

    let addODataQuery (ps: System.Collections.Generic.IList<OpenApiParameter>) =
        ps.Add(makeQuery "$select" "string")
        ps.Add(makeQuery "$filter" "string")
        ps.Add(makeQuery "$expand" "string")


    interface IOperationFilter with

        member _.Apply(operation, context) =
            if operation <> null
               && context <> null
               && context.ApiDescription <> null
               && context.ApiDescription.ParameterDescriptions <> null then

                let mutable isODataOperation = false
                let mutable isODataQueryAdded = false

                context.ApiDescription.ParameterDescriptions
                |> Seq.filter shouldIgnore
                |> Seq.iter (fun parameterToHide ->
                    isODataOperation <- true

                    let parameter =
                        operation.Parameters.FirstOrDefault(fun parameter ->
                            String.Equals(parameter.Name, parameterToHide.Name, System.StringComparison.Ordinal)
                        )
                    if parameter <> null then
                        operation.Parameters.Remove(parameter) |> ignore
                        if not isODataQueryAdded then
                            operation.Parameters |> addODataQuery
                            operation.Parameters.Add(makeQuery "$top" "integer")
                            operation.Parameters.Add(makeQuery "$skip" "integer")
                            operation.Parameters.Add(makeQuery "$count" "bool")
                            isODataQueryAdded <- true
                )

                if not isODataQueryAdded
                   && context.MethodInfo.CustomAttributes.Any(fun x -> x.AttributeType = typeof<EnableQueryAttribute>) then
                    operation.Parameters |> addODataQuery
                    isODataOperation <- true


                if isODataOperation then
                    operation.Responses.Clear()
                    operation.Responses.Add("200", OpenApiResponse(Content = dict [ "application/json", OpenApiMediaType() ]))
                else
                    for resp in operation.Responses do
                        resp.Value.Content <- resp.Value.Content.Where(fun kv -> kv.Key.StartsWith "application/json;odata" |> not) |> Dictionary
