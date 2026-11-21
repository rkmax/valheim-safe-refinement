# SafeRefinement

Valheim mod: a failed refinement at the Forge of Potential no longer destroys your item. It stays at the level it had.

The odds and costs don't change. A refinement still succeeds 65% of the time, and the idol and materials are used up whether it succeeds or fails.

Each player who refines needs it. The roll happens in the game of the player standing at the Forge, so a player without the mod still loses the item on a failure. Nothing goes on the server.

## Install

You need [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).

Download `SafeRefinement-<version>.zip` from [Releases](../../releases), then:

- **r2modman / Thunderstore Mod Manager:** Settings > Import local mod, pick the zip.
- **Manual:** copy `plugins/SafeRefinement.dll` from the zip into `BepInEx/plugins` of your Valheim folder.

Don't run it together with another Forge of Potential mod (MySix Forge of Potential, PotentialReforged and others): they change the same code.

## What it does

In the unmodded game every idol has a break chance of 1, so the 35% of tries that don't succeed destroy the item and give back 35% of its materials. With the mod, a failed try gives the item back at its current level and plays no failure effect. The message in the middle of the screen says so, with a random taunt under it:

> Mistwalker failed to refine. Still level 4.
> *Odin saw that. He's still laughing.*

If the game's refinement code changes in an update, the mod doesn't patch it and writes an error to the BepInEx log. Refinement is then vanilla again, breaks included, until the mod is updated.

## Settings

`BepInEx/config/rkmax.saferefinement.cfg`, read again when you save the file:

- `Enabled` (true): turn the mod off to get vanilla refinement back.
- `FailureEffect` (false): play the Forge's failure effect on a failed try.

Taunts live in `BepInEx/config/rkmax.saferefinement.quotes.txt`, one per line, written with 15 defaults the first time the game starts. Edit it while playing: the next failure uses the new list. Lines starting with `#` are skipped, and an empty file means no taunt. A taunt never repeats the previous one.

## Build

```bash
dotnet build src/SafeRefinement -c Release -p:OutputPath=$PWD/out/
bash scripts/package.sh   # makes dist/SafeRefinement-<version>.zip
```

The build uses the game's assemblies from your Steam install (`ValheimDir`). Add `-p:Deploy=true` to also copy the DLL into your game's `BepInEx/plugins`.
