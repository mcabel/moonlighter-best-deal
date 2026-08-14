# Raw decompiler output (unedited)

This folder is the **untouched** output of `ICSharpCode.Decompiler`'s `WholeProjectDecompiler`
run directly against the original `QuickPrice.dll` (v0.2.0.0, 14,848 bytes), with assembly
resolution against the real `Assembly-CSharp.dll`, `BepInEx.dll` 5.4.21.0, and `0Harmony.dll`
2.7.0 (HarmonyX).

It is preserved here for **provenance and auditing** — so anyone can diff the hand-cleaned
source in `../src/` against exactly what the decompiler produced, with nothing added or removed
beyond cosmetic cleanup. See `../HANDOFF.md` for the itemized list of every change made between
this folder and `../src/`, and why each one is behavior-preserving.

**`QuickPrice.csproj` in this folder is NOT buildable as-is.** It's ILSpy's auto-generated
scaffold, with `HintPath`s pointing at the decompilation sandbox's temp directories and no
`HintPath` at all for the `UnityEngine.*` references (ILSpy couldn't resolve them, since real
Unity engine binaries aren't available in that environment either). Use `../MoonlighterBestDeal.csproj`
to actually build the mod.
