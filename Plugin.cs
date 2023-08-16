using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using LocalizationManager;
using PieceManager;
using ServerSync;
using System;
using System.IO;
using System.Reflection;
using static CharacterAnimEvent;
using YamlDotNet.Core;
using PieceManager;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Object = UnityEngine.Object;
using Jotunn.Managers;
using Jotunn.Utils;
using YamlDotNet.Serialization;

namespace WackySpawners
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class WackySpawner : BaseUnityPlugin
    {
        internal const string ModName = "WackySpawners";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "{azumatt}";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = BepInEx.Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        internal static YMLSpawnLoader spawnClass;


        internal static readonly CustomSyncedValue<string> spawnerInfo = new(ConfigSync, "wackySpawner", ""); // doesn't show up in config
        public static ConfigEntry<bool> IsSinglePlayer;
        public static string OldFile = BepInEx.Paths.ConfigPath + @"/Detalhes.CustomSpawners.json"; // old file to look for
        public static string WackyFile = BepInEx.Paths.ConfigPath + @"/WackyMole.CustomSpawners.yml";

        internal static bool playerspawned = false;
        internal static List<Spawner>  currentpieces = null;


        public static readonly ManualLogSource PieceManagerModTemplateLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            // Uncomment the line below to use the LocalizationManager for localizing your mod.
            //Localizer.Load(); // Use this to initialize the LocalizationManager (for more information on LocalizationManager, see the LocalizationManager documentation https://github.com/blaxxun-boop/LocalizationManager#example-project).

            /*
            Config.SaveOnConfigSet = true;

            IsSinglePlayer = Config.Bind("Server config", "IsSinglePlayer", false,
                    new ConfigDescription("IsSinglePlayer", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SynchronizationManager.OnConfigurationSynchronized += (obj, attr) =>
            {
                if (attr.InitialSynchronization)
                {
                    Jotunn.Logger.LogMessage("Config sync event received");
                }
                else
                {
                    Jotunn.Logger.LogMessage("Config sync event received");
                }
            }; */
            if (File.Exists(WackyFile)) {
                currentpieces = spawnClass.GetSpawnAreaConfigs();

            } else if (File.Exists(OldFile) )
            {
                Logger.LogWarning("Converting Detalhes.CustomSpawners.json in WackyMole.CustomSpawners.yml, Will NOT Delete");
                currentpieces = spawnClass.ConvertOldtoNew();
            }
            else
            {
                Logger.LogWarning("Createing Example File WackyMole.CustomSpawners.yml, please edit to your liking");

                var paul = AssetUtils.LoadText("assets/WackyMole.CustomSpawners.yml");
                var deslizer = new DeserializerBuilder().Build();
                WackySpawns pieces = deslizer.Deserialize<WackySpawns>(paul);
                var serializer = new SerializerBuilder()
                    .WithNewLine("\n")
                    .Build();

                File.WriteAllText(WackySpawner.WackyFile, serializer.Serialize(pieces));
                currentpieces = pieces.spawners;    

            }
            //BuildPiece wacky = new BuildPiece("portal_wood", "portal_wood2", true);
           

            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            //ConfigSync.

            // Globally turn off configuration options for your pieces, omit if you don't want to do this.
            BuildPiece.ConfigurationEnabled = false;

            spawnerInfo.ValueChanged += CustomSpawnerSync;

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
            if (hasAwake)
            {
                spawnClass.GetSpawnAreaConfigs();

            }else
            {
                //wait
            }
        }

        public static bool hasAwake = false;
        [HarmonyPatch(typeof(Game), "Logout")]
        public static class LogoutCheck
        {
            private static void Postfix()
            {
                hasAwake = false;
            }
        }


        [HarmonyPatch(typeof(Player), "OnSpawned")]
        public static class OnSpawnedCheck
        {
            private static void Postfix()
            {
                if (hasAwake == true) return;
                hasAwake = true;

                if (ConfigSync.IsSourceOfTruth) CreateClonedPiece(currentpieces); // yml reader, 

            }
        }


        [HarmonyPatch(typeof(SpawnArea), "UpdateSpawn")]
        public class UpdateSpawn
        {
            public static bool Prefix(SpawnArea __instance) => __instance.m_nview;
        }


        public static void CreateClonedPiece(List<Spawner> list)
        {
            var hammer = ObjectDB.instance.m_items.FirstOrDefault(x => x.name == "Hammer");

            if (!hammer)
            {
                Debug.LogError("Custom Spawners - Hammer could not be loaded"); return;
            }

            PieceTable table = hammer.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces;

            foreach (Spawner areaConfig in list)
            {
                string newName = "CS_" + string.Join("_", areaConfig.name);

                if (table.m_pieces.Exists(x => x.name == newName))
                {
                    continue;
                }
                GameObject customSpawner = PrefabManager.Instance.CreateClonedPrefab(newName, areaConfig.prefabToCopy);
                if (customSpawner == null)
                {
                    Debug.LogError("original prefab not found for " + areaConfig.prefabToCopy);
                    continue;
                }

                customSpawner.GetComponent<ZNetView>().m_syncInitialScale = true;

                SpawnArea area = customSpawner.AddComponent<SpawnArea>();
                Piece piece = customSpawner.GetComponent<Piece>();
                if (piece is null) piece = customSpawner.AddComponent<Piece>();

                piece.m_description = areaConfig.name + " ";
                piece.name = customSpawner.name;

                if (areaConfig.HitPoints > 0)
                {
                    Destructible destructible = customSpawner.GetComponent<Destructible>();
                    if (destructible is null) destructible = customSpawner.AddComponent<Destructible>();
                    destructible.m_health = areaConfig.HitPoints;

                }
                else
                {
                    UnityEngine.Object.Destroy(customSpawner.GetComponent<Destructible>());
                }

                Object.Destroy(customSpawner.GetComponent<WearNTear>());

                area.m_spawnTimer = areaConfig.m_spawnTimer;
                area.m_onGroundOnly = areaConfig.m_onGroundOnly;
                area.m_maxTotal = areaConfig.m_maxTotal;
                area.m_maxNear = areaConfig.m_maxNear;
                area.m_farRadius = areaConfig.m_farRadius;
                area.m_spawnRadius = areaConfig.m_spawnRadius;
                area.m_setPatrolSpawnPoint = areaConfig.m_setPatrolSpawnPoint;
                area.m_triggerDistance = areaConfig.m_triggerDistance;
                area.m_spawnIntervalSec = areaConfig.m_spawnIntervalSec;
                area.m_levelupChance = areaConfig.m_levelupChance;
                area.m_nearRadius = areaConfig.m_nearRadius;
                area.m_prefabs = new List<SpawnArea.SpawnData>();

                foreach (string prefab in areaConfig.m_prefabName.Split(','))
                {
                    var newArea = new SpawnArea.SpawnData();
                    newArea.m_weight = 100 / areaConfig.m_prefabName.Split(',').Count();
                    newArea.m_minLevel = areaConfig.minLevel;
                    newArea.m_maxLevel = areaConfig.maxLevel;
                    newArea.m_prefab = PrefabManager.Instance.GetPrefab(prefab);
                    piece.m_description += prefab + " ";
                    if (newArea.m_prefab == null) continue;

                    area.m_prefabs.Add(newArea);
                }

                Jotunn.Managers.PieceManager.Instance.RegisterPieceInPieceTable(customSpawner, "Hammer", "Custom Spawners");

                if (!SynchronizationManager.Instance.PlayerIsAdmin)
                {
                    table.m_pieces.Remove(customSpawner);
                }
            }
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
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                PieceManagerModTemplateLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                PieceManagerModTemplateLogger.LogError($"There was an issue loading your {ConfigFileName}");
                PieceManagerModTemplateLogger.LogError("Please check your config entries for spelling and format!");
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