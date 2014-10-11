observable-property
===================

[![Build status](https://ci.appveyor.com/api/projects/status/b5egov8pl2202oi1)](https://ci.appveyor.com/project/blacktaxi/observable-property)
[![Build Status](https://api.travis-ci.org/blacktaxi/observable-property.svg?branch=master)](https://travis-ci.org/blacktaxi/observable-property)

An [Rx.NET](https://github.com/Reactive-Extensions/Rx.NET)-based data binding with first-class bindable properties. 

Example
=======

```fsharp
open System.Reactive.Property
open System.Reactive.Property.Operators

type FooBar() =
	// create an observable property
	member val Fred = newP 0 with get

let foo = FooBar()

// observe changes
use _ = foo.Fred.WhenValueSet.Subscribe(fun x -> printfn "value changed: %A" x)

// assign new value
foo.Fred <~ 5		//> value changed: 5

// access current value
printfn "%d" <| 5 + !!foo.Fred
```
