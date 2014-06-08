namespace System.Reactive.Properties.Test

open Xunit

open System
open System.Reactive
open System.Reactive.Subjects
open System.Reactive.Linq

open System.Reactive.Properties
open System.Reactive.Properties.Operators

type Tests () =
    let record (o : IObservable<_>) =
        let x = o.Replay()
        let y = x.ToArray()
        x.Connect() |> ignore
        y

    [<Fact>]
    let ``a property should correctly return the stored value`` () =
        let p = oref 0

        p <~ 5
        Assert.True(!!p = 5)

        p <~ 6
        Assert.True(!!p = 6)

    [<Fact>]
    let ``property changes should be observed correctly`` () =
        let p = oref 0
        let vs = record (p :> IReadableProperty<_>).WhenValueSet

        p <~ 1
        p <~ 2
        p <~ 3

        (p :> IDisposable).Dispose()

        Assert.Equal<int[]>([| 1; 2; 3 |], vs.First())

    [<Fact>]
    let ``property behavior should yield the current value first`` () =
        let p = oref 0
        p <~ 5

        let o = record p.Behavior

        p <~ 6
        (p :> IDisposable).Dispose()

        Assert.Equal<int[]>([| 5; 6 |], o.First())

    [<Fact>]
    let ``property behavior should only yield value changes`` () =
        let p = oref 0
        p <~ 5

        let o = record p.Behavior

        p <~ 6
        p <~ 6
        p <~ 7
        p <~ 7
        p <~ 6
        (p :> IDisposable).Dispose()

        Assert.Equal<int[]>([| 5; 6; 7; 6 |], o.First())

    [<Fact>]
    let ``one-way binding should work`` () =
        let a = oref 0
        let b = oref 0

        let binding = (a, id) @~> b

        a <~ 5
        Assert.True(!!b = 5)

        a <~ 6
        Assert.True(!!b = 6)

        binding.Dispose()

        a <~ 7
        Assert.True(!!b = 6)

    [<Fact>]
    let ``two-way binding should work`` () =
        let a = oref 0
        let b = oref 0

        do
            use binding = (a, id) <~@~> (b, id)

            a <~ 5
            Assert.True(!!b = 5)
            b <~ 6
            Assert.True(!!a = 6)

        a <~ 7
        Assert.True(!!b = 6)

    [<Fact>]
    let ``binding an observable to property should work`` () =
        let a = [1; 2; 3; 4].ToObservable()
        let b = oref 0

        let binding = (a.AsProperty(0), id) @~> b

        Assert.True(!!b = 4)

    [<Fact>]
    let ``converting IObserver to IWriteableProperty works`` () =
        let s = new Subject<_>()
        let o = s.Replay()
        o.Connect() |> ignore

        let p = (s :> IObserver<_>).AsProperty()

        p <~ 1
        p <~ 2
        p <~ 3

        let vals = o.Take(3).ToEnumerable() |> Array.ofSeq

        Assert.True([| 1; 2; 3 |] = vals)
