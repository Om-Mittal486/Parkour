# The Stillworks

An abandoned utility city organised around an enormous hollow concrete core. Its original purpose is deliberately unresolved: intake streets become thermal infrastructure, vehicle circulation becomes construction, and an institutional complex gives way to a communications crown. The climb ends at 588 metres; the last mast reaches approximately 675 metres.

## Play

Open `Assets/Scenes/Stillworks.unity` and press Play. This is the first enabled scene in Build Settings. `MainScene.unity` remains the original controller test scene.

WASD moves; Shift toggles sprint; Space jumps; Ctrl slides. Escape pauses movement and the timer, releases the cursor, and offers restart. The run starts on movement. Reach the centre of the observation roof to finish; the best time is stored locally. Falling below the foundation returns the player to the street without clearing the current timer. There are no checkpoint rooms or automatic upward teleports.

## Architectural route

| Elevation | District | Circulation and identity |
| --- | --- | --- |
| 0–84 m | Intake Quarter | Streets, sealed service doors, dead lamps, abandoned cars, wide terraces and processional stairs. |
| 84–168 m | Heat Exchange | Reservoirs, pipe mains, covered process galleries, broken expansion joints and loading lintels. |
| 168–252 m | Transit Stack | Wide asphalt parking floors, column arcades, divided bays, long graded ramps and a disused parking annex. |
| 252–336 m | Unfinished Works | Narrower construction floors, exposed columns, projecting rebar, steel roof girders and a lattice tower crane. |
| 336–420 m | The Ministry | Deep concrete fins, tall sheltered halls, monumental walls, courtyards and a blind administrative tower. |
| 420–504 m | Relay Needles | Exposed maintenance galleries, vertical cable risers, longer broken joints and an elevated relay crane. |
| 504–588 m | The Crown | Concrete blades, narrow upper circulation, a final bridge and a large observation roof beneath a communications arch. |

The fourteen ascending circulation levels use six irregular building wings each. Wings are joined by broad courts rather than isolated landing blocks. Width, enclosure, surface, facade rhythm and structural vocabulary change by district. Massive plinths, cantilever knees, deep wall sections and the continuous central core explain the structure. Lower roofs remain visible, and fog starts at 700 metres rather than obscuring the climb.

The main route follows floors, ramps, staircases and fractured gallery joints. Each district also has an exposed maintenance stair with missing landing grating. It climbs 42 metres and skips an entire circulation level; its narrow stairs and five 3.8 metre gaps trade safety for a substantial distance saving. Side courts branch from the main floors and rejoin them. Broken main galleries have catch floors five metres down with side ramps returning to the upper floor.

Main-floor openings have 3.2 metre gaps in the middle districts and 4.2 metre gaps high up. Loading lintels provide 1.25 metres of clearance over a short passage, allowing the 1 metre slide capsule through. Long concrete facades and low dividers reserve geometry for future wall-running and vaulting. Those future abilities are not required to complete the route.

The route length and a theoretical uninterrupted sprint time are recorded in `Stillworks-validation.txt`. The intended 15–30 minute skilled run is a design target, not a measured human completion time. The route can be substantially shortened through the maintenance stairs. Human playtesting should tune recognition, fatigue, missed-jump recovery and repeated-run timing before treating it as production-balanced.

## Unity implementation

`Stillworks / Build playable map` rebuilds the separate scene using the original scene's existing player and input references. It writes stable reusable material and mesh assets under `Assets/Stillworks/Generated`. Rebuilding replaces generated scene edits; keep bespoke edits in another scene or change the generator. The route manifest records passage endpoints and widths for inspection and validation.

Architecture is generated in the editor and saved as normal scene objects. There is no runtime map generation, package download or dependence on Blender. Structural geometry is combined by district wing and shared material. Secondary dressing uses LOD groups; simplified city blocks carry distant silhouettes. Boxes provide most collision, ramps use small wedge meshes, and decorative nosings use smooth staircase collision. Eight restrained materials share a world-space weathered concrete shader, avoiding stretched textures on enormous walls.

The new scene normalises the player scale, removes its redundant capsule collider and visible body mesh, sets a 1.62 metre eye height and a 2.4 kilometre far clip, and retains the serialized 14 m/s sprint speed. The movement change prevents standing into low ceilings, permits exiting a passage after slide momentum expires, restores full height after a slide, and exposes motion reset for restarting a run.

The generator's `Validate current map` command uses Unity physics to check recorded floor continuity, capsule headroom and conservative jump distances. A geometric check is not a substitute for a full player-driven run or target-hardware profiling. The level remains an editable gameplay environment intended for those next production passes.
