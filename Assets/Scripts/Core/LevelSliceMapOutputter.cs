using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class LevelSliceMapOutputter
{

    [System.Serializable]
    public struct LevelSliceMapAnalyticsObject
    {
        public string name;
        public string data;
    }

    [System.Serializable]
    struct LevelSliceMapHeader
    {
        public BeatmapStructure mapMetadata;
        public List<BeatCutData> leftHandCutData;
        public List<BeatCutData> rightHandCutData;
    }

    Dictionary<System.Guid, ISliceMapAnalyser> _analysers;

    public LevelSliceMapOutputter(LevelLoader levelLoader)
    {
        _analysers = new Dictionary<System.Guid, ISliceMapAnalyser>();

        levelLoader.OnLevelLoaded += LevelLoader_OnLevelLoaded;
    }

    public System.Guid RegisterAnalyser(ISliceMapAnalyser inAnalyser)
    {
        System.Guid analyserID = new System.Guid();
        _analysers.Add(analyserID, inAnalyser);
        return analyserID;
    }

    public bool UnregisterAnalyser(System.Guid inAnalyserID)
    {
        return _analysers.Remove(inAnalyserID);
    }

    private void LevelLoader_OnLevelLoaded(AudioClip levelAudio, BeatmapData beatmapData)
    {
        SliceMap rightHandSliceMap = new SliceMap(beatmapData.Metadata.bpm, beatmapData.BeatData.colorNotes.ToList<ColourNote>(), beatmapData.BeatData.bombNotes.ToList<BombNote>(), beatmapData.BeatData.obstacles.ToList<Obstacle>(), isRightHand: true);
        SliceMap leftHandSliceMap = new SliceMap(beatmapData.Metadata.bpm, beatmapData.BeatData.colorNotes.ToList<ColourNote>(), beatmapData.BeatData.bombNotes.ToList<BombNote>(), beatmapData.BeatData.obstacles.ToList<Obstacle>(), isRightHand: false);

        LevelSliceMapHeader header = new LevelSliceMapHeader();
        header.mapMetadata = beatmapData.Metadata;
        header.leftHandCutData = new List<BeatCutData>();
        header.rightHandCutData = new List<BeatCutData>();
        leftHandSliceMap.WriteBeatCutDataToList(header.leftHandCutData);
        rightHandSliceMap.WriteBeatCutDataToList(header.rightHandCutData);
        List<LevelSliceMapAnalyticsObject> analytics = new List<LevelSliceMapAnalyticsObject>();
        foreach (ISliceMapAnalyser analyser in _analysers.Values)
        {
            analyser.ProcessSliceMaps(leftHandSliceMap, rightHandSliceMap);

            LevelSliceMapAnalyticsObject analyserObject = new LevelSliceMapAnalyticsObject();
            analyserObject.name = analyser.GetAnalyticsName();
            analyserObject.data = analyser.GetAnalyticsData();
            analytics.Add(analyserObject);
        }

        string outputJson = "{";
        outputJson += JsonUtility.ToJson(header, prettyPrint: true);
        outputJson += ",";
        outputJson += "\n\"analytics\": [";

        int numAnalytics = analytics.Count;
        for (int i = 0; i < numAnalytics; ++i)
        {
            LevelSliceMapAnalyticsObject analyticsObject = analytics[i];
            outputJson += "\n{";
            outputJson += "\n\"name\": \"" + analyticsObject.name + "\",";
            outputJson += "\n\"data\":";
            outputJson += analyticsObject.data;
            outputJson += "\n}";
            if (i < numAnalytics-1)
            {
                outputJson += ",";
            }
        }

        outputJson += "\n]";
        outputJson += "\n}";

#if UNITY_EDITOR
        string path = Application.dataPath + "/Export/";
#else
        string path = Application.persistentDataPath + "/Export/";
#endif
        System.IO.Directory.CreateDirectory(path);
        System.IO.File.WriteAllText(path + beatmapData.Metadata.mapName + "_" + beatmapData.Metadata._difficultyRank.ToString() + "_" + beatmapData.Metadata._difficulty + "_analytics.json", outputJson);
    }
}
