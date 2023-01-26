# The source code of the Fancy Lighting mod for tModLoader 1.4

## [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
## [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

### Latest Version

**v0.6.0 (2023-??-??)**
- Added a new option (disabled by default) to take advantage of HiDef graphics profile features, when possible
- Higher-quality normal map simulation now uses a better formula when using HiDef features
- Normal maps no longer affect walls without higher-quality normal maps enabled
- When using HiDef features, lighting is no longer darker with overbright and render only lighting enabled
- The Light Loss When Exiting Solid Blocks setting now has a max value of 100 (up from 65)
- Shadow paint now absorbs 100% of light with the fancy lighting engine enabled
- Improved dithering slightly
- Fixed bug where ambient occlusion did not render if smooth lighting was disabled
- Smooth lighting is now automatically disabled if an IndexOutOfRangeException occurs
- Lots of code changes