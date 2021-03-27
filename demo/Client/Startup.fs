module Client.Startup

open Elmish
open Elmish.React

#if DEBUG
open Elmish.HMR
#endif


Program.mkProgram App.States.init App.States.update (fun s d -> App.Views.App {| state = s; dispatch = d |})
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "root"
#if DEBUG
#endif
|> Program.run