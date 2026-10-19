# Omasharp.Analyzers

Build-time checks for Omasharp apps. They catch the mistakes that would otherwise show up as a wrong or empty value in QML, and they flag them in the editor too.

Every app made with `dotnet new omarchy-app` references this package through `bridge/Omasharp.targets`. It runs inside the compiler only. Nothing from it ships in the app.

The rules look at the backend, the class QML sees as `backend`. That's the class deriving from `Omasharp.BackendBase`, or the app's `BackendType`. Warnings fail the build in Omasharp apps, so a rule only fires when it's sure.

| Rule | Problem | Fix |
|---|---|---|
| [OMASHARP001](#omasharp001) | A name QML can't take | Rename it |
| [OMASHARP002](#omasharp002) | Two members with the same QML name | Rename one |
| [OMASHARP003](#omasharp003) | A public member QML can't see | Make it a public instance property or method, or make it non-public |
| [OMASHARP004](#omasharp004) | C# changes a property whose setter never tells QML | Use `Set(ref _field, value)` in the setter |
| [OMASHARP005](#omasharp005) | QML never hears about a computed property | Call `Notify(nameof(It))` where its inputs change |
| [OMASHARP006](#omasharp006) | `Notify` names no public property | Pass `nameof` of the public property |
| [OMASHARP007](#omasharp007) | QML can't call or set this | Use a type QML can send. No ref, out, params or overloads |
| [OMASHARP008](#omasharp008) | QML can't use this value | Return `Task`/`Task<T>`, a record or a `List<T>` |
| [OMASHARP009](#omasharp009) | `async void` | Return `Task` |
| [OMASHARP010](#omasharp010) | `ConfigureAwait(false)` | Remove it |

The package also turns off .NET's CA1822 ("can be static") for the backend's public members. QML only sees instance members, so following that advice would hide them.

## OMASHARP001

A member's QML name is its C# name with the first letter lowercased. These names are reserved in QML:

- JavaScript keywords, like `delete`, `new` and `default`.
- Members the bridge itself puts on `backend`, like `ready`, `running`, `restart` and `lastError`.
- `QQmlPropertyMap` members, like `keys` and `objectName`.
- Any name starting with `_`.

Rename the member. `Delete` becomes `Remove`, `Running` becomes `IsRunning`. Without the analyzers, the bridge still refuses the name when the app starts.

## OMASHARP002

Two public members get the same name in QML, like `Total` and `total`, or a property and a method. QML reaches only one of them. Rename one.

## OMASHARP003

QML sees the backend's public instance properties that have a public getter, and its public instance methods. It can't see any other public member:

- static members and constants
- fields
- C# `event`s
- indexers
- generic methods
- properties whose getter isn't public

If QML needs it, make it a public instance property or method, like `public int Max => MaxValue;`. For an event, call `Raise("name", value)` in C# and handle `backend.raised` in QML. If only C# needs it, make it non-public. `internal` is enough for tests.

## OMASHARP004

The backend changes this property after startup, outside its constructor. Its setter never tells QML, so QML keeps showing the old value. Store the value with `Set`:

```cs
string _status = "";
public string Status { get => _status; private set => Set(ref _status, value); }
```

Calling `Notify(nameof(Status))` after the change also works. A plain `{ get; set; }` is fine for properties that only QML writes, and for ones the constructor sets once.

## OMASHARP005

A get-only property is computed from state the backend changes after startup. That state can be a field it assigns, a list it adds to, or a property with a setter. Nothing in the class ever calls `Notify(nameof(ThatProperty))`, so QML keeps showing the first value. Call it where the inputs change, usually next to the `Set` of the input:

```cs
public int Count { get => _count; set { if (Set(ref _count, value)) Notify(nameof(Label)); } }
public string Label => $"Counted {Count}";
```

## OMASHARP006

`Notify("Name")`, `Set(..., "Name")` or `OnPropertyChanged("Name")` names something that isn't a public property of the backend, so nothing updates in QML. Pass `nameof(...)` of the public property that changed.

## OMASHARP007

QML can't call this method or set this property. One of these is the reason:

- A parameter, or a settable property, has a type QML can't send. QML sends `string`, `bool`, the number types, enums, `string[]`, `double[]`, `int[]` and `object` (a parsed JSON value). For anything else, pass an id or an index and look the object up in C#. If QML only reads a property, make its setter private.
- A parameter is `ref`, `out` or `in`. Return the value instead.
- A parameter is `params`. QML passes one array, so drop `params`.
- The method is overloaded. QML picks an overload by argument count only, so give each one its own name.

Optional parameters of other types, like `CancellationToken ct = default`, are fine. QML leaves them out.

## OMASHARP008

QML can't use what this property or method returns:

- `ValueTask` or `ValueTask<T>`. The bridge awaits `Task` and `Task<T>`, and QML gets a Promise for those. Return them instead.
- `IAsyncEnumerable<T>`. Return a `List<T>`, or add to a list property and `Notify`.
- A tuple. QML would get a string. Return a record and QML gets an object.

## OMASHARP009

The backend has an `async void` method. Its exceptions end the app, and QML can't wait for it. Return `Task` and QML gets a Promise.

## OMASHARP010

`ConfigureAwait(false)` in the backend moves the rest of the method off the UI thread, where it races QML for the backend's state. Remove it. Awaits in the backend come back to the UI thread on their own.

## Turning a rule off for one member

When a rule is wrong for one member, say why right there:

```cs
[System.Diagnostics.CodeAnalysis.SuppressMessage("Omasharp", "OMASHARP003", Justification = "only the tests call it")]
public static int Parse(string s) => int.Parse(s);
```

Wrapping the lines in `#pragma warning disable OMASHARP003` and `#pragma warning restore OMASHARP003` also works. To turn a rule off for a whole app, add `dotnet_diagnostic.OMASHARP003.severity = none` to `.editorconfig`, with the reason in a comment.
