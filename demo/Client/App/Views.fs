module Client.App.Views

open System
open Feliz
open Zanaptak.TypedCssClasses
open Fable.MaterialUI
open Fun.LightForm

open type Html
open type prop


let [<Literal>] TwCss = __SOURCE_DIRECTORY__ + "/../www/css/tailwind-generated.css"
type Tw = CssClasses<TwCss, Naming.Verbatim>



let simpleInput props (field: FormField<_>) dispatch =
    div [
        classes [ Tw.``px-2``; Tw.``py-2`` ]
        children [
            div [
                text field.Name
                classes [ Tw.``text-xs``; Tw.``text-gray-600``; Tw.``mb-1`` ]
            ]
            input [
                yield! props
                classes [  Tw.``bg-gray-200``; Tw.``hover:bg-gray-300``; Tw.``px-2``; Tw.``py-1``; Tw.rounded ]
                value (field.RawValue |> string)
                onChange (fun (s: string) -> LightFormMsg.ChangeField(field.Name, box s) |> dispatch)
            ]
        ]
    ]

let simpleDateInput props (field: FormField<_>) dispatch =
    div [
        classes [ Tw.``px-2``; Tw.``py-2`` ]
        children [
            div [
                text field.Name
                classes [ Tw.``text-xs``; Tw.``text-gray-600``; Tw.``mb-1`` ]
            ]   
            input [
                yield! props
                classes [  Tw.``bg-gray-200``; Tw.``hover:bg-gray-300``; Tw.``px-2``; Tw.``py-1``; Tw.rounded ]
                value (field.RawValue |> string)
                onChange (fun (s: string) -> LightFormMsg.ChangeField(field.Name, box (DateTime.Parse s)) |> dispatch)
                type' "Date"
            ]
        ]
    ]


[<ReactComponent>]
let App (props: {| state: State; dispatch: Msg -> unit |}) =
    let filter, _ = LightForm.useLightForm(Filter.defaultValue, Validation.emptyValidators)
    let field key renderF renderArgs = filter.CreateField key (renderF renderArgs)

    div [
        classes [ Tw.``sm:w-full``; Tw.``md:w-3/4``; Tw.``lg:w-2/3``; Tw.``mx-auto`` ]
        children [
            div [
                classes [ Tw.``shadow-xl``; Tw.``m-2``; Tw.flex; Tw.``flex-row``; Tw.``flex-wrap`` ]
                children [
                    field "PageSize" simpleInput []
                    field "Page" simpleInput []
                    field "SearchName" simpleInput []
                    field "MinPrice" simpleInput [ type' "number" ]
                    field "FromCreatedDate" simpleDateInput []
                    field "ToCreatedDate" simpleDateInput []

                    filter.CreateField "QueryType" (fun field dispatch ->
                        select [
                            children (
                                [
                                    QueryType.Simple, "Simple"
                                    QueryType.Fluent, "Fluent"
                                    QueryType.Pro, "Pro"
                                ]
                                |> List.map (fun (k, name) ->
                                    li [
                                        value (string k)
                                        text name
                                    ])
                            )
                            onSelect (fun (e: Browser.Types.Event) -> LightFormMsg.ChangeField(field.Name, box e.returnValue) |> dispatch)
                        ]
                    )

                    button [
                        children [
                            span "Load Data"
                            if props.state.IsLoading then Icons.accessAlarmIcon []
                        ]
                        onClick (fun _ -> 
                            OnFilterChange (filter.GetValue()) |> props.dispatch
                            LoadData |> props.dispatch)
                        classes [ Tw.``m-2``; Tw.``bg-green-600``; Tw.``text-white``; Tw.``px-2``; Tw.``py-1``; Tw.shadow ]
                    ]
                ]
            ]

            match props.state.ErrorInfo with
                | None -> ()
                | Some e ->
                    div [
                        text e
                        classes [ Tw.``m-2``; Tw.``px-2``; Tw.``bg-red-200``; Tw.``text-red-600`` ]
                    ]

            match props.state.ODataQuery with
                | None -> ()
                | Some q ->
                    div [
                        children [
                            div "OData query:"
                            div [ text q; classes [ Tw.``break-words`` ] ]
                        ]
                        classes [ Tw.``m-2``; Tw.``px-2``; Tw.``bg-green-200`` ]
                    ]

            div [
                text (sprintf "Total count %d" props.state.TotalCount)
                classes [ Tw.``m-2``; Tw.``font-bold`` ]
            ]

            div [
                children [
                    for data in props.state.Data do
                        div [
                            children [
                                div [
                                    text (sprintf "%A" data)
                                    classes [ Tw.``px-2``]
                                ]
                                match props.state.Detail with
                                | Some detail when detail.Id = data.Id ->
                                    div [
                                        text (sprintf "%A" detail)
                                        classes [ Tw.``my-2``; Tw.``px-2``; Tw.``bg-indigo-300`` ]
                                    ]
                                | _ -> ()
                            ]
                            onClick (fun _ -> LoadDataById data.Id |> props.dispatch)
                            classes [ Tw.``m-2``; Tw.``bg-blue-300``; Tw.``hover:bg-blue-400``; Tw.``cursor-pointer``; Tw.``hover:shadow-lg`` ]
                        ]
                ]
            ]
        ]
    ]
