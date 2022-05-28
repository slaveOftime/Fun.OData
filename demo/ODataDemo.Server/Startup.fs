#nowarn "0020"

open System
open System.Linq
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.OData
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.OpenApi.Models
open ODataDemo.Db
open ODataDemo.Server


let builder = WebApplication.CreateBuilder(Environment.GetCommandLineArgs())
let services = builder.Services

services.AddControllersWithViews().AddOData(fun options -> options.EnableQueryFeatures() |> ignore)
services.AddServerSideBlazor()
services.AddFunBlazorServer()

services.AddScoped<HttpClient>(fun (sp: IServiceProvider) ->
    let httpClient = new HttpClient()
    let httpContext = sp.GetService<IHttpContextAccessor>()
    httpClient.BaseAddress <- Uri(httpContext.HttpContext.Request.Scheme + "://" + httpContext.HttpContext.Request.Host.ToString())
    httpClient
)

services.AddSwaggerGen(fun options ->
    let securiyReq = OpenApiSecurityRequirement()
    securiyReq.Add(OpenApiSecurityScheme(Reference = OpenApiReference(Type = ReferenceType.SecurityScheme, Id = "Bearer")), [||])
    options.AddSecurityRequirement(securiyReq)
    options.OperationFilter<OpenApiOperationIgnoreFilter>() |> ignore
)

services.AddDbContext<DemoDbContext>() |> ignore


let app = builder.Build()


let db = app.Services.CreateScope().ServiceProvider.GetService<DemoDbContext>()
db.Database.EnsureCreated() |> ignore

if db.Users.Any() |> not then
    db.Users.Add(User(Name = "p1", CreatedDate = DateTime.Now, Roles = [ Role(Caption = "Admin", Credential = "2423") ].ToList()))
    db.Users.Add(User(Name = "p2", CreatedDate = DateTime.Now, Roles = [ Role(Caption = "Admin", Credential = "2423") ].ToList()))
    db.Users.Add(User(Name = "p3", CreatedDate = DateTime.Now, Roles = [ Role(Caption = "Guest", Credential = "1234") ].ToList()))
    db.SaveChanges() |> ignore


app.UseSwagger()
app.UseSwaggerUI()

app.UseBlazorFrameworkFiles($"/{WASM.UrlPath}")
app.UseStaticFiles()

app.UseRouting()

app.MapControllers()

app.MapBlazorHub($"/{SERVER.UrlPath}/_blazor")
app.MapFunBlazor($"/{SERVER.UrlPath}", Pages.create PageMode.SERVER)
app.MapFunBlazor($"/{WASM.UrlPath}", Pages.create PageMode.WASM)

app.MapFallback(
    RequestDelegate(fun ctx ->
        ctx.Response.Redirect($"/{WASM.UrlPath}")
        Task.CompletedTask
    )
)


app.Run()
