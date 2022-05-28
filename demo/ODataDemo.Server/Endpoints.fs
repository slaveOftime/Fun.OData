namespace ODataDemo.Server

open System.Linq
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.OData.Query
open Microsoft.AspNetCore.OData.Results
open Microsoft.EntityFrameworkCore


[<ApiController; Route "/api">]
type Endpoints(db: Db.DemoDbContext) =
    inherit ControllerBase()

    [<HttpGet "Roles">]
    member _.GetRoles(options: ODataQueryOptions<_>) = options.Query(db.Roles.AsNoTracking())


    [<HttpGet "Users">]
    member _.GetUsers(options: ODataQueryOptions<_>) = options.Query(db.Persons.AsNoTracking())

    [<HttpGet "Users/{id}"; EnableQuery>]
    member _.GetUsers(id: int) = db.Persons.AsNoTracking().Where(fun x -> x.Id = id) |> SingleResult.Create
