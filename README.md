(readme version: April 16, 2023)

# Hyperion Tech Tree
A modded tech tree independent of the stock KSP2 tech system.

## Player Guide

For players, the mod should be mostly plug-and-play. Technical details relating to the data files of the mod should only become relevant when trying to incorporate part mods or planet mods; in these cases, instructions will be given.

### UI Navigation

Both in the VAB and in-flight, there should be an app tray button for Hyperion Tech Tree. Clicking this menu will open a window. Currently, there are two tabs: Tech Tree and Goals. The tech tree tab lets the player unlock tech nodes and unlock parts. The goals tab lets the player see science-gathering information, such as where the player has been.

![image](https://user-images.githubusercontent.com/69665635/232379753-b31fd32a-f4c9-4e8c-9186-6618f7368af4.png)

![image](https://user-images.githubusercontent.com/69665635/232380313-148214a9-4e5c-47f8-9a80-a84e11377a80.png)

## Modding Guide

If you plan to use HTT without any part mods or planet mods, and are not a mod developer who wishes to build a mod around this one, feel free to disregard the rest of this readme.

Inside the mod folder for HTT are several subfolders, each having different purposes. For every one of these, putting in a new .json file with the same structure will automatically append the new json's data into the mod, allowing for easy moddability.

### assets/images

This is where all of the icons used by the mod are stored. To create an icon for a tech tree node, create a 24x24 image with a transparent background and white foreground, and name it `[nodeID].png`. The mod will automatically load that texture onto the node in-game.

### Fluff

(currently unimplemented into the mod)

### Goals

This is where all planet-related information is stored. This includes the award amounts for entering each situation, where each situation begins or ends, whether a celestial body has an atmosphere or surface. The mod will not recognize a planet unless it is in this directory, so if you're making a planet mod don't forget to create goals data for it for this mod to use!

```json
{
    "modVersion": "0.0.1", // Throws warning to log if this doesn't match the swinfo.json version number
    "bodies": [
        {
            "bodyName": "Kerbol", // The celestial body's ID exactly.
            "spaceThreshold": 1000000000, // The altitude from sea level where low space becomes high space.
            "atmosphereThreshold": 18000, // The altitude from sea level where low atmosphere becomes high atmosphere. This is NOT the atmosphere height of the body; that is programatically calculated by the program.
            "hasAtmosphere": true, // Toggles if the atmosphere rewards are to be triggered.
            "hasSurface": false, // Toggles if the landed reward is to be triggered.
            "highSpaceAward": 80, // Amount of tech points for claiming this situation. Disclaimer that revisiting a situation on a separate flight will grant additional points, albeit at a reduced rate; specifically, this value divided by 2^(amount of times this situation has been claimed).
            "lowSpaceAward": 440,
            "orbitAward": 0,
            "highAtmosphereAward": 520,
            "lowAtmosphereAward": 600,
            "landedAward": 0
        },
```

### PodProbeDistinction

Determines what parts can hold crew and what parts are probes.

```json
{
    "modVersion": "0.0.1", // Throws warning to log if this doesn't match the swinfo.json version number
    "crewed": [
        "cockpit_1v_m1_crew", // List of part IDs that correspond to crewed command modules. (dispute: should this include crew cabins?)
    ],
    "uncrewed": [
        "probe_0v_hexagonal_electricity", // List of all probes (any part that can control a craft without crew)
```

### Saves

Save data that the mod uses.

### Tech Tree

All tech tree nodes are stored in here. To create a new node(s), create a new file following the following structure. Any part that isn't listed under any tech tree node is permanently locked, so if you're making a part mod don't forget to add or modify a tech tree node that has your part listed!

```json
{
    "modVersion": "0.0.1", // Throws warning to log if this doesn't match the swinfo.json version number
    "nodes": [
        {
            "nodeID": "Start", // Display name of the node, as well as the internal name used.
            "dependencies": [], // List of strings of nodeIDs that need to be unlocked before this node is unlockable.
            "requiresAll": true, // Determines if every dependency is required to make this node unlockable, or if only one is needed.
            "posx": 10, // x-position where the node is drawn.
            "posy": 300, // y-position where the node is drawn.
            "parts": [ "fin_0v_procedural", "pod_1v_conical_crew", "booster_1v_solid_flea", "parachute_0v" ], // List of part IDs of parts that this node unlocks.
            "cost": 0, // Tech point cost of the node.
            "unlockedInitially": true // Determines if this node is unlocked at the start of the save.
        },
```

If you wish to add parts to an existing node rather than create a new one, in a similarly structured .json file in the directory, make `nodeID` the same as the existing node, and delete all of the data in the new node except for `nodeID` and `parts`.
