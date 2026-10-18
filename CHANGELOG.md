# Changelog

One section per `bridge/VERSION`. Apps update with `dotnet build -t:UpdateBridge`; entries marked **ABI** change the C ABI (`bridge/host/omasharp.h` ⟷ `bridge/cs/Native.cs`), so an app that customized either file must merge both before it runs again.

## 0.1.1

The omasharp logo: the left half of Omarchy's logo as a C, and a sharp on the same grid. It's the icon of both packages and heads the README (`assets/`). Nothing changed for apps.

## 0.1.0

First release (after 0.0.1-rc.1, a test release): the bridge (window apps over the C ABI, panels over JSON lines), the `omarchy-app` template with the `omasharp` agent skill (`check.sh`: unit tests, then the app run in a private compositor, with its visible text and a screenshot), `UpdateBridge`, `Drawing`/`DrawView`, `Theme`, and async methods as Promises. Apps build with warnings as errors, .NET's recommended analyzers and the native AOT ones on every build, and omasharp's own analyzers (the `Omasharp.Analyzers` package, OMASHARP001–010) checking the backend against what QML can do. The bridge calls `Dispose()` on an `IDisposable` backend when the app exits. Panels use `Commons.Color` for the shell's palette, which Qt 6.12 requires.
