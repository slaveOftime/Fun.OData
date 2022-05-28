namespace ODataDemo.Server

open Microsoft.AspNetCore.Mvc.Rendering
open Fun.Blazor


type PageMode =
    | WASM
    | SERVER

    member this.UrlPath =
        match this with
        | WASM -> "wasm" // This should be synced with the StaticWebAssetBasePath which defined in ODataDemo.Wasm.fsproj file
        | SERVER -> "server"

    member this.RenderMode =
        match this with
        | SERVER -> RenderMode.ServerPrerendered
        | WASM -> RenderMode.WebAssemblyPrerendered


module Pages =

    let create (mode: PageMode) ctx =
        let rootView = rootComp<ODataDemo.Wasm.App.AppComp> ctx mode.RenderMode

        let blazorJs =
            fragment {
                match mode with
                | SERVER -> script { src "/_framework/blazor.server.js" }
                | WASM _ -> script { src "_framework/blazor.webassembly.js" }
#if DEBUG
                html.hotReloadJSInterop
#endif
            }

        fragment {
            doctype "html"
            html' {
                head {
                    title { "ODataDemo" }
                    meta { charset "utf-8" }
                    meta {
                        name "viewport"
                        content "width=device-width, initial-scale=1.0"
                    }
                    link {
                        rel "shortcut icon"
                        type' "image/x-icon"
                        href $"/{PageMode.WASM}/favicon.ico"
                    }
                    baseUrl $"/{mode.UrlPath}/"
                }
                body {
                    rootView
                    blazorJs
                }
            }
        }
