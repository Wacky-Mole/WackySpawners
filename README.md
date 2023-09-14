# Wacky Spawners


This is a replacement for Custom Spawners: [https://valheim.thunderstore.io/package/Detalhes/CustomSpawners/](https://valheim.thunderstore.io/package/Detalhes/CustomSpawners/)

While Custom Spawners still works, it lacks documentation and examples.


#### Dynamic Monster Spawning: The mod enables the creation of monster spawners that generate monsters based on configurable parameters.
![Custom Spawners](https://wackymole.com/hosts/spawnerUI.png)
## Features:

- Custom monster spawner for specific places in Dungeons/Temples, etc.
- Attaches spawner to ordinary objects, so they don't look out of place.
- 0 health means invincible.
- Highly customizable.

All created spawners will appear in the hammer; only admins can build them.

## New Features:

- Drop-in replacement converts JSON to YAML.
- Replaced RPC calls with ServerSync.
- Added Filewatcher for YAML, live updates.
- Will probably drop JVL in the future.
- Default file will be created if none exist, with examples.
- Custom Spawners drop nothing when they are destroyed

### WackyMole.CustomSpawners.yml

Configuration Parameters

Live updates for new pieces, existing ones might not. 

- name (string): Name of the monster spawner. - (Can't change without reboot)
- prefabToCopy (string): Prefab to copy when creating monsters. (Can't change without reboot)
- m_spawnTimer (int): Just appears to be the internal counter: doesn't really do anything.
- m_onGroundOnly (bool): Set to true to spawn monsters only on the ground.
- m_maxTotal (int): Maximum total spawned monsters.
- m_maxNear (int): Maximum monsters in the NEAR proximity of the spawner.
- m_farRadius (int): What determintes a FAR Radius.
- m_spawnRadius (int): Radius within which monsters can spawn.
- m_setPatrolSpawnPoint (bool): Set to true to enable patrol spawn points for monsters. 
- m_triggerDistance (int): Distance at which players trigger monster spawns.
- m_spawnIntervalSec (int): Time interval between monster spawns (in seconds).
- m_levelupChance (int): Chance for monsters to level up when spawned.
- m_prefabName (string): Name of the spawned monster prefab.
- m_nearRadius (int): What determines a NEAR Radius
- minLevel (int): Minimum level for spawned monsters.
- maxLevel (int): Maximum level for spawned monsters.
- HitPoints (int): Hit points for spawned piece. A 0 is infinite, 400 is the standard health of a portal.


### Credits:
Detalhes and all his mods https://valheim.thunderstore.io/package/Detalhes/

Azumatt and his template.

JVL Team

For questions or suggestions please join discord channel: [Odin Plus Team](https://discord.gg/odinplus) or my discord at [Wolf Den](https://discord.gg/yPj7xjs3Xf)

Support me at https://www.buymeacoffee.com/WackyMole  or https://ko-fi.com/wackymole

