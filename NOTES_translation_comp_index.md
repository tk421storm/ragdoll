# Translation error: "Field activateLabelString does not exist in type TKS_Ragdoll.CompProperties_Tossable"

Status: investigated 2026-10-15, **not applied**. Judged too risky for a translation-only error; ideally the
affected translations should name comps by class instead of by position.

## Report

Arthur GC (Workshop, 2026-09-03), with Apex Mechanoids:

```
Exception in Verse.DefInjectionPackage.SetDefFieldAtPath: System.InvalidOperationException:
Field activateLabelString does not exist in type TKS_Ragdoll.CompProperties_Tossable.
```

## Cause

- `Patches/Patches.xml` adds `TKS_Ragdoll.CompProperties_Tossable` to the `comps` of the abstract
  `BasePawn` and `ResourceBase` ThingDefs.
- With XML inheritance a child def's list is the parent's entries followed by its own, so every pawn and
  resource def's own comps move down by one place.
- DefInjected translations can address a comp by its position (`<Def.comps.1.field>`) or by class
  (`<Def.comps.CompUsable.field>`). Position paths written without Ragdoll then point one comp too early and
  can land on ours, which has no such field. Handles match on the list item's class or field values, so only a
  numbered path can reach Ragdoll's comp.
- `activateLabelString` is a `CompProperties_Interactable` field, so Apex Mechanoids (or a translation of it)
  presumably uses a numbered path on a mech with an interactable comp. Not verified: Apex has no public
  source and was not installed.

Also affects vanilla Danish: the official Danish translation uses `PsychicSoothePulser.comps.1.useLabel` and
`PsychicAnimalPulser.comps.1.useLabel` (both inherit from `ResourceBase`). Every other official language uses
`comps.CompUsable.useLabel`, which is unaffected.

Corpses are not affected: `ImpliedCorpseDefs` postfix appends the comp at the end of the list.

## Possible fix (not applied)

Harmony prefix on `LoadedLanguage.InjectIntoData_BeforeImpliedDefs` (present in 1.5, 1.6 and 1.7). Defs are
in the DefDatabase by then and Ragdoll's Harmony patches are applied in the Mod constructor, so it runs in
time. It moves our comp to the end of each ThingDef's comps list, so position paths match what translators
saw. XML scope is unchanged; only the comp's position changes.

```csharp
    [HarmonyPatch(typeof(LoadedLanguage))]
    public static class LoadedLanguage_patches
    {
        //the XML patch adds our comp to BasePawn and ResourceBase, which puts it ahead of every child def's own comps.
        //translations can address comps by position (e.g. "PsychicSoothePulser.comps.1.useLabel"), so move ours to the
        //end of the list before translations are injected, otherwise they land on our comp and error
        [HarmonyPatch(typeof(LoadedLanguage), "InjectIntoData_BeforeImpliedDefs")]
        [HarmonyPrefix]
        public static void InjectIntoData_BeforeImpliedDefs_prefix()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                List<CompProperties> comps = def.comps;
                if (comps == null || comps.Count < 2)
                {
                    continue;
                }

                int index = comps.FindIndex(comp => comp is CompProperties_Tossable);
                if (index < 0 || index == comps.Count - 1)
                {
                    continue;
                }

                CompProperties tossable = comps[index];
                comps.RemoveAt(index);
                comps.Add(tossable);
            }
        }
    }
```

Compiled against 1.6 (placed after `Corpses_patches`). Test: Danish language + Ragdoll, no startup errors for
`PsychicSoothePulser`; Apex Mechanoids, no `activateLabelString` error.

## Alternative

Reply to the reporter / Apex author: use class handles instead of numbered paths, as the vanilla translations do
(`comps.CompUsable.useLabel` names the comp class), which also survives any other mod adding comps.
