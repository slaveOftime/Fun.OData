module Client.App.Views

open Fable.React
open Fable.React.Props
open Zanaptak.TypedCssClasses
open Fun.LightForm.FormView


type Tw = CssClasses<"./public/css/tailwind-generated.css", Naming.Verbatim>
type Fa = CssClasses<"./public/css/font-awesome-v5-10-2.min.css", Naming.Verbatim>


let app state dispatch =
    let field key renderF renderArgs = Form.field state.FilterForm (FilterFormMsg >> dispatch) key (renderF renderArgs)
    div </> [
      Classes [
        Tw.``sm:w-full``
        Tw.``md:w-03/04``
        Tw.``lg:w-02/03``
        Tw.``mx-auto``
      ]
      Children [
        div </> [
          Children [
            field "PageSize" Form.input [
              InputProp.Label "PageSize"
            ]
            field "Page" Form.input [
              InputProp.Label "Page"
            ]
            field "SearchName" Form.input [
              InputProp.Label "Search by Name"
            ]
            field "MinPrice" Form.input [
              InputProp.Label "Min Price"
              InputProp.ConvertTo InputValue.Number
            ]
            field "FromCreatedDate" Form.input [
              InputProp.Label "From Created Date"
              InputProp.ConvertTo InputValue.Date
            ]
            field "ToCreatedDate" Form.input [
              InputProp.Label "To Created Date"
              InputProp.ConvertTo InputValue.Date
            ]

            field "QueryType" Form.selector [
              SelectorProp.Label "Server query type"
              SelectorProp.OnlyOne true
              SelectorProp.Source [
                QueryType.Simple, "Simple"
                QueryType.Fluent, "Fluent"
                QueryType.Pro, "Pro"
              ]
            ]

            button </> [
              Children [
                str "Load Data"
                if state.IsLoading then
                  span </> [
                    Classes [
                      Fa.fas
                      Fa.``fa-spinner``
                      Tw.``ml-02``
                      Fa.``fa-spin``
                    ]
                  ]
              ]
              OnClick (fun _ -> LoadData |> dispatch)
              Classes [
                Tw.``m-02``
                Tw.``bg-green-600``
                Tw.``text-white``
                Tw.``px-02``
                Tw.``py-01``
                Tw.shadow
              ]
            ]
          ]
          Classes [
            Tw.``shadow-xl``
            Tw.``m-02``
          ]
        ]

        match state.ErrorInfo with
          | None -> ()
          | Some e ->
              div </> [
                Text e
                Classes [
                  Tw.``m-02``
                  Tw.``px-02``
                  Tw.``bg-red-200``
                  Tw.``text-red-600``
                ]
              ]

        match state.ODataQuery with
          | None -> ()
          | Some q ->
              div </> [
                Children [
                  div </> [ Text "OData query:" ]
                  div </> [ Text q; Classes [ Tw.``break-words`` ] ]
                ]
                Classes [
                  Tw.``m-02``
                  Tw.``px-02``
                  Tw.``bg-green-200``
                ]
              ]

        div </> [
          Text (sprintf "Total count %d" state.TotalCount)
          Classes [
            Tw.``m-02``
            Tw.``font-bold``
          ]
        ]

        div </> [
          Children [
            for data in state.Data do
              div </> [
                Children [
                  div </> [
                    Text (sprintf "%A" data)
                    Classes [
                      Tw.``px-02``
                    ]
                  ]
                  match state.Detail with
                    | Some detail when detail.Id = data.Id ->
                        div </> [
                          Text (sprintf "%A" detail)
                          Classes [
                            Tw.``my-02``
                            Tw.``px-02``
                            Tw.``bg-indigo-300``
                          ]
                        ]
                    | _ -> ()
                ]
                OnClick (fun _ -> LoadDataById data.Id |> dispatch)
                Classes [
                  Tw.``m-02``
                  Tw.``bg-blue-300``
                  Tw.``hover:bg-blue-400``
                  Tw.``cursor-pointer``
                  Tw.``hover:shadow-lg``
                ]
              ]
          ]
        ]
      ]
    ]
