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

module ObservableProperty =
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

    /// Create an observable property.
    let inline create initialValue = new ObservableProperty<_>(initialValue)

    /// Observe distinct changes of a property.
    let inline changes (p : IOutObservableProperty<_>) = p.WhenValueSet.DistinctUntilChanged()

    /// Observe property behavior: current value followed by distinct changes.
    let inline behavior (p : IOutObservableProperty<_>) =
        p.WhenValueSet
            .StartWith(p.Value)
            .DistinctUntilChanged()

    /// Propagate values from an IOutObservableProperty to an IInObservableProperty, using a mapping function.
    let inline bind (from' : IOutObservableProperty<_>) (mapTo : _ -> _) (to' : IInObservableProperty<_>) : IDisposable =
        (behavior from')
            .ObserveOn(Scheduler.Immediate) // is this really the way to go?
            .Select(mapTo)
            .Subscribe(to'.Sink)

    /// Synchronize two observable properties. Value changes will propagate both ways through given mapping functions.
    let inline sync a (mapTo : 'a -> 'b) b (mapFrom : 'b -> 'a) : IDisposable =
        // it just magically works!
        new CompositeDisposable(bind a mapTo b, bind b mapFrom a) :> _

    /// Create an observable property from an IObservable and an initial value.
    let ofObservableInit initialValue o : IOutObservableProperty<_> =
        new ObservablePropertyFromObservable<'a>(o, initialValue) :> _

    /// Create an observable property from an IObservable.
    let ofObservable (o : IObservable<'a>) : IOutObservableProperty<'a option> =
        new ObservablePropertyFromObservable<'a option>(o.Select(Some), None) :> _

    /// Create an observable property from an IObservable<'a option>.
    let ofObservableOption (o : IObservable<'a option>) : IOutObservableProperty<'a option> =
        new ObservablePropertyFromObservable<'a option>(o, None) :> _

    /// Create an observable property from an IObserver.
    let inline ofObserver (o : IObserver<_>) : IInObservableProperty<_> =
        { new IInObservableProperty<'a> with
            member this.Set(x) = o.OnNext(x)
            member this.Sink = o }

    /// Get the current value of an observable property.
    let inline getValue (p : #IOutObservableProperty<_>) = p.Value

    /// Set the current value of an observable property.
    let inline setValue v (p : #IInObservableProperty<_>) = p.Set(v)

module Operators =
    /// Create a new observable property with a given initial value.
    let inline newOP v = ObservableProperty.create v

    /// Set the current value of an observable property.
    let inline (<~) (p : IInObservableProperty<'a>) (x : 'a) = ObservableProperty.setValue x p

    /// Get the current value of an observable property.
    let inline (!!) (p : IOutObservableProperty<'a>) : 'a = ObservableProperty.getValue p

    /// Propagate values from left to right, using a mapping function.
    let inline ( @~> ) (from', mapTo) to' = ObservableProperty.bind from' mapTo to'

    /// Propagate values from right to left, using a mapping function.
    let inline ( <~@ ) to' (from', mapTo) = ObservableProperty.bind from' mapTo to'

    /// Synchronize two observable properties. Value changes will propagate both ways through given mapping functions.
    let inline (<~@~>) (a, mapTo) (b, mapFrom) = ObservableProperty.sync a mapTo b mapFrom

[<Extension>]
type ObservablePropertyEx =
    [<Extension>]
    /// Create an observable property from an IObservable and an initial value.
    static member AsProperty(o : IObservable<'a>, initialValue : 'a) =
        ObservableProperty.ofObservableInit initialValue o

    [<Extension>]
    /// Create an observable property from an IObservable<'a option>.
    static member AsProperty(o : IObservable<'a option>) = ObservableProperty.ofObservableOption o

    [<Extension>]
    /// Create an observable property from an IObservable.
    static member AsProperty(o : IObservable<'a>) = ObservableProperty.ofObservable o

    [<Extension>]
    /// Create an observable property from an IObserver.
    static member AsProperty(o : IObserver<'a>) = ObservableProperty.ofObserver o

    [<Extension>]
    /// Observe distinct changes of a property.
    static member ObserveWhenValueChanges(p : IOutObservableProperty<'a>) =
        ObservableProperty.changes p

    [<Extension>]
    /// Observe property behavior: current value followed by distinct changes.
    static member ObserveBehavior(p : IOutObservableProperty<'a>) = ObservableProperty.behavior p

    [<Extension>]
    /// Propagate values from an IOutObservableProperty to an IInObservableProperty, using a mapping function.
    static member Bind(property, target, selector) =
        ObservableProperty.bind property selector target

    [<Extension>]
    /// Synchronize two observable properties. Value changes will propagate both ways through given mapping functions.
    static member Sync(property, counterpart, convert, convertBack) =
        ObservableProperty.sync property convert counterpart convertBack

[<AutoOpen>]
module ObservablePropertyExFSharp =
    type IOutObservableProperty<'a> with
        member property.WhenValueChanges = ObservableProperty.changes property
        member property.Behavior = ObservableProperty.behavior property
