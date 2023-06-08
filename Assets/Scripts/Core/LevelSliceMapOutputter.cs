using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using JoshaParity;

public class LevelSliceMapOutputter
{

    [System.Serializable]
    public struct LevelSliceMapAnalyticsObject
    {
        public string name;
        public string description;
        public string data;
    }

    [System.Serializable]
    struct LevelSliceMapHeader
    {
        public BeatmapStructure mapMetadata;
    }

    private Dictionary<System.Guid, ISliceMapAnalyser> _analysers;
    private MapDatabase _mapDatabase;

    public LevelSliceMapOutputter(MapDatabase inMapDatabase)
    {
        _analysers = new Dictionary<System.Guid, ISliceMapAnalyser>();
        _mapDatabase = inMapDatabase;
    }

    public System.Guid RegisterAnalyser(ISliceMapAnalyser inAnalyser)
    {
        System.Guid analyserID = System.Guid.NewGuid();
        _analysers.Add(analyserID, inAnalyser);
        return analyserID;
    }

    public bool UnregisterAnalyser(System.Guid inAnalyserID)
    {
        return _analysers.Remove(inAnalyserID);
    }

    public void ProcessBeatmap(BeatmapData beatmapData, MapAnalyser mapAnalyser)
    {
        float bpm = _mapDatabase.GetMapBPM(beatmapData.Metadata.id);

        List<SwingData> swingData = mapAnalyser.GetSwingData(beatmapData.Metadata._difficultyRank);
        List<SwingData> rightHandSwingData = swingData.FindAll(x => x.rightHand);
        List<SwingData> leftHandSwingData = swingData.FindAll(x => !x.rightHand);

        LevelSliceMapHeader header = new LevelSliceMapHeader();
        header.mapMetadata = beatmapData.Metadata;
        List<LevelSliceMapAnalyticsObject> analytics = new List<LevelSliceMapAnalyticsObject>();
        foreach (ISliceMapAnalyser analyser in _analysers.Values)
        {
            analyser.ProcessSwingData(_mapDatabase, beatmapData.Metadata, leftHandSwingData, rightHandSwingData);

            LevelSliceMapAnalyticsObject analyserObject = new LevelSliceMapAnalyticsObject();
            analyserObject.name = analyser.GetAnalyticsName();
            analyserObject.description = analyser.GetAnalyticsDescription();
            analyserObject.data = analyser.GetAnalyticsData();
            analytics.Add(analyserObject);
        }

        string outputJson = "{";

        string headerStr = JsonUtility.ToJson(header, prettyPrint: true);
        Debug.Log("headerStr = " + headerStr);
        headerStr = headerStr.Remove(0, 1);
        headerStr = headerStr.Remove(headerStr.Length - 1);

        outputJson += headerStr;
        outputJson += ",";
        outputJson += "\n\"analytics\": [";

        int numAnalytics = analytics.Count;
        for (int i = 0; i < numAnalytics; ++i)
        {
            LevelSliceMapAnalyticsObject analyticsObject = analytics[i];
            outputJson += "\n{";
            outputJson += "\n\"name\": \"" + analyticsObject.name + "\",";
            outputJson += "\n\"description\": \"" + analyticsObject.description + "\",";
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
        string fileName = beatmapData.Metadata.mapName + "_" + beatmapData.Metadata._difficultyRank.ToString() + "_" + beatmapData.Metadata._difficulty + "_analytics.json";
        System.IO.File.WriteAllText(path + fileName, outputJson);
        Debug.Log("Writing \"" + fileName + "\".");
    }
}
