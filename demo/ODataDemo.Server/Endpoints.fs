namespace ODataDemo.Server

open System.Linq
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.OData.Query
open Microsoft.AspNetCore.OData.Results
open Microsoft.EntityFrameworkCore
open ODataDemo.Db


[<ApiController; Route "/api">]
type Endpoints(db: DemoDbContext) =
    inherit ControllerBase()

    [<HttpGet "Roles">]
    member _.GetRoles(options: ODataQueryOptions<_>) = options.Query(db.Roles.AsNoTracking())


    [<HttpGet "Users">]
    member _.GetUsers(options: ODataQueryOptions<_>) = options.Query(db.Users.AsNoTracking())

    [<HttpGet "Users/{id}"; EnableQuery>]
    member _.GetUsers(id: int) = db.Users.AsNoTracking().Where(fun x -> x.Id = id) |> SingleResult.Create
