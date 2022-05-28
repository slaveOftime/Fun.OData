// hot-reload
[<AutoOpen>]
module ODataDemo.Wasm.App

open FSharp.Data.Adaptive
open Fun.Blazor
open Fun.Result


let private app =
    html.comp (fun (hook: IComponentHook) ->
        let top = cval 5
        let users = cval DeferredState<UserBrief list, string>.NotStartYet
        let userDetail = cval DeferredState<User, string>.NotStartYet


        let getUsers () =
            users.Publish DeferredState.Loading
            hook.LoadUsers(top = top.Value) |> Task.sleep 500 |> Task.map users.Publish |> ignore

        let getUserDetail id =
            userDetail.Publish DeferredState.Loading
            hook.LoadUserDetail(id) |> Task.sleep 500 |> Task.map userDetail.Publish |> ignore

        div {
            span { "Take: " }
            adaptiview () {
                let! top, setTop = top.WithSetter()
                input {
                    placeholder "take"
                    value top
                    onchange (fun e ->
                        match e.Value.ToString() with
                        | INT32 x ->
                            setTop x
                            getUsers ()
                        | _ -> ()
                    )
                }
            }
            button {
                onclick (fun _ -> getUsers ())
                "Load users"
            }
            section {
                adaptiview () {
                    match! users with
                    | DeferredState.Loaded users
                    | DeferredState.Reloading users ->
                        ul.create
                            [
                                for user in users do
                                    li {
                                        style { cursorPointer }
                                        onclick (fun _ -> getUserDetail user.Id)
                                        user.Name
                                    }
                            ]
                    | DeferredState.Loading -> span { "Loading users..." }
                    | DeferredState.NotStartYet -> span { "Not started yet" }
                    | DeferredState.LoadFailed msg
                    | DeferredState.ReloadFailed (_, msg) ->
                        span {
                            style { color "red" }
                            "Failed to load users: " + msg
                        }
                }
            }
            section {
                adaptiview () {
                    match! userDetail with
                    | DeferredState.Loaded user
                    | DeferredState.Reloading user ->
                        div { sprintf "Id: %d" user.Id }
                        div { sprintf "Name: %s" user.Name }
                        div { sprintf "Roles: %s" (user.Roles.ToString()) }
                    | DeferredState.Loading -> span { "Loading user detail..." }
                    | DeferredState.NotStartYet -> ()
                    | DeferredState.LoadFailed msg
                    | DeferredState.ReloadFailed (_, msg) ->
                        span {
                            style { color "red" }
                            "Failed to load user detail: " + msg
                        }
                }
            }
        }
    )


type AppComp() =
    inherit FunBlazorComponent()

    override _.Render() =
#if DEBUG
        html.hotReloadComp (app, "ODataDemo.Wasm.App.app")
#else
        app
#endif
