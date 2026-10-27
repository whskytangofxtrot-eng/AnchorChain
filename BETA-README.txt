Unofficial AnchorChain beta preloader compatibility fork

This branch provides the one-time preloader update for WhskyTangoFxtrot's
DCM Beta Compatibility patch. Supported versions: Sea Power 0.8.7-261026,
AnchorChain 1.1.0 and Bungalow's Dynamic Campaign Mod 0.25.0.1.

Players: download the preloader ZIP from this fork's Releases page and follow
READ-ME-FIRST.txt. Keep your original BepInEx and AnchorChain installations.
The ZIP replaces one file: BepInEx/plugins/AnchorChain.Preloader.dll.

Workshop patch:
https://steamcommunity.com/sharedfiles/filedetails/?id=3828162693

The preloader waits for Sea Power's mod-directory refresh, checks the in-game
mod selections, and loads only checked code mods. Checkbox changes require a
restart. It leaves subscribed Workshop files untouched.

With the DCM patch enabled, the separate Workshop helper prepares a verified
local cache from the player's original DCM DLL. With that patch disabled, the
preloader loads the original code of enabled mods; DCM's original beta
incompatibilities may return. The loader never downloads or auto-updates code.

BUILD FROM SOURCE
Requires the .NET 8 SDK and a local installation of the supported Sea Power
beta, BepInEx and AnchorChain. Run build-preloader.ps1 with -GameDirectory set
to the game's installation folder. It compiles AnchorChain.Preloader/Preloader.cs
against your local game/BepInEx references and writes bin/AnchorChain.Preloader.dll.
It does not copy anything into your game or redistribute game reference DLLs.

The original AnchorChain sources and MIT license are retained in this fork.
This branch changes only the preloader and its build/release documentation;
it is not an upstream AnchorChain release.

Bungalow deserves 100% of the credit for creating the Dynamic Campaign Mod.
The separate DCM compatibility patch and optional experimental map are an
independent, temporary effort by WhskyTangoFxtrot, without collaboration with
Bungalow, until Bungalow chooses a direction for the mod.
