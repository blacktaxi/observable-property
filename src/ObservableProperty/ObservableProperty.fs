namespace System.Reactive.Properties

open System
open System.Reactive
open System.Reactive.Concurrency
open System.Reactive.Disposables
open System.Reactive.Subjects
open System.Reactive.Linq

open System.Runtime.CompilerServices

/// A read-only property variable.
type IOutObservableProperty<'a> =
    /// Current value of the property
    abstract Value : 'a
    /// An observable that generates a value when this IReadableProperty<'a> is being set.
    abstract WhenValueSet : IObservable<'a>

/// A write-only property variable.
type IInObservableProperty<'a> =
    /// Set the current value of the property
    abstract Set : 'a -> unit
    /// An observer that will set the property's value in OnNext.
    abstract Sink : IObserver<'a>

type IInOutObservableProperty<'a> =
    inherit IOutObservableProperty<'a>
    inherit IInObservableProperty<'a>

type ObservableProperty<'a>(initialValue) =
    let subject = new BehaviorSubject<_>(initialValue)
    // this is not an optimal implementation: see ObservableProperty.behavior
    let observable = subject.Skip(1) :> IObservable<_>

    let toDispose = subject :> IDisposable

    let dispose disposing =
        if disposing then
            subject.OnCompleted()
            toDispose.Dispose()

    interface IInOutObservableProperty<'a> with
        member this.Value = subject.Value
        member this.WhenValueSet = observable

        member this.Set(x) = subject.OnNext(x)
        member this.Sink = subject :> _

    member this.Dispose() = dispose true
    interface IDisposable with
        member this.Dispose() = this.Dispose()

type ObservablePropertyFromObservable<'a> internal (source : IObservable<'a>, initialValue) =
    let inner = new ObservableProperty<'a>(initialValue)

    let subscription =
        source.ObserveOn(Scheduler.Immediate).Subscribe((inner :> IInObservableProperty<_>).Sink)

    let dispose disposing =
        if disposing then
            subscription.Dispose()
            (inner :> IDisposable).Dispose()

    interface IOutObservableProperty<'a> with
        member this.Value = (inner :> IOutObservableProperty<_>).Value
        member this.WhenValueSet = (inner :> IOutObservableProperty<_>).WhenValueSet

    member this.Dispose() = dispose true
    interface IDisposable with
        member this.Dispose() = this.Dispose()

module ObservableProperty =
    let inline changes (p : IOutObservableProperty<_>) = p.WhenValueSet.DistinctUntilChanged()
    let inline behavior (p : IOutObservableProperty<_>) =
        p.WhenValueSet
            .StartWith(p.Value)
            .DistinctUntilChanged()

    let inline bind (from' : IOutObservableProperty<_>) (mapTo : _ -> _) (to' : IInObservableProperty<_>) : IDisposable =
        (behavior from')
            .ObserveOn(Scheduler.Immediate)
            .Select(mapTo)
            .Subscribe(to'.Sink)

    let inline sync a (mapTo : 'a -> 'b) b (mapFrom : 'b -> 'a) : IDisposable =
        // it just magically works!
        new CompositeDisposable(bind a mapTo b, bind b mapFrom a) :> _

    let ofObservableInit initialValue o : IOutObservableProperty<_> =
        new ObservablePropertyFromObservable<'a>(o, initialValue) :> _

    let ofObservable (o : IObservable<'a>) : IOutObservableProperty<'a option> =
        new ObservablePropertyFromObservable<'a option>(o.Select(Some), None) :> _

    let ofObservableOption (o : IObservable<'a option>) : IOutObservableProperty<'a option> =
        new ObservablePropertyFromObservable<'a option>(o, None) :> _

    let ofObserver (o : IObserver<_>) : IInObservableProperty<_> =
        { new IInObservableProperty<'a> with
            member this.Set(x) = o.OnNext(x)
            member this.Sink = o }

module Operators =
    /// Creates a new ObservableProperty with a given initial value.
    let inline newOP v = new ObservableProperty<_>(v)

    /// Assigns a new value to an IWriteableProperty.
    let inline (<~) (p : IInObservableProperty<'a>) (x : 'a) = p.Set(x)

    /// Gets current value of an IReadableProperty.
    let inline (!!) (p : IOutObservableProperty<'a>) : 'a = p.Value

    /// 'Binds' an IReadableProperty (left operand) to an IWriteableProperty (right operand),
    /// projecting the values with given map function.
    let inline ( @~> ) (from', mapTo) to' = ObservableProperty.bind from' mapTo to'

    /// 'Binds' an IReadableProperty (left operand) to an IWriteableProperty (right operand),
    /// projecting the values with given map function.
    let inline ( <~@ ) to' (from', mapTo) = ObservableProperty.bind from' mapTo to'

    let inline (<~@~>) (a, mapTo) (b, mapFrom) = ObservableProperty.sync a mapTo b mapFrom

[<Extension>]
type ObservablePropertyEx =
    [<Extension>]
    static member AsProperty(o : IObservable<'a>, initialValue : 'a) =
        ObservableProperty.ofObservableInit initialValue o

    [<Extension>]
    static member AsProperty(o : IObservable<'a option>) = ObservableProperty.ofObservableOption o

    [<Extension>]
    static member AsProperty(o : IObservable<'a>) = ObservableProperty.ofObservable o

    [<Extension>]
    static member AsProperty(o : IObserver<'a>) = ObservableProperty.ofObserver o

    [<Extension>]
    static member ObserveWhenValueChanges(p : IOutObservableProperty<'a>) =
        ObservableProperty.changes p

    [<Extension>]
    static member ObserveBehavior(p : IOutObservableProperty<'a>) = ObservableProperty.behavior p

    [<Extension>]
    static member Bind(property, target, selector) =
        ObservableProperty.bind property selector target

    [<Extension>]
    static member Sync(property, counterpart, convert, convertBack) =
        ObservableProperty.sync property convert counterpart convertBack

[<AutoOpen>]
module ObservablePropertyExFSharp =
    type IOutObservableProperty<'a> with
        member property.WhenValueChanges = ObservableProperty.changes property
        member property.Behavior = ObservableProperty.behavior property
