# The Stillworks

A single-player first-person laundry rescue through an abandoned brutalist utility city. Climb seven connected districts to the clothesline at 588 metres, then bring the clothes back to the street shelter before 45 minutes of rain soak them.

Open `Assets/Scenes/Stillworks.unity` in Unity **6000.3.10f1** and press Play. The title screen starts the game. The original `MainScene.unity` remains available as the controller test scene.

**Controls:** WASD move, Shift toggle sprint, Space jump, Ctrl slide, E collect the laundry, Escape pause. Menus support mouse and keyboard selection. Movement requires mouse and keyboard.

Hold Space while moving forward at speed beside a vertical wall to wall-run for up to 1.65 seconds; release Space to kick away. Hold Ctrl against a wall while falling to wall-slide; press Space to exit a wall-slide. Land to refresh a wall-run on the same surface.

- The main route is approximately 13.7 km. Seeded layouts vary obstacle locations, spacing and combinations: skewed girders, fallen pipe bundles, toppled cabinets, compressor banks, raised cable inspection beds, offset loading shutters and abandoned shuttering. Low passages vary between ducts, pipes, roller shutters and ventilation hoods, with occasional uninterrupted sprint galleries.
- Required jump and slide lanes have guard fences. Construction and relay transfers include 65 cm structural beams; upper fire escapes use 72 cm stairs and 6.4 m gaps onto 85 cm landings. Lower transfer roofs provide recovery routes.
- A global URP atmosphere filter uses restrained colour grading, ACES tone mapping, subtle bloom and grain. Movement adds camera lean, speed FOV and concrete contact dust. Reduced motion disables camera lean and particles.
- Near, middle and distant rain surround the player in all directions; a separate city-wide rain volume covers the full vertical skyline. Roof occlusion and exposed-floor splashes accompany the original cloud sky, reservoirs and industrial surroundings. Economy mode reduces all rain layers.
- Seamless mipmapped concrete, cracked asphalt, corroded steel and flaking-paint textures cover the architecture. Large chipped facade bands, service stencils and faded blue, oxide red, ochre and teal details add colour. Separated foundation/junction surfaces fix the starting ground's z-fighting.
- Settings include mouse sensitivity, field of view, master volume, separate music/rain volume, reduced camera motion, inverted look, fullscreen and economy graphics. An original ambient music loop and button clicks accompany softer rain audio.
- **Save & Return to Title** suspends your run at the latest clear standing position. Continue restores its position and timer. A fall never automatically advances you upward.
- Collecting the laundry changes the objective to returning to the starting shelter. Deliver it to win; 100% wetness ends the attempt. Pausing stops the wetness clock. Best delivery time and one suspended run are saved locally.
- Fifty-six original narrator clips cover each circulation level, two gallery landmarks per level, the laundry story and six rotating fall taunts. Physical trigger zones queue story lines without overlapping speech or repeating visited cues; the second level has its own arrival line. Lost-height landings and large falls trigger repeatable taunts. Subtitles accompany baked offline voices; expanded 64-bit story progress survives a saved run. Edit `Assets/Stillworks/Dialogue.json` and regenerate the clips with `Tools/GenerateNarration.ps1` in Windows PowerShell. `Tools/GenerateSoundtrack.ps1` generates the music loop.

## Tools

- **Stillworks / Build playable map** regenerates the baked scene and shared assets. Generated scene edits are replaced when rebuilding.
- **Stillworks / Validate current map** checks floors, headroom and jump distances across the recorded routes.
- **Stillworks / Run controller smoke checks** exercises the actual input/controller and game screens. It preserves an existing suspended save.
- **Stillworks / Build Windows game** writes `Builds/Stillworks/Stillworks.exe` and its required companion files.

The game uses Unity's existing Input System, URP and Canvas UI. Audio is synthesized locally; no downloaded assets or online services are required to play.

See [map design and implementation](Documentation/Stillworks.md), [geometry validation](Documentation/Stillworks-validation.txt), and the screenshots in `Documentation/Previews`.
