using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;
using System;
using System.IO;
using System.Reflection;
using static CharacterAnimEvent;
using YamlDotNet.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Object = UnityEngine.Object;
using Jotunn.Managers;
using Jotunn.Utils;
using YamlDotNet.Serialization;
using UnityEngine.XR;
using System.Diagnostics;
using Jotunn.Entities;
using System.Runtime.CompilerServices;


namespace WackySpawners
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class WackySpawner : BaseUnityPlugin
    {
        internal const string ModName = "WackySpawners";
        internal const string ModVersion = "1.1.1";
        internal const string Author = "WackyMole";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = BepInEx.Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        internal static YMLSpawnLoader spawnClass;
        internal static string assetPath;
        internal static string assetPathWacky;
        public static bool hasAwake = false;

        public static ConfigEntry<bool> IsSinglePlayer;
        public static string OldFile = BepInEx.Paths.ConfigPath + @"/Detalhes.CustomSpawners.json"; // old file to look for
        public static string WackyFile = BepInEx.Paths.ConfigPath + @"/WackyMole.CustomSpawners.yml";
        public static string WackyYML= "WackyMole.CustomSpawners.yml";

        internal static bool playerspawned = false;
        //internal static List<Spawner>  currentpieces = null;
        internal static WackySpawns ymlspawn = null;


        public static readonly ManualLogSource Logg =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        internal static readonly CustomSyncedValue<string> spawnerInfo = new(ConfigSync, "wackySpawner", ""); // doesn't show up in config

        public static Dictionary<string,int> Multispawn = new();

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {

            assetPathWacky = Path.Combine(BepInEx.Paths.ConfigPath, WackyYML);
            spawnClass = new YMLSpawnLoader();

            string[] customConfigs = Directory.GetFiles(BepInEx.Paths.ConfigPath, "*.yml").Where(f => Path.GetFileName(f).StartsWith("WackyMole.CustomSpawners", StringComparison.OrdinalIgnoreCase)).ToArray();

            if (customConfigs.Length > 0) {
                ymlspawn = spawnClass.GetSpawnAreaConfigs();

            } else if (File.Exists(OldFile) )
            {
                Logg.LogWarning("Converting Detalhes.CustomSpawners.json in WackyMole.CustomSpawners.yml, Will NOT Delete");
                ymlspawn = spawnClass.ConvertOldtoNew();
            }
            else
            {
                Logg.LogWarning("Creating Example File WackyMole.CustomSpawners.yml, please edit to your liking");

                var paul = ReadEmbeddedFileBytes("WackyMole.CustomSpawners.yml");
                File.WriteAllBytes(assetPathWacky, paul);
                var pete = File.ReadAllText(assetPathWacky);

                var deslizer = new DeserializerBuilder().Build();
                WackySpawns pieces = deslizer.Deserialize<WackySpawns>(pete);
                ymlspawn = pieces;

            }
            
           
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            // Globally turn off configuration options for your pieces, omit if you don't want to do this.

            spawnerInfo.ValueChanged += CustomSpawnerSync;

            SynchronizationManager.OnAdminStatusChanged += CustomSpawnerSync;

           // PrefabManager.OnVanillaPrefabsAvailable += LoadPiecesNow;

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void CustomSpawnerSync()
        {
            if (ZNet.instance.IsServer() && ZNet.instance.IsDedicated())
            {
                 bool isDedServer = true;
            }

            if(ConfigSync.IsSourceOfTruth) return;  // no need to get data if singleplayer

            Logg.LogInfo("Wacky.Spawners, Sync, reloading");

            if (hasAwake && spawnerInfo.Value != "")
            {
                var copystring = spawnerInfo.Value;
                ymlspawn = spawnClass.GetSpawnAreaConfigs(copystring);                
                CreateandUpdateSpawnConfigs(ymlspawn.spawners);// CustomSync
                
            }else if (spawnerInfo.Value != "")
            {
                Logg.LogInfo("Has not passed Dung Start");
                // Only gets called if DungonDB Start hasn't been called yet
                var copystring = spawnerInfo.Value;
                ymlspawn = spawnClass.GetSpawnAreaConfigs(copystring);
              //  CreateandUpdateSpawnConfigs(ymlspawn.spawners);// CustomSync
            } else if (spawnerInfo.Value == "")
            {
                Logg.LogWarning("Spawners, Sync was empty, check server yml");
            }
        }

        private byte[] ReadEmbeddedFileBytes(string name)
        {
            using MemoryStream stream = new();
            Assembly.GetExecutingAssembly().GetManifestResourceStream(ModName + "." + name)?.CopyTo(stream);
            return stream.ToArray();
        }


        
        [HarmonyPatch(typeof(Game), "Logout")]
        public static class LogoutCheckWAc
        {
            private static void Postfix()
            {
                hasAwake = false;
            }
        }        
        /*
        [HarmonyPatch(typeof(Player), "OnSpawned")]
        public static class PlayerLateLateReload
        {
            private static void Postfix()
            {
                if (ZNet.instance.IsServer())
                {
                    CreateandUpdateSpawnConfigs(ymlspawn.spawners); // dumb dumb

                }
            }
        }*/

        [HarmonyPatch(typeof(ZNet), "Start")]
        public static class ServerloadWacSpawn
        {
            private static void Prefix()
            {
                if (ZNet.instance.IsServer())
                {
                    CreateandUpdateSpawnConfigs(ymlspawn.spawners); // On Start
                    var serializer = new SerializerBuilder()
                       // .WithNewLine("\n")
                         .Build();
                    spawnerInfo.Value = serializer.Serialize(ymlspawn);
                }
            }
        }        
        



        [HarmonyPatch(typeof(DungeonDB), "Start")]
        public static class OnSpawnedCheckSpawnerWac
        {
            private static void Postfix()
            {         
                hasAwake = true;
            }
        }

        
        [HarmonyPatch(typeof(SpawnArea), "UpdateSpawn")]
        public class UpdateSpawnView
        {
            public static bool Prefix(SpawnArea __instance) => __instance.m_nview;
        }

        /*
        [HarmonyPatch(typeof(SpawnArea), "IsSpawnPrefab")]  // This was causing regular vanilla spawners to go out of control for some reason in 218.21
        public class FixErrorSometimes
        {
            public static bool Prefix(GameObject go)
            {
                if (go  == null ) return false;
                if (go.GetComponent<SpawnArea>() == null ) return false;

                return true;
            }          
        }
        */
        
        [HarmonyPatch(typeof(SpawnArea), "SpawnOne")]
        public class UpdateSpawnAmount
        {
            public static void Postfix(SpawnArea __instance, bool __result)
            {
                if (__result)
                {
                   // Logg.LogWarning("Name " + __instance.name);
                    if (Multispawn.ContainsKey(__instance.name))
                    {
                        
                        int multi = Multispawn[__instance.name];
                        if (multi == 1) return;
                        int minus = multi - 1;

                        Logg.LogInfo("MultiSpawn "+ minus + " of" + __instance.name );

                        __instance.GetInstances(out var near, out var total);
                        if (near+minus >= __instance.m_maxNear || total+minus >= __instance.m_maxTotal)
                        {
                            return;
                        }


                        for (int j = 0; j < minus; j++)
                        {

                            SpawnArea.SpawnData spawnData = __instance.SelectWeightedPrefab();
                            if (spawnData == null)
                            {
                                return;
                            }

                            if (!__instance.FindSpawnPoint(spawnData.m_prefab, out var point))
                            {
                                return;
                            }

                            GameObject gameObject = UnityEngine.Object.Instantiate(spawnData.m_prefab, point, Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f));
                            if (__instance.m_setPatrolSpawnPoint)
                            {
                                BaseAI component = gameObject.GetComponent<BaseAI>();
                                if (component != null)
                                {
                                    component.SetPatrolPoint();
                                }
                            }

                            Character component2 = gameObject.GetComponent<Character>();
                            if (spawnData.m_maxLevel > 1)
                            {
                                int i;
                                for (i = spawnData.m_minLevel; i < spawnData.m_maxLevel; i++)
                                {
                                    if (!(UnityEngine.Random.Range(0f, 100f) <= __instance.GetLevelUpChance()))
                                    {
                                        break;
                                    }
                                }

                                if (i > 1)
                                {
                                    component2.SetLevel(i);
                                }
                            }

                            Vector3 centerPoint = component2.GetCenterPoint();
                            __instance.m_spawnEffects.Create(centerPoint, Quaternion.identity);

                        }
                        
                    }
                }
            }
        }

        public static void CreateandUpdateSpawnConfigs(List<Spawner> list)
        {
           
            if (list == null || list.Count == 0)
            {
                Logg.LogWarning("list was empty for spawners");
                return;
            }

            var hammer = ObjectDB.instance.m_items.FirstOrDefault(x => x.name == "Hammer");
            if (!hammer)
            {
                Logg.LogError("Custom Spawners - Hammer could not be loaded"); return;
            }


            PieceTable table = hammer.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces;

            foreach (Spawner areaConfig in list)
            {
                string newName = "CS_" + string.Join("_", areaConfig.name);
                bool skipcreation = false;
                GameObject hold = null;

                if (PrefabManager.Instance.GetPrefab(newName) != null)
               // if (table.m_pieces.Exists(x => x.name == newName))
                {
                    skipcreation = true ;
                }
                if (!skipcreation)
                {
                    GameObject customSpawner = PrefabManager.Instance.CreateClonedPrefab(newName, areaConfig.prefabToCopy);
                    if (customSpawner == null)
                    {
                        Logg.LogError("original prefab not found for " + areaConfig.prefabToCopy);
                        continue;
                    }

                    customSpawner.GetComponent<ZNetView>().m_syncInitialScale = true;

                    SpawnArea area = customSpawner.AddComponent<SpawnArea>();
                    Piece piece = customSpawner.GetComponent<Piece>();
                    if (piece is null) piece = customSpawner.AddComponent<Piece>();

                    piece.m_description = areaConfig.name + " ";
                    piece.name = customSpawner.name;
                    hold = customSpawner;
                    foreach (var pi in piece.m_resources)
                    {
                        pi.m_recover = false;
                    }
                }

                // update section
                GameObject currentcustomSpawner;
                if (!skipcreation)
                     currentcustomSpawner = hold;
                else
                    currentcustomSpawner = PrefabManager.Instance.GetPrefab(newName);

                if (areaConfig.HitPoints > 0 )
                {
                    if (currentcustomSpawner.TryGetComponent<Destructible>(out Destructible de1)){
                        de1.m_health = areaConfig.HitPoints;
                    }else
                    {
                      var de2 = currentcustomSpawner.AddComponent<Destructible>();
                      de2.m_health = areaConfig.HitPoints;
                    }
                }
                else
                {
                    if (currentcustomSpawner.TryGetComponent<Destructible>(out Destructible de3))
                        UnityEngine.Object.Destroy(currentcustomSpawner.GetComponent<Destructible>());
                }
                


                if (currentcustomSpawner.TryGetComponent<WearNTear>(out WearNTear temp))
                    Object.Destroy(currentcustomSpawner.GetComponent<WearNTear>());
                temp = null;

                SpawnArea area2 = currentcustomSpawner.GetComponent<SpawnArea>();
                Piece piece2 = currentcustomSpawner.GetComponent<Piece>();
                // CircleProjector circle2 =  currentcustomSpawner.AddComponent<CircleProjector>();
                //circle2.m_prefab =

                area2.name = newName;
                if (areaConfig.multiSpawn != 0)
                {
                    if (Multispawn.ContainsKey(area2.name + "(Clone)"))
                        Multispawn[area2.name + "(Clone)"] = areaConfig.multiSpawn;
                    else
                        Multispawn.Add(area2.name + "(Clone)", areaConfig.multiSpawn);
                }

                area2.m_spawnTimer = areaConfig.m_spawnTimer;
                area2.m_onGroundOnly = areaConfig.m_onGroundOnly;
                area2.m_maxTotal = areaConfig.m_maxTotal;
                area2.m_maxNear = areaConfig.m_maxNear;
                area2.m_farRadius = areaConfig.m_farRadius;
                area2.m_spawnRadius = areaConfig.m_spawnRadius;
                area2.m_setPatrolSpawnPoint = areaConfig.m_setPatrolSpawnPoint;
                area2.m_triggerDistance = areaConfig.m_triggerDistance;
                area2.m_spawnIntervalSec = areaConfig.m_spawnIntervalSec;
                area2.m_levelupChance = areaConfig.m_levelupChance;
                area2.m_nearRadius = areaConfig.m_nearRadius;
                area2.m_prefabs = new List<SpawnArea.SpawnData>();
                piece2.m_description = areaConfig.name + " ";
                piece2.name = currentcustomSpawner.name;
               

                piece2.m_randomTarget = areaConfig.mobTarget;
                piece2.m_primaryTarget = areaConfig.mobTarget;
                piece2.m_targetNonPlayerBuilt = areaConfig.mobTarget;


                

                foreach (string prefab in areaConfig.m_prefabName.Split(','))
                {
                    var newArea = new SpawnArea.SpawnData();
                    newArea.m_weight = 100 / areaConfig.m_prefabName.Split(',').Count();
                    newArea.m_minLevel = areaConfig.minLevel;
                    newArea.m_maxLevel = areaConfig.maxLevel;
                    newArea.m_prefab = PrefabManager.Instance.GetPrefab(prefab);
                    piece2.m_description += prefab + " ";
                    if (newArea.m_prefab == null) continue;

                    area2.m_prefabs.Add(newArea);
                }

                if (ZNet.instance.IsServer() && ZNet.instance.IsDedicated())
                    continue;

                if (!skipcreation)
                {
                    //Jotunn.Managers.PieceManager.Instance.RegisterPieceInPieceTable(currentcustomSpawner, "_HammerPieceTable", "Custom Spawners");
                    //PieceManager.Instance.RegisterPieceInPieceTable(currentcustomSpawner, "_HammerPieceTable", "Custom Spawners");
                }

                try
                {
                    Jotunn.Managers.PieceManager.Instance.RegisterPieceInPieceTable(currentcustomSpawner, "_HammerPieceTable", "Custom Spawners"); // I started having problems with this all of a sudden
                }
                catch (Exception e) { Logg.LogWarning("Failed to find piecehammer, will try again"); continue; }


                if (!SynchronizationManager.Instance.PlayerIsAdmin)
                {
                   // UnityEngine.Debug.LogWarning("Player is not admin Remove Piece" + currentcustomSpawner.name);
                    table.m_pieces.Remove(currentcustomSpawner);
                    // table.m_pieces.find
                    //table.m_pieces.fin(currentcustomSpawner).SetActive(false);
                }
                else
                {

                    // UnityEngine.Debug.LogWarning("Player is admin, Leave piece " + currentcustomSpawner.name);
                    if (table.m_pieces.Exists(x => x.name == currentcustomSpawner.name))
                    {
                        // good
                    }else
                    {
                        table.m_pieces.Add(currentcustomSpawner);
                    }
                }
            }
        }
       public class MultiSpawn : MonoBehaviour
        {

        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(BepInEx.Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = false;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;

            FileSystemWatcher watcher2 = new(BepInEx.Paths.ConfigPath, "*.yml");
            watcher2.Changed += ReadSpawnerValues;
            watcher2.Created += ReadSpawnerValues;
            watcher2.Renamed += ReadSpawnerValues;
            watcher2.IncludeSubdirectories = false;
            watcher2.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher2.EnableRaisingEvents = true;
        }
        private void ReadSpawnerValues (object sender, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);
            if (!fileName.StartsWith("WackyMole.CustomSpawners", StringComparison.OrdinalIgnoreCase)) return;

            ZNet zNet = ZNet.instance;
            if (zNet == null) return;
            if (zNet.IsServer())
            {
                Logger.LogInfo("Spawners file changed, reloading");

                ymlspawn = spawnClass.GetSpawnAreaConfigs();
                var serializer = new SerializerBuilder()
                .WithNewLine("\n")
                    .Build();

                spawnerInfo.Value = serializer.Serialize(ymlspawn);

                
                if (ConfigSync.IsSourceOfTruth)
                    CreateandUpdateSpawnConfigs(ymlspawn.spawners); // On update singleplayer or host
            }

        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                Logg.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                Logg.LogError($"There was an issue loading your {ConfigFileName}");
                Logg.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        #endregion

    }


}