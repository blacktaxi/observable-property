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

[<Interface>]
type IReadWriteProperty<'a> =
    inherit IReadableProperty<'a>
    inherit IWriteableProperty<'a>

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

    interface IReadWriteProperty<'a> with
        member this.Value = currentValue
        member this.Observe = observable

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

module ObservableProperty =
    let inline changes (p : IReadableProperty<_>) = p.Observe.DistinctUntilChanged()
    let inline behavior (p : IReadableProperty<_>) =
        p.Observe
            .Multicast(
                (fun () -> new BehaviorSubject<_>(p.Value) :> _),
                fun x -> x)
            .DistinctUntilChanged()

    let inline bind (from' : IReadableProperty<_>) (mapTo : _ -> _) (to' : IWriteableProperty<_>) : IDisposable =
        (behavior from')
            .ObserveOn(Scheduler.Immediate)
            .Select(mapTo)
            .Subscribe(to'.Provide)

    let inline sync a (mapTo : 'a -> 'b) b (mapFrom : 'b -> 'a) : IDisposable =
        // it just magically works!
        new CompositeDisposable(bind a mapTo b, bind b mapFrom a) :> _

module Operators =
    let inline (<<-) (p : IWriteableProperty<'a>) (x : 'a) = p.Set(x)
    let inline (!!) (p : IReadableProperty<'a>) : 'a = p.Value
    let inline (|->>) (from', mapTo) to' = ObservableProperty.bind from' mapTo to'
    let inline (<<-|) to' (from', mapTo) = ObservableProperty.bind from' mapTo to'
    let inline (<<-|->>) (a, mapTo) (b, mapFrom) = ObservableProperty.sync a mapTo b mapFrom

[<AutoOpen>]
module Extensions =
    type IReadableProperty<'a> with
        member property.ObserveChanges = ObservableProperty.changes property
        member property.Behavior = ObservableProperty.behavior property
        member property.Bind(target, selector) = ObservableProperty.bind property selector target

    type IReadWriteProperty<'a> with
        member property.Sync(counterpart, convert, convertBack) =
            ObservableProperty.sync property convert counterpart convertBack

    type IObservable<'a> with
        member observable.AsProperty () = new ObservablePropertyFromObservable<'a>(observable)

    type IObserver<'a> with
        member observer.AsProperty () =
            { new IWriteableProperty<'a> with
                member this.Set(x) = observer.OnNext(x)
                member this.Provide = observer }
