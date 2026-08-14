# QuickPrice — Decompilation & Rebuild Handoff

Date: 2026-08-13
Agent environment: sandboxed Linux container, no network access to `nuget.org` or
`nuget.bepinex.dev` (see "Environment constraints" below). All facts below are either
directly observed in this session or explicitly marked as unverified.

## TL;DR

- `QuickPrice.dll` (v0.2.0.0, 14,848 bytes) was decompiled with **real** `ICSharpCode.Decompiler`
  (v11.0.0.9375, the library behind `ilspycmd`/ILSpy), with full assembly resolution against the
  real `Assembly-CSharp.dll`, real `BepInEx.dll` 5.4.21.0, and real `0Harmony.dll` 2.7.0 (HarmonyX).
- Output: 4 types across 3 files — `Plugin`, `Patch_AutoPrice`, `Patch_NotebookDetailItem`,
  `ConfigurationManagerAttributes` — all in namespace `QuickPrice`.
- The hand-cleaned source in `src/` was **compiled successfully** and **round-tripped through the
  decompiler again to confirm zero semantic drift** from the raw decompile in `decompiled-raw/`.
  See "Build verification" for exactly what this does and doesn't prove.
- **The old prototype's transpiler (swapping `GetLastPrice` → `GetMaxCorrectPrice`) is gone.**
  It was replaced by a completely different mechanism — see "Task 6" below.

## Environment constraints hit in this sandbox (read this first)

This container's network egress is allow-listed and does **not** include `nuget.org` or
`nuget.bepinex.dev` (confirmed via direct `curl`, both return `403 host_not_allowed` from the
egress proxy). `github.com`, `api.github.com`, and GitHub's release-asset CDN **are** reachable.
This shaped the whole approach:

- **Decompiler**: instead of `dotnet tool install ilspycmd` (needs nuget.org), the official
  **ILSpy 11.0 self-contained Linux release** was downloaded directly from
  `github.com/icsharpcode/ILSpy/releases`, and `ICSharpCode.Decompiler.dll` was extracted from it
  and driven directly via a small host program (reflected the real API first, no guessing).
- **BepInEx / HarmonyX**: real binaries were obtained from **official GitHub releases**, not
  NuGet — `BepInEx_x64_5.4.21.0.zip` from `github.com/BepInEx/BepInEx/releases` (contains
  `BepInEx.dll` 5.4.21.0), and `HarmonyX.2.7.0.nupkg` from `github.com/BepInEx/HarmonyX/releases`
  (a `.nupkg` is just a zip; extracted `0Harmony.dll` from `lib/netstandard2.0/` without touching
  a NuGet client).
- **UnityEngine.\***: no legitimate source for real Unity reference assemblies was reachable from
  this sandbox (they're proprietary, normally supplied from the game's own
  `Moonlighter_Data/Managed/` folder — which the user has, but wasn't uploaded). A small,
  hand-verified **stub** assembly set was built instead, covering only the exact API surface
  QuickPrice's source touches (see "Build verification").
- **This means**: `dotnet build` on `MoonlighterBestDeal.csproj` as shipped (which uses real
  `PackageReference`s per your spec) has **not been run to completion in this sandbox** — it needs
  `nuget.org` (for HarmonyX) and `nuget.bepinex.dev` (for `BepInEx.Core`, `BepInEx.PluginInfoProps`,
  `UnityEngine.Modules`), neither reachable here. **This should build cleanly on your own machine**,
  which presumably has normal internet access; the NuGet package IDs/versions were verified to
  exist via web search (not guessed) — see "NuGet package verification" below. If it doesn't,
  that's the first thing to debug, and the stub-based verification build in `verify-build-notes.md`
  gives a strong baseline for what "correct" looks like.

## NuGet package verification (not guessed)

Searched and confirmed via nuget.org/BepInEx docs before writing the csproj:
- `BepInEx.Core`, `BepInEx.PluginInfoProps`, and `UnityEngine.Modules` are published **only** on
  `https://nuget.bepinex.dev/v3/index.json` — a GitHub issue on BepInEx/BepInEx explicitly shows
  `NU1101: Unable to find package BepInEx.Core. No packages exist with this id in source(s):
  nuget.org`, confirming they are not mirrored to nuget.org.
- `HarmonyX` **is** on nuget.org directly (`nuget.org/packages/HarmonyX/2.7.0` exists, confirmed).
- `MoonlighterBestDeal.csproj`'s sibling `NuGet.Config` adds the BepInEx feed alongside nuget.org,
  matching the pattern in official BepInEx docs and the original repo's own `NuGet.Config`.

## Repo layout (this folder, `QuickPrice2/`)

```
QuickPrice2/
  MoonlighterBestDeal.csproj   - the buildable project (netstandard2.0, NuGet refs, see task 3)
  NuGet.Config                 - nuget.org + BepInEx feed
  src/                         - hand-cleaned decompiled source (this is what you edit/build)
    Plugin.cs
    Patches/
      Patch_AutoPrice.cs
      Patch_NotebookDetailItem.cs
    ConfigurationManagerAttributes.cs
    Properties/AssemblyInfo.cs
  decompiled-raw/              - untouched ILSpy WholeProjectDecompiler output, for diffing/audit
  HANDOFF.md                   - this file
  verify-build-notes.md        - exactly what was compiled/verified in this sandbox, and how
```

`MoonlighterBestDeal.csproj` references `../Assembly-CSharp.dll` (the copy already at the repo
root) with `<Private>false</Private>`, rather than duplicating the 3.9MB file inside this folder.

Note: `AssemblyName` is `QuickPrice` (matching the actual shipped mod's identity — GUID
`zzl.moonlighter.quickprice`, product/title `QuickPrice`), while the **project file** is named
`MoonlighterBestDeal.csproj` per your instruction. These are intentionally different; flagging it
so it doesn't look like a mistake.

## Task 4 — Full catalog: Harmony patches, ConfigEntries, hotkeys

### Harmony patch classes (2)

Both are discovered and applied via `Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(),
"zzl.moonlighter.quickprice")` in `Plugin.Awake()` — not manual `Harmony.PatchAll()` on an
instance, and not transpilers.

| Class | Target | Patch type | Purpose |
|---|---|---|---|
| `Patch_AutoPrice` | `ShowcaseSlotGUI.SetItemStack` | `[HarmonyPostfix]` | When `EnableAutoPrice` is on and an item is placed on a showcase (not during initialization), auto-sets its price per the configured `PriceTier`. |
| `Patch_NotebookDetailItem` | `NotebookPanel.DetailItem` | `[HarmonyPostfix]` | When `EnableNotebookHighlight` is on, highlights (green) whichever of the notebook's 4 "last price" labels (TooCheap/Cheap/Expensive/TooExpensive) matches the item's current computed price tier boundary. |

### ConfigEntries (14 total, all via `BaseUnityPlugin.Config.Bind`)

| Section | Key | Type | Default | Notes |
|---|---|---|---|---|
| `0. Debug` | `Debug` | `bool` | `false` | Enables debug logging (`Order=99`) |
| `1. Notebook` | `Enable Notebook Highlight` | `bool` | `true` | Toggles `Patch_NotebookDetailItem`'s highlighting (`Order=50`) |
| `2. Auto Price` | `Enable Auto Price` | `bool` | `false` | Toggles `Patch_AutoPrice` (`Order=49`) |
| `2. Auto Price` | `Price Tier` | `string` | `"MarketPrice"` | One of 5 values (`AcceptableValueList`): `MaxPriceInTooCheap`, `MaxCorrectPrice`, `MaxPriceInExpensive`, `MaxPriceInTooExpensive`, `MarketPrice` (`Order=48`) |
| `3. Hotkeys` | `<Label> Gamepad` × 5 | `string` | see below | One of 15 `GamepadOptions` strings |
| `3. Hotkeys` | `<Label> Keyboard` × 5 | `KeyboardShortcut` (BepInEx type) | see below | |

Every `ConfigEntry` also carries a `ConfigurationManagerAttributes { Order = N }` — this is a
duck-typed class (no interface, matched by reflection) that the separate, popular "Configuration
Manager" BepInEx plugin uses to build its in-game settings GUI. Only `Order` is ever set; the
`Browsable`, `CustomDrawer`, and `CustomHotkeyDrawer` fields are declared (part of the contract)
but never assigned — this is expected, not a bug (the compiler will warn CS0649 on these, which is
correct and matches the original).

### Hotkeys (5 tiers × gamepad + keyboard = 10 bindings)

Built by `Plugin.MakeTier()`, checked every frame in `Plugin.Update()` — but only while a showcase
slot's price button (`ButtonPrizeHandler`) is the currently-selected UI element
(`EventSystem.current.currentSelectedGameObject`).

| Label | Tier (maps to `PriceTier` values) | Gamepad default | Keyboard default |
|---|---|---|---|
| TooCheap | `MaxPriceInTooCheap` | RS Down | Alt+1 |
| Cheap | `MaxCorrectPrice` | X | Alt+2 |
| Expensive | `MaxPriceInExpensive` | RS Right | Alt+3 |
| TooExpensive | `MaxPriceInTooExpensive` | RS Up | Alt+4 |
| Market Price | `MarketPrice` | RS Left | Alt+5 |

("Alt+N" = `KeyCode.Alt1`..`Alt5` combined with `KeyCode.LeftAlt` (308) via BepInEx's
`KeyboardShortcut`; digits are `KeyCode.Alpha1`(49)..`Alpha5`(53).)

Gamepad button mapping (`GamepadMap`, Xbox-style generic joystick buttons,
`KeyCode.JoystickButton0`=330 .. `JoystickButton9`=339): `A`=330, `B`=331, `X`=332, `Y`=333,
`LB`=334, `RB`=335, `BACK`=336, `START`=337, `LS`=338, `RS`=339.

Right-stick directions (`AnalogDirMap`, generic joystick analog axes 3=X, 4=Y, edge-detected
against a 0.7 threshold so a hotkey fires once per stick push, not every frame it's held):
`RS Up`=(axis 4, -1), `RS Down`=(axis 4, +1), `RS Left`=(axis 3, -1), `RS Right`=(axis 3, +1).

## Task 6 — Is the old transpiler still present?

**No — it's gone, replaced with a different architecture.** Definitive, from the actual decompiled
source (`decompiled-raw/QuickPrice/Patch_AutoPrice.cs` and `Patch_NotebookDetailItem.cs`):

- The old prototype (per your description) was a **single Harmony transpiler** on
  `ShowcaseSlotGUI.ResetEditingPrize` that rewrote IL to swap calls from `GetLastPrice` to
  `GetMaxCorrectPrice`.
- The current mod has **no transpilers at all**. It uses two `[HarmonyPostfix]` patches on
  **different methods** (`ShowcaseSlotGUI.SetItemStack` and the new `NotebookPanel.DetailItem` —
  neither is `ResetEditingPrize`).
- `GetLastPrice` is still called — but now purely for **reading** historical price data
  (`ItemPriceManager.GetLastPrice(item, ItemPriceValoration)`) inside `Patch_NotebookDetailItem`,
  to compare against freshly-computed tier boundaries for the notebook highlight feature. It is
  never swapped out for `GetMaxCorrectPrice` or anything else — both are called, for different
  purposes, in different places.
- Pricing itself (auto-price and hotkeys) is now driven by a configurable `PriceTier` dispatching
  to **five different** `ItemPriceManager` methods (`GetMinCorrectPrice`, `GetMaxCorrectPrice`,
  `GetMaxOverpricedPrice`, `GetTooExpensiveLimitPrice`, `GetCorrectPriceWithPopularity`) — a
  materially different and more flexible design than a single hardcoded method swap.

## Build verification — what was actually proven, and how

Two different things were checked. Be precise about which is which:

**1. Does the hand-cleaned `src/` compile?** Yes — proven, but not with the real shipped
`MoonlighterBestDeal.csproj` (blocked by NuGet access; see above). Instead, a parallel
verification project (`verify-build-notes.md` has full detail) referenced:
- The **real** `Assembly-CSharp.dll` (yours)
- The **real** `BepInEx.dll` v5.4.21.0 (from BepInEx's official GitHub release zip)
- The **real** `0Harmony.dll` v2.7.0 (from HarmonyX's official GitHub release nupkg)
- **Hand-built stub** `UnityEngine`/`UnityEngine.CoreModule`/`UnityEngine.InputLegacyModule`/
  `UnityEngine.UI` assemblies covering only the small API surface QuickPrice's source actually
  touches (`Object`, `Component`, `Behaviour`, `MonoBehaviour`, `ScriptableObject`, `GameObject`,
  `KeyCode`, `Color`, `Input.GetKeyDown`/`GetAxisRaw`, `EventSystem.current`, `Graphic`/`Text.color`)
  — **these stubs are not real Unity and are not part of the shipped project.** They exist only in
  `verify-build-notes.md`'s described location outside this folder, purely to let this sandbox
  typecheck the real source against real game/BepInEx/Harmony types.

Result: **0 errors**, 3 expected warnings (the unassigned `ConfigurationManagerAttributes` fields
noted above). Target framework for this verification build was `net8.0` (not `netstandard2.0`) —
purely a sandbox workaround, since `<Reference HintPath>` doesn't enforce TFM compatibility and
`netstandard2.0` class libraries in this sandbox need a `NETStandard.Library` NuGet restore that's
also blocked. **The shipped `MoonlighterBestDeal.csproj` correctly targets `netstandard2.0` as you
specified** — only the throwaway sandbox verification copy used `net8.0`.

**2. Did cleanup change behavior?** Checked by decompiling the freshly-built verification DLL a
second time and diffing its logic against the raw decompile. Result: **identical** — every
`Get*Price`/`SetPopularity`/`GetPopularity`/`SetLastSettedPrice` call, in the same order, same
conditions. The only differences between `decompiled-raw/` and `src/` are cosmetic (see next
section) and are proven behavior-preserving by this round-trip, not just asserted.

**What remains unverified**: an actual `dotnet build` of `MoonlighterBestDeal.csproj` exactly as
committed, with real NuGet restore against `nuget.org` + `nuget.bepinex.dev`, and an actual in-game
smoke test. Both require your machine. See "Next steps for you" below.

## Manual cleanup log (`decompiled-raw/` → `src/`)

All of the following are decompiler artifacts from ILSpy being unable to resolve
`UnityEngine.CoreModule`/`UnityEngine.UI` in the *first* decompilation pass (before the
verification build existed) — not logic changes. Each was confirmed behavior-identical by the
round-trip check above.

- `(Object)(object)x == (Object)null` → `x == null` (identical IL once `UnityEngine.Object`'s
  `==` overload is resolvable — this is exactly what real Unity code compiles to).
- `Color val = default(Color); ((Color)(ref val))._002Ector(0f, 0.8f, 0.2f);` →
  `Color highlight = new Color(0f, 0.8f, 0.2f);` (mangled ctor-via-ref call, `_002E` = hex for
  `.`; `Color`'s 3-arg constructor sets `a = 1` implicitly, matching Unity's actual `Color` struct).
  Confirmed against `Text.color`'s type via the real decompiled `Assembly-CSharp` source.
- `((Graphic)__instance.textLastTooCheap).color` → `__instance.textLastTooCheap.color` — verified
  the field is declared `public Text textLastTooCheap;` in the real `NotebookPanel` (decompiled
  from `Assembly-CSharp.dll`), and `Text : ... : Graphic` already exposes `color` directly; the
  cast was only needed because ILSpy hadn't resolved `UnityEngine.UI` in that pass.
- Local variable/`out var` names tidied (e.g. `value2`/`value3` → `gamepadKey`/`analogDir`, with
  named tuple elements `.Axis`/`.Dir` instead of `.Item1`/`.Item2`) — compile-time only, identical
  IL either way.
- `IsAnalogDirActive`'s `if (flag) return !flag2; return false;` simplified to
  `return isActiveNow && !wasActive;` — same truth table, checked by hand.
- Unused `using` directives removed from `AssemblyInfo.cs` (`System.Diagnostics`,
  `System.Runtime.CompilerServices`, `System.Runtime.Versioning`, `System.Security`,
  `System.Security.Permissions` — leftover from a stripped `SecurityPermissionAttribute` the
  decompiler intentionally omits).
- Physical file layout only: `Patch_AutoPrice.cs` and `Patch_NotebookDetailItem.cs` moved into a
  `Patches/` subfolder for readability. **The C# `namespace QuickPrice` declaration was
  deliberately left unchanged** on all types (folder structure and namespace don't have to match,
  and Harmony patch discovery via `CreateAndPatchAll` doesn't care about namespace at all — kept
  it identical to the real compiled mod for fidelity).

No control-flow, method signatures, config keys/defaults, or hotkey bindings were altered anywhere.

## Next steps for you

1. `cd QuickPrice2 && dotnet build` on your Linux Mint machine (needs internet access to
   `nuget.org` and `nuget.bepinex.dev`). Report back if `NU1101`/`NU1301`-type errors occur — the
   `NuGet.Config` may need adjusting for your NuGet client version, but the package IDs/versions
   themselves are confirmed to exist.
2. Copy the built `QuickPrice.dll` into `BepInEx/plugins/` and smoke-test in-game: toggle
   `EnableAutoPrice`, place an item on a showcase, confirm the price matches the selected
   `PriceTier`; toggle `EnableNotebookHighlight` and check the notebook highlight logic; try each
   of the 10 hotkey bindings.
3. If you have the game's `Moonlighter_Data/Managed/` folder handy, consider re-running the
   decompiler with the **real** `UnityEngine.CoreModule.dll`/`UnityEngine.UI.dll`/
   `UnityEngine.InputLegacyModule.dll` instead of this session's stubs, purely to double-check the
   round-trip conclusion above with zero stub involvement (should produce byte-identical logic;
   this is a nice-to-have confirmation, not expected to surface anything new).
