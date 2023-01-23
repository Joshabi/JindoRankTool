using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
public class LevelLoader : MonoBehaviour
{

    public delegate void LevelLoadEvent(AudioClip levelAudio, BeatmapData beatmapData);
    public event LevelLoadEvent OnLevelLoaded;

    public LevelStructure _loadedLevel;
    public List<BeatmapData> _loadedBeatmaps;
    private BeatmapDifficultyRank _desiredDifficulty = 0;

    public void LoadLevel(string levelFolder, BeatmapDifficultyRank desiredDiff)
    {
        _desiredDifficulty = desiredDiff;
        string infoDatFile = levelFolder + "/info.dat";

        Debug.Log("Attempting to load " + infoDatFile);

        string fileJson = File.ReadAllText(infoDatFile);

        Debug.Log("File: " + fileJson);

        if (fileJson.Length > 0)
        {
            _loadedLevel = JsonUtility.FromJson<LevelStructure>(fileJson);
            LevelDifficultyStructure[] diffs = _loadedLevel._difficultyBeatmapSets;
            foreach (LevelDifficultyStructure diff in diffs)
            {
                if (!diff._beatmapCharacteristicName.ToLower().Equals("lightshow") && !diff._beatmapCharacteristicName.ToLower().Equals("360degree") && !diff._beatmapCharacteristicName.ToLower().Equals("90degree"))
                {
                    foreach (BeatmapStructure difficulty in diff._difficultyBeatmaps)
                    {
                        string mapFilePath = levelFolder + "/" + difficulty._beatmapFilename;
                        string mapFileJson = File.ReadAllText(mapFilePath);
                        BeatmapFileStructureV3 beatDataV3 = JsonUtility.FromJson<BeatmapFileStructureV3>(mapFileJson);
                        if (beatDataV3.version == null || beatDataV3.version.Length == 0)
                        {
                            BeatmapFileStructureV2 beatDataV2 = JsonUtility.FromJson<BeatmapFileStructureV2>(mapFileJson);
                            beatDataV3 = BeatmapFileStructure.ConvertV2ToV3(beatDataV2);
                        }
                        if (beatDataV3.version == null || beatDataV3.version.Length == 0)
                        {
                            beatDataV3 = new BeatmapFileStructureV3();
                            Debug.LogError("Tried to load \"" + mapFilePath + "\" as V3 and as V2 map, both failed to parse to JSON.");
                        }
                        BeatmapData beatmap = new BeatmapData();
                        beatmap.Metadata = difficulty;
                        beatmap.Metadata.bpm = _loadedLevel._beatsPerMinute;
                        beatmap.Metadata.mapName = _loadedLevel._songName;
                        beatmap.BeatData = beatDataV3;

                        _loadedBeatmaps.Add(beatmap);
                    }
                }
            }

            string songFilePath = "file:///" + levelFolder + "/" + _loadedLevel._songFilename;
            StartCoroutine(GetAudioFile(songFilePath));
        }
    }

    IEnumerator GetAudioFile(string filePath)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.OGGVORBIS))
        {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogError(www.error);
            }
            else
            {
                OnAudioLoaded(DownloadHandlerAudioClip.GetContent(www));
            }
        }
    }

    void OnAudioLoaded(AudioClip audio)
    {
        if (OnLevelLoaded != null)
        {
            bool levelFound = false;
            foreach (BeatmapData beatmap in _loadedBeatmaps)
            {
                if (beatmap.Metadata._difficultyRank == _desiredDifficulty)
                {
                    OnLevelLoaded(audio, beatmap);
                    levelFound = true;
                    break;
                }
            }
            if (!levelFound && _loadedBeatmaps.Count > 0)
            {
                OnLevelLoaded(audio, _loadedBeatmaps[0]);
            }
        }
        else
        {
            Debug.LogError("Nothing to broadcast to.");
        }
    }

}
