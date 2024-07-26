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

    odataDefaultQuery<Contact> () |> expectQuery "$select=Phone,Email"

    odataQuery<Contact> { filter "test" } |> expectQuery "$select=Phone,Email&$filter=(test)"

    odataQuery<Contact> { count } |> expectQuery "$select=Phone,Email&$count=true"

    odataQuery<Contact> { keyValue "myQuery" "myValue" } |> expectQuery "$select=Phone,Email&myQuery=myValue"


    odataDefaultQuery<Person> ()
    |> expectQuery "$select=Name,Age,Contact,Addresses&$expand=Contact($select=Phone,Email),Addresses($select=Street,Room)"

    odataQuery<Person> {
        filter (
            filterAnd {
                gt (fun x -> x.Age) 10
                lt (fun x -> x.Age) 20
            }
        )
    }
    |> expectQuery
        "$select=Name,Age,Contact,Addresses&$expand=Contact($select=Phone,Email),Addresses($select=Street,Room)&$filter=(Age gt 10 and Age lt 20)"

    odataQuery<Person> { 
        expandList (fun x -> x.Addresses) (
            odata { 
                filterAnd { contains (fun x -> x.Street) "test" }
            }
        ) 
    }
    |> expectQuery
        "$select=Name,Age,Contact,Addresses&$expand=Addresses($select=Street,Room;$filter=(contains(Street, 'test'))),Contact($select=Phone,Email)"

    odataQuery<Person> {
        disableAutoExpand
        expandPoco (fun x -> x.Contact)
    }
    |> expectQuery "$select=Name,Age,Contact,Addresses&$expand=Contact($select=Phone,Email)"


    odataQuery<Person> {
        count
        skip 5
        take 10
        orderBy (fun x -> x.Name)
        expandPoco (fun x -> x.Contact)
        expandList (fun x -> x.Addresses)
        expandList (fun x -> x.Addresses) (odata { count })
        filterAnd {
            "custom-filter"
            gt (fun x -> x.Age) 10
            lt (fun x -> x.Age) 20
            filterOr {
                contains (fun x -> x.Name) "test1"
                "custom1"
                None
                Some "custom2"
                custom (fun x -> x.Age) (sprintf "custom3(%s)")
                custom (fun x -> x.Age) (sprintf "custom4(%s)" >> Some)
                custom (fun x -> x.Age) (fun _ -> None)
                filterAnd {
                    gt (fun x -> x.Age) 10
                    lt (fun x -> x.Age) 20
                }
            }
        }
    }
    |> expectQuery
        "$select=Name,Age,Contact,Addresses&$count=true&$skip=5&$top=10&$orderby=Name&$expand=Contact($select=Phone,Email),Addresses($select=Street,Room;$count=true)&$filter=((custom-filter) and Age gt 10 and Age lt 20 and (contains(Name, 'test1') or (custom1) or (custom2) or (custom3(Age)) or (custom4(Age)) or (Age gt 10 and Age lt 20)))"


    odataQuery<{| Id: int
                  Name: string
                  Test1: {| Id: Guid; Name: string; Contact: Contact |}
                  Test2: {| Id: Guid; Name: string |} option
                  Test3: {| Id: int |} []
                  Test4: {| Id: int |} list |}> {
        empty
    }
    |> expectQuery
        "$select=Id,Name,Test1,Test2,Test3,Test4&$expand=Test1($select=Contact,Id,Name;$expand=Contact($select=Phone,Email)),Test2($select=Id,Name),Test3($select=Id),Test4($select=Id)"


[<Fact>]
let ``Test query override generation`` () =
    odataQuery<Contact> {
        count
        take 5
        odata { take 10 }
    }
    |> expectQuery "$select=Phone,Email&$count=true&$top=10"

    odataQuery<Contact> {
        count
        odata { take 10 }
        take 5
    }
    |> expectQuery "$select=Phone,Email&$count=true&$top=5"


[<Fact>]
let ``Test yield filter directly generation`` () =
    odataQuery<Contact> {
        filterOr {
            contains (fun x -> x.Phone) "123"
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(contains(Phone, '123'))"

    odataQuery<Contact> {
        filterOr {
            contains (fun x -> x.Phone) "123"
        }
        count
    }
    |> expectQuery "$select=Phone,Email&$count=true&$filter=(contains(Phone, '123'))"

    odataQuery<Contact> {
        count
        filterOr {
            contains (fun x -> x.Phone) "123"
        }
    }
    |> expectQuery "$select=Phone,Email&$count=true&$filter=(contains(Phone, '123'))"

    odataQuery<Contact> {
        count
        filterOr {
            contains (fun x -> x.Phone) "123"
        }
        take 10
        filterOr {
            contains (fun x -> x.Email) "456"
        }
    }
    |> expectQuery "$select=Phone,Email&$count=true&$top=10&$filter=(contains(Phone, '123')) and (contains(Email, '456'))"

    odataQuery<Contact> {
        filterAnd<{| Address: string option |}> {
            contains (fun x -> x.Address) "123"
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(contains(Address, '123'))"

    odataQuery<Contact> {
        filterAnd<{| Address: int option; Address2: string option |}> {
            eq (fun x -> x.Address) ({| Address = Option<int>.None |}).Address
            lt (fun x -> x.Address) ({| Address = Option<int>.None |}).Address
            gt (fun x -> x.Address) ({| Address = Option<int>.None |}).Address
            gt (fun x -> x.Address) (Option<int>.None)
            contains (fun x -> x.Address2) None
        }
    }
    |> expectQuery "$select=Phone,Email"
    
    odataQuery<Contact> {
        filterAnd<{| Address: int |}> {
            eq (fun x -> x.Address) 1
            lt (fun x -> x.Address) 2
            gt (fun x -> x.Address) 3
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(Address eq 1 and Address lt 2 and Address gt 3)"
    
    odataQuery<Contact> {
        filterAnd<{| Address: int option |}> {
            eq (fun x -> x.Address) 1
            lt (fun x -> x.Address) 2
            gt (fun x -> x.Address) 3
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(Address eq 1 and Address lt 2 and Address gt 3)"

    odataQuery<Contact> {
        filterAnd {
            eq "Address" 1
            lt "Address" 2
            gt "Address" 3
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(Address eq 1 and Address lt 2 and Address gt 3)"

    odataQuery<Contact> {
        filterAnd<{| Room: int option |}> {
            eq (fun x -> x.Room) (Some 1)
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(Room eq 1)"
    
    odataQuery<Contact> {
        filterAnd<{| Address: string option |}> {
            eq (fun x -> x.Address) "#$^&12"
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(Address eq '%23%24%5e%2612')"

    odataQuery<Contact> {
        filterAnd<{| Address: string |}> {
            eq (fun x -> x.Address) (Some "123")
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(Address eq '123')"

    odataQuery<Contact> {
        filterAnd<{| Address: string |}> {
            eq (fun x -> x.Address) None
        }
    }
    |> expectQuery "$select=Phone,Email"

    odataQuery<Contact> {
        filterOr {
            "pre"
            for i in 1..3 do
                filterAnd<{| Address: string |}> {
                    eq (fun x -> x.Address) (Some $"a{i}")
                    eq (fun x -> x.Address) (Some $"b{i}")
                }
            "post"
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=((pre) or (Address eq 'a1' and Address eq 'b1') or (Address eq 'a2' and Address eq 'b2') or (Address eq 'a3' and Address eq 'b3') or (post))"

    
type LoopNav = { Id: int; LoopNav: LoopNav option }
type LoopData = { Id: int; LoopNav: LoopNav option }

[<Fact>]
let ``Test loop reference expand`` () =
    odataQuery<LoopData> { 
        expandPoco (fun x -> x.LoopNav) (
            odata<LoopNav> { 
                expandPoco (fun x -> x.LoopNav)
            }
        )
    }
    |> expectQuery "$select=Id,LoopNav&$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav))"
    
    odataQuery<LoopData> { empty }
    |> expectQuery "$select=Id,LoopNav&$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav)))"
    
    odataQuery<LoopData> { maxLoopDeepth 2 }
    |> expectQuery "$select=Id,LoopNav&$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav))))"

    odataQuery<LoopData> {
        expandPoco (fun x -> x.LoopNav) (
            odata<LoopNav> { 
                maxLoopDeepth 2
            }
        )
    }
    |> expectQuery "$select=Id,LoopNav&$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav;$expand=LoopNav($select=Id,LoopNav))))"


[<Fact>]
let ``orderBy with multiple fields`` () =
    odataQuery<Contact> {
        orderBy (fun x -> x.Phone)
        orderBy (fun x -> x.Email)
    }
    |> expectQuery "$select=Phone,Email&$orderby=Phone,Email"

    odataQuery<Contact> {
        orderByDesc (fun x -> x.Phone)
        orderByDesc (fun x -> x.Email)
    }
    |> expectQuery "$select=Phone,Email&$orderby=Phone desc,Email desc"

    odataQuery<Contact> {
        orderBy (fun x -> x.Phone)
        orderByDesc (fun x -> x.Email)
    }
    |> expectQuery "$select=Phone,Email&$orderby=Phone,Email desc"
    

[<Fact>]
let ``eq and ne should work`` () =
    odataQuery<Contact> {
        filterAnd {
            ne "Phone" "123#"
            ne (fun x -> x.Email) "456#"
            contains (fun x -> x.Email) "#"
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=(Phone ne '123%23' and Email ne '456%23' and contains(Email, '%23'))"
  
    odataQuery<{| IsReadOnly: bool |}> {
        filterAnd<{| IsReadOnly: bool; Name: string |}> {
            eq (fun x -> x.IsReadOnly) true
            ne (fun x -> x.IsReadOnly) false
            ne (fun x -> x.Name) (Some null)
        }
    }
    |> expectQuery "$select=IsReadOnly&$filter=(IsReadOnly eq true and IsReadOnly ne false and Name ne null)"

  
[<Fact>]
let ``yield list of string should work for filter`` () =
    odataQuery<Contact> {
        filterAnd {
            [
                Filter.contains "Phone" "123"
                Filter.eq "Email" "456"
            ]
            filterOr {
                [
                    "test1"
                    Filter.ne "Email" "test2"
                ]
                [] // should do nothing
            }
        }
    }
    |> expectQuery "$select=Phone,Email&$filter=((contains(Phone, '123')) and (Email eq 456) and ((test1) or (Email ne test2)))"


[<Fact>]
let ``empty filter should work`` () =
    odataQuery<Contact> {
        filterAnd {
            [
            ]
            ""
            Some ""
            filterOr {
                []
                ""
                Some ""
            }
            andQueries []
            andQueries [""]
            orQueries []
            orQueries [""]
        }
    }
    |> expectQuery "$select=Phone,Email"


[<Fact>]
let ``filterNot should work`` () =
    filterAndQuery {
        filterNot {
            filterAnd {
                "demo"
                filterNot {
                    "lol"
                }
            }
        }
        eq "foo" 1
    }
    |> expectQuery "(not((demo) and (not(lol)))) and foo eq 1"



[<Fact>]
let ``exclude should work`` () =
    odataQuery<Person> {
        excludeSelect (fun x -> x.Age)
        if true then odata<Person> { excludeSelect (fun x -> x.Addresses) }
    }
    |> expectQuery "$select=Name,Contact&$expand=Contact($select=Phone,Email)"

    odataQuery<Person> {
        excludeSelect (fun x -> x.Age)
        excludeSelect (fun x -> x.Contact)
        excludeSelect (fun x -> x.Addresses)
    }
    |> expectQuery "$select=Name"

    odataQuery<Person> {
        excludeSelect ["Contact"; "Addresses"]
    }
    |> expectQuery "$select=Name,Age"
