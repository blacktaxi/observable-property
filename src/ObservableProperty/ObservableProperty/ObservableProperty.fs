namespace System.Reactive.Properties

open System
open System.Reactive
open System.Reactive.Concurrency
open System.Reactive.Disposables
open System.Reactive.Subjects
open System.Reactive.Linq

type IReadableProperty<'a> =
    abstract Value : 'a
    abstract Observe : IObservable<'a>

type IWriteableProperty<'a> =
    abstract Set : 'a -> unit
    abstract Provide : IObserver<'a>

type ObservableProperty<'a>(?initialValue) =
    let subject = new Subject<_>()
    let observable = subject :> IObservable<_>

    [<VolatileField>]
    let mutable currentValue : 'a = defaultArg initialValue Unchecked.defaultof<_>

    let updateCurrentValue =
        subject.ObserveOn(Scheduler.Immediate).Subscribe(fun x -> currentValue <- x)

    let toDispose =
        new CompositeDisposable(subject :> IDisposable, updateCurrentValue)

    let dispose disposing =
        if disposing then
            subject.OnCompleted()
            toDispose.Dispose()

    interface IReadableProperty<'a> with
        member this.Value = currentValue
        member this.Observe = observable

    interface IWriteableProperty<'a> with
        member this.Set(x) = subject.OnNext(x)
        member this.Provide = subject :> _

    interface IDisposable with
        member this.Dispose() = dispose true

type ObservablePropertyFromObservable<'a>(source : IObservable<'a>) =
    let inner = new ObservableProperty<'a>()

    let subscription =
        source.ObserveOn(Scheduler.Immediate).Subscribe((inner :> IWriteableProperty<_>).Provide)

    let dispose disposing =
        if disposing then
            subscription.Dispose()
            (inner :> IDisposable).Dispose()

    interface IReadableProperty<'a> with
        member this.Value = (inner :> IReadableProperty<_>).Value
        member this.Observe = (inner :> IReadableProperty<_>).Observe

    interface IDisposable with
        member this.Dispose() = dispose true

module Operators =
    let inline (<<-) (p : IWriteableProperty<'a>) (x : 'a) = p.Set(x)
    let inline (!!) (p : IReadableProperty<'a>) : 'a = p.Value

[<AutoOpen>]
module Extensions =
    type IReadableProperty<'a> with
        member property.ObserveChanges = property.Observe.DistinctUntilChanged()
        member property.Behavior =
            property.Observe
                .Multicast(
                    (fun () -> new BehaviorSubject<_>(property.Value) :> _),
                    fun x -> x)
                .DistinctUntilChanged()

    type IObservable<'a> with
        member observable.AsProperty () = new ObservablePropertyFromObservable<'a>(observable)

    type IObserver<'a> with
        member observer.AsProperty () =
            { new IWriteableProperty<'a> with
                member this.Set(x) = observer.OnNext(x)
                member this.Provide = observer }
