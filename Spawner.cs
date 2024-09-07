using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;

namespace WackySpawners
{

        public class YMLSpawnLoader
        {

            public WackySpawns ConvertOldtoNew()
            {
                var serializer = new SerializerBuilder()
                .WithNewLine("\n")
                .Build();

                var deslizer = new DeserializerBuilder().Build();
                WackySpawns pieces =  deslizer.Deserialize<WackySpawns>(File.ReadAllText(WackySpawner.OldFile));
                File.WriteAllText(WackySpawner.WackyFile, serializer.Serialize(pieces));
                //WackySpawner.ymlspawn = pieces;
                return pieces;
            }


            public WackySpawns GetSpawnAreaConfigs()
                {
                var deslizer = new DeserializerBuilder().Build();
                WackySpawns pieces = deslizer.Deserialize<WackySpawns>(File.ReadAllText(WackySpawner.assetPathWacky));
                //WackySpawner.ymlspawn = pieces;
                return pieces;
                }

            public WackySpawns GetSpawnAreaConfigs(string yml)
                {

                    var deslizer = new DeserializerBuilder()
                             .IgnoreUnmatchedProperties() // future proofing
                             .Build(); // make sure to include all

                    var pieces = deslizer.Deserialize<WackySpawns>(yml);
                    return pieces;
           

            }
        }

        public class Spawner
        {
            public string name { get; set; }
            public string prefabToCopy { get; set; }
            public int m_spawnTimer { get; set; }
            public bool m_onGroundOnly { get; set; }
            public int m_maxTotal { get; set; }
            public int m_maxNear { get; set; }         
            public int m_spawnRadius { get; set; }
            public bool m_setPatrolSpawnPoint { get; set; }
            public int m_triggerDistance { get; set; }
            public int m_spawnIntervalSec { get; set; }
            public int m_levelupChance { get; set; }
            public string m_prefabName { get; set; }
            public int m_nearRadius { get; set; }
            public int m_farRadius { get; set; }
            public int minLevel { get; set; }
            public int maxLevel { get; set; }
            public int HitPoints { get; set; }
            public bool mobTarget { get; set; }
        }

        public class WackySpawns
        {
            public List<Spawner> spawners { get; set; }
        }

    
}
