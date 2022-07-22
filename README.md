# The source code of the Fancy Lighting mod for tModLoader 1.4

## [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
## [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

### Changelog

**v0.4.0 (2022-07-22)**
- Smooth lighting and ambient occlusion now work in camera mode
- Added a new option to use modified sky colors
- Added a new option to make non-solid blocks generate ambient occlusion
- Reduced the default ambient occlusion radius
- Fixed unloading not disposing textures
- Removed the option to set the ambient occlusion radius to 7, as it had poor visual quality
- Slightly tweaked and optimized the fancy lighting engine
- Made a minor optimization to ambient occlusion
- Made a minor optimization to cubic upscaling

**v0.3.1 (2022-07-14)**
- Optimized rendering with smooth lighting enabled
- Updated some config tooltips
- Removed some unnecessary code

**v0.3.0 (2022-07-13)**
- Updated the mod icon
- The fancy lighting engine now uses more vanilla-accurate light masks for liquids
- Tweaked temporal optimization for the fancy lighting engine
- Made minor optimizations to the fancy lighting engine and ambient occlusion
- Optimized using smooth lighting without light map blurring
- Added more safeguards against uncaught exceptions in smooth lighting
- Fixed the mod description to reflect that the fancy lighting engine is now enabled by default
- Renamed some fields and changed access modifiers
- Renamed the Shaders folder to Effects

**v0.2.8 (2022-07-09)**
- Greatly optimized the fancy lighting engine
- Added a new option to make lighting brighter when using the fancy lighting engine
- The fancy lighting engine is now enabled by default
- Fixed a bug where the sky behind solid blocks could be too bright with smooth lighting enabled

**v0.2.7 (2022-07-04)**
- Improved the fancy lighting engine so that diagonals are no longer brighter
- Added support for lava lamps and partial support for Martian conduit plating as glowing tiles
- Honey now absorbs more light when using the fancy lighting engine
- Fixed a minor bug with the fancy lighting engine

**v0.2.6 (2022-07-03)**
- Tweaked and optimized the fancy lighting engine
- Reduced unintended flickering in glowing mushroom biomes when using the fancy lighting engine

**v0.2.5 (2022-07-03)**
- Updated the mod icon
- Added stronger protections against exceptions crashing the game
- Fixed glowing tiles flickering when using smooth lighting
- Added some minor optimizations

**v0.2.4.1 (2022-07-01)**
- Attempted to fix a potential crash

**v0.2.4 (2022-07-01)**
- Implemented and added an option to toggle high-quality light map upscaling
- Added a new option to toggle light map blurring
- Added a new debug option to render only lighting
- Glowing moss now glows properly with smooth lighting enabled
- Fixed a minor visual bug with glowing meteorite brick
- Applied some minor optimizations all around
- Updated config tooltips

**v0.2.3 (2022-06-24)**
- Optimized smooth lighting calculations, mitigating some visual issues
- Attempted to fix a bug that could cause a crash in some cases

**v0.2.2 (2022-06-23)**
- Optimized and multithreaded smooth lighting
- Fixed a bug with light map blurring
- Tweaked and optimized ambient occlusion

**v0.2.1 (2022-06-22)**
- Added a new option to make shadows darker when using the fancy lighting engine
- Enabling or disabling the fancy lighting engine no longer requires a reload
- Added a temporal optimization option to the fancy lighting engine
- Made other general optimizations to the fancy lighting engine
- The background can now also have smooth lighting when using the fancy lighting engine
- Shadow paint now absorbs even more light with the fancy lighting engine
- Fixed a visual bug when using the fancy lighting engine

**v0.2.0 (2022-06-21)**
- Added a new lighting engine with directional shadows
- Fixed a smooth lighting visual bug that occurred in deep water

**v0.1.1 (2022-06-19)**
- Added new options to change the intensity and radius of ambient occlusion
- The default setting for ambient occlusion has been made slightly more subtle
- Ambient occlusion now has a higher resolution (1x1 instead of 2x2)
- Ambient occlusion no longer slightly darkens all walls
- Added support for meteorite brick as a glowing block
- Fixed some minor visual bugs with glowing blocks
- Updated config UI
- Changed mod to client side only
- Included source code in tmod file

**v0.1 (2022-06-18)**
- Initial release (added to GitHub one day later)