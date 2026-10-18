# Omasharp.Analyzers

Compile-time checks for omasharp apps: they catch, in the editor and the build, the mistakes that otherwise only show up as a wrong or empty value in QML. Every app made with `dotnet new omarchy-app` references this package through `bridge/Omasharp.targets`. It runs in the compiler only; nothing of it ships in the app.

The checks apply to the backend: the class QML sees as `backend` (it derives from `Omasharp.BackendBase`, or it's the app's `BackendType`). In omasharp apps warnings fail the build, so every rule is written to fire only when it's sure.

| Rule | What's wrong | Fix |
|---|---|---|
| [OMASHARP001](#omasharp001) | a name QML can't take | rename it |
| [OMASHARP002](#omasharp002) | two members with the same QML name | rename one |
| [OMASHARP003](#omasharp003) | a public member QML can't see | make it a visible instance property/method, or non-public |
| [OMASHARP004](#omasharp004) | C# changes a property whose setter never tells QML | `Set(ref _field, value)` in the setter |
| [OMASHARP005](#omasharp005) | a computed property QML is never told about | `Notify(nameof(It))` where its inputs change |
| [OMASHARP006](#omasharp006) | `Notify` names no public property | `nameof` the public property |
| [OMASHARP007](#omasharp007) | QML can't call or set this | use a type QML can send; no ref/out/params/overloads |
| [OMASHARP008](#omasharp008) | QML can't use this value | return `Task`/`Task<T>`, a record, a `List<T>` |
| [OMASHARP009](#omasharp009) | `async void` | return `Task` |
| [OMASHARP010](#omasharp010) | `ConfigureAwait(false)` | remove it |

It also turns off .NET's CA1822 ("can be static") on the backend's public members: QML only sees instance members, so that advice would hide them.

## OMASHARP001

A member's QML name (first letter lowercased) is reserved in QML: a JavaScript keyword (`delete`, `new`, `default`...), a member the bridge itself puts on `backend` (`ready`, `running`, `restart`, `lastError`...), one of `QQmlPropertyMap`'s (`keys`, `objectName`...), or it starts with `_`. Rename it: `Delete` → `Remove`, `Running` → `IsRunning`. (Without the analyzers, the bridge still refuses the name when the app starts.)

## OMASHARP002

Two public members become the same name in QML (`Total` and `total`, or a property and a method). QML reaches only one of them. Rename one.

## OMASHARP003

QML sees the backend's public instance properties (with a public getter) and public instance methods. Anything else that's public is invisible to it: static members and constants, fields, C# `event`s, indexers, generic methods, properties whose getter isn't public. Make it a public instance property or method if QML needs it (`public int Max => MaxValue;`; for events, call `Raise("name", value)` and handle `backend.raised` in QML), or make it non-public (`internal` is enough for tests) if it's only for C#.

## OMASHARP004

The backend changes the property after startup (outside its constructor), but the property's setter never tells QML, so QML keeps showing the old value. Store it with `Set`:

```cs
string _status = "";
public string Status { get => _status; private set => Set(ref _status, value); }
```

or call `Notify(nameof(Status))` after changing it. Properties only QML writes, and ones set only in the constructor, are fine as `{ get; set; }`.

## OMASHARP005

A get-only property is computed from state the backend changes after startup (a field it assigns or a list it adds to, a property with a setter), but nothing in the class ever calls `Notify(nameof(ThatProperty))`, so QML keeps showing its first value. Call `Notify(nameof(ThatProperty))` where the inputs change, usually next to the `Set` of the input:

```cs
public int Count { get => _count; set { if (Set(ref _count, value)) Notify(nameof(Label)); } }
public string Label => $"Counted {Count}";
```

## OMASHARP006

`Notify("Name")` (or `Set(..., "Name")`, `OnPropertyChanged("Name")`) with a name that isn't a public property of the backend: nothing updates in QML. Pass `nameof(...)` of the public property that changed.

## OMASHARP007

QML can't call this method or set this property:
- a parameter or a settable property of a type QML can't send. QML sends `string`, `bool`, the number types, enums, `string[]`, `double[]`, `int[]` and `object` (a parsed JSON value). For anything else, pass an id or index and look the object up in C#; for a property QML only reads, make the setter private;
- a `ref`, `out` or `in` parameter (return the value instead), or `params` (QML passes one array; drop `params`);
- an overloaded method: QML picks an overload by argument count only. Give each one its own name.

Optional parameters of other types (`CancellationToken ct = default`) are fine: QML leaves them out.

## OMASHARP008

QML can't use what this property or method gives it: `ValueTask`/`ValueTask<T>` (the bridge awaits `Task` and `Task<T>`; return those and QML gets a Promise), `IAsyncEnumerable<T>` (return a `List<T>`, or add to a list property and `Notify`), or a tuple (QML would get a string; return a record and QML gets an object).

## OMASHARP009

An `async void` method in the backend: its exceptions end the app, and QML can't wait for it. Return `Task` (QML gets a Promise).

## OMASHARP010

`ConfigureAwait(false)` in the backend moves the rest of the method off the UI thread, where it races QML for the backend's state. Remove it: awaits in the backend come back to the UI thread.

## Turning a rule off for one member

When a rule is wrong for one member, say why, right there:

```cs
[System.Diagnostics.CodeAnalysis.SuppressMessage("Omasharp", "OMASHARP003", Justification = "only the tests call it")]
public static int Parse(string s) => int.Parse(s);
```

or wrap the lines in `#pragma warning disable OMASHARP003` / `#pragma warning restore OMASHARP003`. For a whole app, `dotnet_diagnostic.OMASHARP003.severity = none` in `.editorconfig`, with the reason in a comment.
