﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NosCore.Core;
using NosCore.Core.Networking;
using NosCore.Data;
using NosCore.Data.StaticEntities;
using NosCore.Data.WebApi;
using NosCore.DAL;
using NosCore.Shared.Enumerations.Map;
using NosCore.Shared.I18N;
using Mapster;

namespace NosCore.GameObject.Networking
{
    public sealed class ServerManager : BroadcastableBase
    {
        private static ServerManager _instance;

        private long _lastGroupId = 1;

        private static readonly ConcurrentDictionary<Guid, MapInstance> Mapinstances =
            new ConcurrentDictionary<Guid, MapInstance>();

        private static readonly List<Map.Map> Maps = new List<Map.Map>();

        private ServerManager()
        {
        }
        private static int _seed = Environment.TickCount;
        private static readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref _seed)));
        public int RandomNumber(int min = 0, int max = 100)
        {
            return Random.Value.Next(min, max);
        }
        public static ServerManager Instance => _instance ?? (_instance = new ServerManager());

        public List<NpcMonsterDTO> NpcMonsters { get; set; }
        public List<Item.Item> Items { get; set; }

        public ConcurrentDictionary<long, Group> Groups { get; set; }

        public MapInstance GenerateMapInstance(short mapId, MapInstanceType type)
        {
            var map = Maps.Find(m => m.MapId.Equals(mapId));
            if (map == null)
            {
                return null;
            }

            var guid = Guid.NewGuid();
            var mapInstance = new MapInstance(map, guid, false, type);
            mapInstance.LoadPortals();
            mapInstance.LoadMonsters();
            mapInstance.LoadNpcs();
            mapInstance.StartLife();
            Mapinstances.TryAdd(guid, mapInstance);
            return mapInstance;
        }

        private void LaunchEvents()
        {
            Observable.Interval(TimeSpan.FromMinutes(5)).Subscribe(x => { SaveAll(); });
        }

        public long GetNextGroupId()
        {
            _lastGroupId++;
            return _lastGroupId;
        }

        public void Initialize()
        {
            Groups = new ConcurrentDictionary<long, Group>();
            // parse rates
            try
            {
                var i = 0;
                var monstercount = 0;
                var npccount = 0;
                OrderablePartitioner<ItemDTO> itemPartitioner = Partitioner.Create(DAOFactory.ItemDAO.LoadAll(), EnumerablePartitionerOptions.NoBuffering);
                Items = DAOFactory.ItemDAO.LoadAll().Adapt<List<Item.Item>>();
                Logger.Log.Info(string.Format(LogLanguage.Instance.GetMessageFromKey(LanguageKey.ITEMS_LOADED), Items.Count));
                NpcMonsters = DAOFactory.NpcMonsterDAO.LoadAll().ToList();
                var mapPartitioner = Partitioner.Create(DAOFactory.MapDAO.LoadAll().Adapt<List<Map.Map>>(),
                    EnumerablePartitionerOptions.NoBuffering);
                var mapList = new ConcurrentDictionary<short, Map.Map>();
                Parallel.ForEach(mapPartitioner, new ParallelOptions { MaxDegreeOfParallelism = 8 }, map =>
                  {
                      var guid = Guid.NewGuid();
                      map.Initialize();
                      mapList[map.MapId] = map;
                      var newMap = new MapInstance(map, guid, map.ShopAllowed, MapInstanceType.BaseMapInstance);
                      Mapinstances.TryAdd(guid, newMap);
                      newMap.LoadPortals();
                      newMap.LoadMonsters();
                      newMap.LoadNpcs();
                      newMap.StartLife();
                      monstercount += newMap.Monsters.Count;
                      npccount += newMap.Npcs.Count;
                      i++;
                  });
                Maps.AddRange(mapList.Select(s => s.Value));
                if (i != 0)
                {
                    Logger.Log.Info(string.Format(LogLanguage.Instance.GetMessageFromKey(LanguageKey.MAPS_LOADED), i));
                }
                else
                {
                    Logger.Log.Error(LogLanguage.Instance.GetMessageFromKey(LanguageKey.NO_MAP));
                }
                Logger.Log.Info(string.Format(LogLanguage.Instance.GetMessageFromKey(LanguageKey.MAPNPCS_LOADED),
                    npccount));
                Logger.Log.Info(string.Format(LogLanguage.Instance.GetMessageFromKey(LanguageKey.MAPMONSTERS_LOADED),
                    monstercount));
                LaunchEvents();
            }
            catch (Exception ex)
            {
                Logger.Log.Error("General Error", ex);
            }
        }

        public void SaveAll()
        {
            try
            {
                Logger.Log.Info(LogLanguage.Instance.GetMessageFromKey(LanguageKey.SAVING_ALL));
                Parallel.ForEach(Sessions.Values.Where(s => s.Character != null), session =>
                {
                    session.Character.Save();
                });
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public Guid GetBaseMapInstanceIdByMapId(short mapId)
        {
            return Mapinstances.FirstOrDefault(s =>
                s.Value?.Map.MapId == mapId && s.Value.MapInstanceType == MapInstanceType.BaseMapInstance).Key;
        }

        public MapInstance GetMapInstance(Guid id)
        {
            return Mapinstances.ContainsKey(id) ? Mapinstances[id] : null;
        }

        public void BroadcastPacket(PostedPacket postedPacket, int? channelId = null)
        {
            if (channelId == null)
            {
                foreach (var channel in WebApiAccess.Instance.Get<List<WorldServerInfo>>("api/channels"))
                {
                    WebApiAccess.Instance.Post<PostedPacket>("api/packet", postedPacket, channel.WebApi);
                }
            }
            else
            {
                var channel = WebApiAccess.Instance.Get<List<WorldServerInfo>>("api/channels", id: channelId.Value).FirstOrDefault();
                if (channel != null)
                {
                    WebApiAccess.Instance.Post<PostedPacket>("api/packet", postedPacket, channel.WebApi);
                }
            }
        }

        public void BroadcastPackets(List<PostedPacket> packets, int? channelId = null)
        {
            foreach (var packet in packets)
            {
                BroadcastPacket(packet, channelId);
            }
        }
    }
}