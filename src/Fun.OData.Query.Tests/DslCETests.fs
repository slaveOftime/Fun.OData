module Fun.OData.Query.Tests.QueryGenerationTests

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


let inline expectQuery x y = Assert.Equal(x, y)


[<Fact>]
let ``Test query generation`` () =

    odataSimple<Contact> () |> expectQuery "$select=Phone,Email"

    odataQuery<Contact> () { filter "test" } |> expectQuery "$select=Phone,Email&$filter=(test)"

    odataQuery<Contact> () { count } |> expectQuery "$select=Phone,Email&$count=true"

    odataQuery<Contact> () { keyValue "myQuery" "myValue" } |> expectQuery "$select=Phone,Email&myQuery=myValue"


    odataSimple<Person> ()
    |> expectQuery "$select=Name,Age,Contact,Addresses&$expand=Contact($select=Phone,Email),Addresses($select=Street,Room)"

    odataQuery<Person> () {
        filter (
            odataAnd () {
                gt (fun x -> x.Age) "10"
                lt (fun x -> x.Age) "20"
            }
        )
    }
    |> expectQuery
        "$select=Name,Age,Contact,Addresses&$expand=Contact($select=Phone,Email),Addresses($select=Street,Room)&$filter=(Age gt 10 and Age lt 20)"

    odataQuery<Person> () { expandList (fun x -> x.Addresses) (odata () { filter (odataAnd () { contains (fun x -> x.Street) "test" }) }) }
    |> expectQuery
        "$select=Name,Age,Contact,Addresses&$expand=Addresses($select=Street,Room&$filter=(contains(Street, 'test'))),Contact($select=Phone,Email)"

    odataQuery<Person> () {
        disableAutoExpand
        expandPoco (fun x -> x.Contact)
    }
    |> expectQuery "$select=Name,Age,Contact,Addresses&$expand=Contact($select=Phone,Email)"


    odataQuery<Person> () {
        count
        skip 5
        take 10
        orderBy (fun x -> x.Name)
        expandPoco (fun x -> x.Contact)
        expandList (fun x -> x.Addresses)
        expandList (fun x -> x.Addresses) (odata () { count })
        filter "custom-filter"
        filter (
            odataAnd () {
                gt (fun x -> x.Age) 10
                lt (fun x -> x.Age) 20
            }
        )
        filter (
            odataOr () {
                contains (fun x -> x.Name) "test1"
                "custom1"
                None
                Some "custom2"
                custom (fun x -> x.Age) (sprintf "custom3(%s)")
                custom (fun x -> x.Age) (sprintf "custom4(%s)" >> Some)
                custom (fun x -> x.Age) (fun _ -> None)
                odataAnd () {
                    gt (fun x -> x.Age) 10
                    lt (fun x -> x.Age) 20
                }
            }
        )
    }
    |> expectQuery
        "$select=Name,Age,Contact,Addresses&$count=true&$skip=5&$top=10&$orderBy=Name&$expand=Contact($select=Phone,Email),Addresses($select=Street,Room&$count=true)&$filter=(custom-filter) and (Age gt 10 and Age lt 20) and (contains(Name, 'test1') or (custom1) or (custom2) or (custom3(Age)) or (custom4(Age)) or (Age gt 10 and Age lt 20))"


    odataQuery<{| Id: int
                  Name: string
                  Test1: {| Id: Guid; Name: string; Contact: Contact |}
                  Test2: {| Id: Guid; Name: string |} option
                  Test3: {| Id: int |}[]
                  Test4: {| Id: int |} list |}>
        () {
        empty
    }
    |> expectQuery
        "$select=Id,Name,Test1,Test2,Test3,Test4&$expand=Test1($select=Contact,Id,Name&$expand=Contact($select=Phone,Email)),Test2($select=Id,Name),Test3($select=Id),Test4($select=Id)"
