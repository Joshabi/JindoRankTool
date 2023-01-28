using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapDatabase
{

    internal struct MapData
    {
        public string mapFolder;
        public string hash;
        public float bpm;
        public float duration;
    }

    private Dictionary<MapId, MapData> _database;
    private Dictionary<string, MapId> _hashLookup;
    private Dictionary<string, MapId> _folderLookup;

    public MapDatabase()
    {
        _database = new Dictionary<MapId, MapData>();
        _hashLookup = new Dictionary<string, MapId>();
        _folderLookup = new Dictionary<string, MapId>();
    }

    public MapId GetMapIdFromFolderPath(string folderPath)
    {
        if (!_folderLookup.ContainsKey(folderPath))
        {
            Debug.LogError("No map ID registered for folder \"" + folderPath + "\".");
            return MapId.InvalidMapId;
        }

        return _folderLookup[folderPath];
    }

    public MapId HashToMapId(string hash)
    {
        if (!_hashLookup.ContainsKey(hash))
        {
            Debug.LogError("No map ID registered for hash \"" + hash + "\".");
            return MapId.InvalidMapId;
        }

        return _hashLookup[hash];
    }

    public void SetMapHash(MapId id, string hash)
    {
        MapData data = FindOrAddMapData(id);
        data.hash = hash;
        _database[id] = data;
        if (!_hashLookup.ContainsKey(hash))
        {
            _hashLookup.Add(hash, id);
        }
    }

    public void SetMapFolder(MapId id, string mapFolder)
    {
        MapData data = FindOrAddMapData(id);
        data.mapFolder = mapFolder;
        _database[id] = data;
        if (!_folderLookup.ContainsKey(mapFolder))
        {
            _folderLookup.Add(mapFolder, id);
        }
    }

    public void SetMapBPM(MapId id, float bpm)
    {
        MapData data = FindOrAddMapData(id);
        data.bpm = bpm;
        _database[id] = data;
    }

    public void SetMapDuration(MapId id, float duration)
    {
        MapData data = FindOrAddMapData(id);
        data.duration = duration;
        _database[id] = data;
    }

    public string GetMapHash(MapId id)
    {
        return FindOrAddMapData(id).hash;
    }

    public float GetMapBPM(MapId id)
    {
        return FindOrAddMapData(id).bpm;
    }

    public float GetMapDuration(MapId id)
    {
        return FindOrAddMapData(id).duration;
    }

    private MapData FindOrAddMapData(MapId id)
    {
        if (!_database.ContainsKey(id))
        {
            _database.Add(id, new MapData());
        }
        return _database[id];
    }

}
