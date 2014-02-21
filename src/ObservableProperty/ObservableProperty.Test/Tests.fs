namespace System.Reactive.Properties.Test

open Xunit

open System
open System.Reactive
open System.Reactive.Linq

open System.Reactive.Properties
open System.Reactive.Properties.Operators
open System.Reactive.Properties.Extensions

type Tests () =
    [<Fact>]
    let ``a property should correctly return the stored value`` () =
        let p = new ObservableProperty<_>()

        p <<- 5
        Assert.True(!!p = 5)

        p <<- 6
        Assert.True(!!p = 6)

    [<Fact>]
    let ``property changes should be observed correctly`` () =
        let p = new ObservableProperty<_>()
        let o = p.ObserveChanges.Replay()
        o.Connect() |> ignore

        p <<- 1
        p <<- 2
        p <<- 3

        (p :> IDisposable).Dispose()

        Assert.Equal<int[]>([| 1; 2; 3 |], o.ToArray().First())