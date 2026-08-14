# Verification build notes (sandbox-only, not part of the shipped project)

This documents exactly how `src/` was proven to compile in an environment with no access to
`nuget.org` or `nuget.bepinex.dev`, for anyone who wants to reproduce or extend the check. None of
this is part of `MoonlighterBestDeal.csproj` or committed to the repo as buildable artifacts —
it's a record of the verification method.

## 1. Real binaries obtained (no NuGet client involved)

```bash
# ICSharpCode.Decompiler.dll v11.0.0.9375 (ILSpy 11.0, self-contained Linux release)
curl -sL "https://github.com/icsharpcode/ILSpy/releases/download/v11.0/ILSpy_linux-x64_11.0.0.9375.zip" -o ilspy.zip
unzip ilspy.zip -d extracted   # -> extracted/ICSharpCode.Decompiler.dll

# BepInEx.dll v5.4.21.0
curl -sL "https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_x64_5.4.21.0.zip" -o bepinex.zip
unzip bepinex.zip -d bepinex_extracted   # -> bepinex_extracted/BepInEx/core/BepInEx.dll

# 0Harmony.dll v2.7.0 (HarmonyX) - a .nupkg is just a zip
curl -sL "https://github.com/BepInEx/HarmonyX/releases/download/v2.7.0/HarmonyX.2.7.0.nupkg" -o harmonyx.nupkg
unzip harmonyx.nupkg -d harmonyx_extracted   # -> harmonyx_extracted/lib/netstandard2.0/0Harmony.dll
```

## 2. Decompiler host

A small net10.0 console app (net10.0 needed only because `ICSharpCode.Decompiler.dll` from ILSpy
11.0 was built against net9/net10-era `System.Collections.Immutable`/`System.Reflection.Metadata`;
net8.0 hit a hard version-mismatch `FileLoadException` since those BCL-extension assemblies aren't
unified across major versions the way core framework assemblies are) that:
- References `ICSharpCode.Decompiler.dll` via plain `<Reference HintPath>` (no PackageReference)
- Calls `ICSharpCode.Decompiler.CSharp.ProjectDecompiler.WholeProjectDecompiler.DecompileProject(...)`
  (exact API confirmed via runtime reflection before writing any code — not assumed)
- Resolves assembly references via `UniversalAssemblyResolver` with a search directory containing
  `Assembly-CSharp.dll` + `BepInEx.dll` + `0Harmony.dll` (all real, from step 1) +
  `QuickPrice.dll` itself.

`NuGet.Config` in this project has `<packageSources><clear /></packageSources>` — with zero
`PackageReference`s, `dotnet build` needs no network at all.

## 3. Unity stub assemblies

Real `UnityEngine.*` reference assemblies were not obtainable in this sandbox (proprietary Unity
binaries; normally sourced from the game's `Moonlighter_Data/Managed/` folder). Four minimal net8.0
class libraries were built instead, matching the **exact assembly names** Assembly-CSharp.dll
itself references (confirmed via reading its `AssemblyRef` metadata table directly with
`System.Reflection.Metadata`, not guessed):

| Stub assembly name | Types provided | Why this exact name |
|---|---|---|
| `UnityEngine.CoreModule` | `Object`, `Component`, `Behaviour`, `MonoBehaviour`, `ScriptableObject`, `GameObject`, `KeyCode`, `Color` | Matches `Assembly-CSharp.dll`'s own `AssemblyRef` table entry `UnityEngine.CoreModule, Version=0.0.0.0` |
| `UnityEngine.InputLegacyModule` | `Input.GetKeyDown`, `Input.GetAxisRaw` | Matches Assembly-CSharp's `UnityEngine.InputLegacyModule` reference (also seen directly in `QuickPrice.dll`'s own metadata strings) |
| `UnityEngine.UI` | `Graphic`, `Text`, `EventSystems.EventSystem` | Matches Assembly-CSharp's `UnityEngine.UI, Version=1.0.0.0` reference; confirmed `EventSystem` is *not* a separate `UnityEngine.EventSystemsModule` reference for this Unity version — `NotebookPanel.cs`'s real `using UnityEngine.EventSystems;` resolves from the same `UnityEngine.UI` assembly, per Assembly-CSharp's reference list |
| `UnityEngine` (facade) | `[assembly: TypeForwardedTo(...)]` for `Object`/`Component`/`Behaviour`/`MonoBehaviour`/`ScriptableObject`/`GameObject`/`KeyCode`/`Color` | `BepInEx.dll` 5.4.21.0's `BaseUnityPlugin` was itself compiled against the old monolithic `UnityEngine, Version=0.0.0.0` assembly name (pre-module-split facade), which real Unity ships as a pure type-forwarder to the split modules for back-compat. Without this, `Plugin : BaseUnityPlugin` fails with `CS0012` even though `MonoBehaviour` exists in the CoreModule stub — discovered by hitting the actual compiler error, not anticipated up front. |

All four target `net8.0` (not `netstandard2.0`) purely to avoid this sandbox's blocked
`NETStandard.Library` NuGet restore — `<Reference HintPath>` doesn't enforce TFM compatibility, so
this has no effect on whether the *real* `QuickPrice` source compiles correctly against them.

## 4. Verification project

A copy of `src/` was compiled as a `net8.0` project referencing the 3 real binaries (step 1) + 4
stubs (step 3) via `<Reference HintPath>`. Result: **build succeeded, 0 errors, 3 expected
warnings** (`CS0649` on `ConfigurationManagerAttributes.Browsable`/`CustomDrawer`/
`CustomHotkeyDrawer` — correctly unassigned, matches the original compiled mod's actual behavior).

## 5. Round-trip check

The freshly-built verification `QuickPrice.dll` was decompiled again with the same tool from step
2. The resulting `Patch_NotebookDetailItem.cs` (the file with the most cleanup) came back with
**zero decompiler artifacts** this time (proper `UnityEngine.UI` resolution from the stub this
time round) and, when diffed against the original raw decompile
(`decompiled-raw/QuickPrice/Patch_NotebookDetailItem.cs`) on every `Get*Price`/`SetPopularity`/
`GetPopularity`/`SetLastSettedPrice` call and its surrounding condition, was **identical**. Same
check run against `Plugin.cs` and `Patch_AutoPrice.cs`.

## What this does NOT prove

- That `MoonlighterBestDeal.csproj` builds via `dotnet build` with real NuGet restore (untested in
  this sandbox — needs your machine's network access).
- That the mod behaves correctly at runtime inside Moonlighter (no runtime/game testing was
  possible in this sandbox).
- That the stub `UnityEngine.*` types exactly match real Unity 2019.2.20's public API beyond the
  narrow slice QuickPrice's source touches (they were deliberately minimal, verified member-by-
  member against actual usage — not a general-purpose Unity stub).
