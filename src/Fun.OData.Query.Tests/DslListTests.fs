module Fun.OData.Query.Tests.ListQueryGenerationTests

open System
open Xunit
open Fun.OData.Query


type Contact = { Phone: string; Email: string }
type Address = { Street: string; Room: int }

type Person =
    {
        Name: string
        Age: int
        Contact: Contact
        Addresses: Address list
    }

type AnonymousPerson =
    {| Id: int
       Name: string
       Test1: {| Id: Guid; Name: string; Contact: Contact |}
       Test2: {| Id: Guid; Name: string |} option
       Test3: {| Id: int |} []
       Test4: {| Id: int |} list |}

type NCFolderStatusWithRoles =
    {
        Nr: int
        Pos: int voption
        Descript: string
        EzdNCFolderStatus: EzdNCFolderStatusBrief option
        NcroleFolderStati: {| UserRoleId: int |} list
    }
and EzdNCFolderStatusBrief = { Color: string }


let inline expectQuery (x: string) y = Assert.Equal(x, y)


[<Fact>]
let ``select should work`` () =
    Query.generateSelectQuery<Contact> ()
    |> expectQuery "Phone,Email"

    [ SelectType typeof<Contact> ]
    |> Query.generate
    |> expectQuery "?$select=Phone,Email"

    [ Select "Phone" ] |> Query.generate |> expectQuery "?$select=Phone"

    // Later select overrides the previous one
    [ Select "Phone"; Select "Email" ]
    |> Query.generate
    |> expectQuery "?$select=Email"

    [ Select "Phone"; SelectType typeof<Contact> ]
    |> Query.generate
    |> expectQuery "?$select=Phone,Email"

    [ SelectType typeof<Contact>; Select "Phone" ]
    |> Query.generate
    |> expectQuery "?$select=Phone"


[<Fact>]
let ``generateFor should generate select and expand`` () =
    Query.generateFor<Contact> []
    |> expectQuery "?$select=Phone,Email"

    Query.generateFor<Person> []
    |> expectQuery
        "?$expand=Contact($select=Phone,Email),Addresses($select=Street,Room)&$select=Name,Age,Contact,Addresses"

    Query.generateFor<AnonymousPerson> []
    |> expectQuery
        "?$expand=Test1($expand=Contact($select=Phone,Email);$select=Contact,Id,Name),Test2($select=Id,Name),Test3($select=Id),Test4($select=Id)&$select=Id,Name,Test1,Test2,Test3,Test4"


[<Fact>]
let ``multiple ExpandEx should work`` () =
    Query.generateFor<NCFolderStatusWithRoles> [
        ExpandEx [ "NcroleFolderStati", [ Select "UserRoleId" ] ]
        ExpandEx [ "EzdNCFolderStatus", [ SelectType typeof<EzdNCFolderStatusBrief> ] ]
    ]
    |> expectQuery
        "?$expand=EzdNCFolderStatus($select=Color),NcroleFolderStati($select=UserRoleId)&$select=Nr,Pos,Descript,EzdNCFolderStatus,NcroleFolderStati"

    Query.generateFor<NCFolderStatusWithRoles> []
    |> expectQuery
        "?$expand=EzdNCFolderStatus($select=Color),NcroleFolderStati($select=UserRoleId)&$select=Nr,Pos,Descript,EzdNCFolderStatus,NcroleFolderStati"

    [ ExpandEx [ "Foo", [ Select "A" ] ]; ExpandEx [ "Bar", [ Count ] ] ]
    |> Query.generate
    |> expectQuery "?$expand=Foo($select=A),Bar($count=true)"

    [ ExpandEx [ "Foo", [ Select "A"; ExpandEx [ "Bar", [ Count ] ]; Filter "A eq 1" ] ] ]
    |> Query.generate
    |> expectQuery "?$expand=Foo($expand=Bar($count=true);$select=A;$filter=(A eq 1))"


[<Fact>]
let ``expand should work`` () =
    [ Expand "Foo"; Expand "Bar" ]
    |> Query.generate
    |> expectQuery "?$expand=Foo,Bar"

    [ Expand "Foo"; ExpandEx [ "Bar", [ Select "Id" ] ] ]
    |> Query.generate
    |> expectQuery "?$expand=Foo,Bar($select=Id)"

    // A later expand for the same field overrides the previous one
    [ Expand "Foo"; ExpandEx [ "Foo", [ Select "Id" ] ] ]
    |> Query.generate
    |> expectQuery "?$expand=Foo($select=Id)"

    [ ExpandEx [ "Foo", [ Select "Id" ] ]; Expand "Foo" ]
    |> Query.generate
    |> expectQuery "?$expand=Foo"

    [ ExpandEx [ "Foo", [] ] ] |> Query.generate |> expectQuery "?$expand=Foo"
    [ ExpandEx [] ] |> Query.generate |> expectQuery "?"
    [ Expand "" ] |> Query.generate |> expectQuery "?"


[<Fact>]
let ``orderBy with multiple fields should work`` () =
    [ OrderBy "Phone"; OrderBy "Email" ]
    |> Query.generate
    |> expectQuery "?$orderBy=Phone,Email"

    [ OrderByDesc "Phone"; OrderByDesc "Email" ]
    |> Query.generate
    |> expectQuery "?$orderBy=Phone desc,Email desc"

    [ OrderBy "Phone"; OrderByDesc "Email" ]
    |> Query.generate
    |> expectQuery "?$orderBy=Phone,Email desc"

    [ OrderBy ""; OrderByDesc "" ] |> Query.generate |> expectQuery "?"


[<Fact>]
let ``paging and count should work`` () =
    [ Count; Take 5; Skip 10 ]
    |> Query.generate
    |> expectQuery "?$count=true&$skip=10&$top=5"

    // Later take and skip override the previous ones
    [ Count; Take 5; Take 10 ]
    |> Query.generate
    |> expectQuery "?$count=true&$top=10"

    [ Count; Skip 1; Skip 2 ]
    |> Query.generate
    |> expectQuery "?$count=true&$skip=2"


[<Fact>]
let ``filters should combine with and`` () =
    [ Filter "A eq 1"; Filter "B eq 2" ]
    |> Query.generate
    |> expectQuery "?$filter=(A eq 1) and (B eq 2)"

    [ Filter ""; Filter null ] |> Query.generate |> expectQuery "?"

    [ Filter "Age gt 10"; Filter "Age lt 20"; Count; Select "Name" ]
    |> Query.generate
    |> expectQuery "?$count=true&$select=Name&$filter=(Age gt 10) and (Age lt 20)"

    [ Filter (andQueries [ gt "Age" 10; lt "Age" 20 ]) ]
    |> Query.generate
    |> expectQuery "?$filter=((Age gt 10) and (Age lt 20))"

    [ Filter (orQueries [ contains "Name" "a b"; eq "Phone" "123" ]) ]
    |> Query.generate
    |> expectQuery "?$filter=((contains(Name, 'a+b')) or (Phone eq 123))"


[<Fact>]
let ``external queries should work`` () =
    [ External "etest1=123"; External "etest2=456" ]
    |> Query.generate
    |> expectQuery "?etest1=123&etest2=456"

    [ Select "Phone"; External "etest1=123" ]
    |> Query.generate
    |> expectQuery "?etest1=123&$select=Phone"

    [ Count; External "a=1"; External "b=2"; Take 1 ]
    |> Query.generate
    |> expectQuery "?$count=true&a=1&b=2&$top=1"


[<Fact>]
let ``id and compute should work`` () =
    [ Id "42"; Select "Phone"; Count ]
    |> Query.generate
    |> expectQuery "(42)?$count=true&$select=Phone"

    // The first id is used as the resource path
    [ Id "1"; Id "2"; Select "Phone" ]
    |> Query.generate
    |> expectQuery "(1)?$select=Phone"

    [ Compute "demo as Demo2"; Select "Demo2" ]
    |> Query.generate
    |> expectQuery "?$compute=demo as Demo2&$select=Demo2"

    [ Compute ""; Select "Demo2" ]
    |> Query.generate
    |> expectQuery "?$select=Demo2"


[<Fact>]
let ``generateFor should merge and override extra queries`` () =
    Query.generateFor<Person> [ Count; Take 5; Skip 10; OrderBy "Name"; Filter "Age gt 10" ]
    |> expectQuery
        "?$count=true&$expand=Contact($select=Phone,Email),Addresses($select=Street,Room)&$orderBy=Name&$select=Name,Age,Contact,Addresses&$skip=10&$top=5&$filter=(Age gt 10)"

    Query.generateFor<Person> [ Select "Name" ]
    |> expectQuery "?$expand=Contact($select=Phone,Email),Addresses($select=Street,Room)&$select=Name"

    Query.generateFor<Person> [ ExpandEx [ "Contact", [ Select "Email" ] ] ]
    |> expectQuery
        "?$expand=Contact($select=Email),Addresses($select=Street,Room)&$select=Name,Age,Contact,Addresses"


[<Fact>]
let ``combineQuery should use the given separator`` () =
    Query.combineQuery ";" [ Select "Id"; Count; Filter "Id gt 1" ]
    |> expectQuery "$count=true;$select=Id;$filter=(Id gt 1)"
