# The source code of the Fancy Lighting mod for tModLoader 1.4.4

## [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
## [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

### Latest Version

**v0.7.0 (2023-07-31)**
Multiple features have been added to the Ultra preset, decreasing performance. The new Very High preset is roughly equivalent to the old Ultra preset.
- Updated for Terraria 1.4.4.9
- Renamed some presets and added a new preset
- Renamed some options and updated some tooltips
- Added gamma correction when using overbright rendering and enhanced shaders and colors
- Added an option to apply a tone mapping curve to the light map
- Added an option to change the internal resolution of the fancy lighting engine
- Added an option to change the light absorption of solid blocks when using the fancy lighting engine
- Added an option to use enhanced light map blurring
- Added another option controlling the strength of ambient occlusion
- Added experimental global illumination
- Tweaked simulated normal maps
- Improved ambient occlusion
- Changed the minimum and maximum ambient occlusion radius to 2 and 5 (from 1 and 6)
- Changed some default ambient occlusion settings
- Changed sky color profile 1
- Improved render only light when paired with overbright rendering
- Added support for tile shine effects when using smooth lighting
- Added support for more glow effects when using smooth lighting
- Tweaked some pre-existing glow effects
- Increased the maximum thread count to 32, but capped the default value to 16
- Cave backgrounds and water now always use custom rendering when smooth lighting is enabled
- Optimized the fancy lighting engine
- Improved temporal optimization for the fancy lighting engine
- Slightly improved the quality of the fancy lighting engine in camera mode
- Slightly improved how light spreads when using the fancy lighting engine
- Reduced visual disruption after updating settings
- Made some minor optimizations
- Removed shadow paint blocking all light when using the fancy lighting engine
- Removed the option to toggle higher-quality normal maps, which are always used now
- Updated the mod description
- Fixed a visual issue with blocks adjacent to glowing blocks when using smooth lighting
- Fixed a camera mode bug where overbright lighting was not applied to tile entities
- Fixed a bug that disabled dithering when using a specific combination of settings
