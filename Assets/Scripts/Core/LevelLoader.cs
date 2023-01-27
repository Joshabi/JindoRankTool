using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
public class LevelLoader
{

    public delegate void LevelLoadEvent(BeatmapData beatmapData);

    public void LoadLevel(string levelFolder, LevelLoadEvent onLevelLoadCallback)
    {
        string infoDatFile = levelFolder + "/info.dat";
        string fileJson = File.ReadAllText(infoDatFile);

        if (fileJson.Length > 0)
        {
            LevelStructure loadedLevel = JsonUtility.FromJson<LevelStructure>(fileJson);
            LevelDifficultyStructure[] diffs = loadedLevel._difficultyBeatmapSets;
            foreach (LevelDifficultyStructure diff in diffs)
            {
                if (!diff._beatmapCharacteristicName.ToLower().Equals("lightshow") && !diff._beatmapCharacteristicName.ToLower().Equals("360degree") && !diff._beatmapCharacteristicName.ToLower().Equals("90degree"))
                {
                    foreach (BeatmapStructure difficulty in diff._difficultyBeatmaps)
                    {
                        LoadDifficulty(levelFolder, difficulty, loadedLevel, onLevelLoadCallback);
                    }
                }
            }
        }
    }

    public void LoadLevel(string levelFolder, BeatmapDifficultyRank desiredDifficulty, LevelLoadEvent onLevelLoadCallback)
    {
        string infoDatFile = levelFolder + "/info.dat";
        string fileJson = File.ReadAllText(infoDatFile);

        if (fileJson.Length > 0)
        {
            LevelStructure loadedLevel = JsonUtility.FromJson<LevelStructure>(fileJson);
            LevelDifficultyStructure[] diffs = loadedLevel._difficultyBeatmapSets;
            foreach (LevelDifficultyStructure diff in diffs)
            {
                if (!diff._beatmapCharacteristicName.ToLower().Equals("lightshow") && !diff._beatmapCharacteristicName.ToLower().Equals("360degree") && !diff._beatmapCharacteristicName.ToLower().Equals("90degree"))
                {
                    foreach (BeatmapStructure difficulty in diff._difficultyBeatmaps)
                    {
                        if (difficulty._difficultyRank == desiredDifficulty)
                        {
                            LoadDifficulty(levelFolder, difficulty, loadedLevel, onLevelLoadCallback);
                        }
                    }
                }
            }
        }
    }

    private void LoadDifficulty(string levelFolder, BeatmapStructure difficulty, LevelStructure loadedLevel, LevelLoadEvent onLevelLoadCallback)
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
        beatmap.Metadata.bpm = loadedLevel._beatsPerMinute;
        if (loadedLevel._songName.Contains("/")) { loadedLevel._songName = loadedLevel._songName.Replace("/", string.Empty); }
        beatmap.Metadata.mapName = loadedLevel._songName;
        beatmap.Metadata.songFilename = loadedLevel._songFilename;
        beatmap.BeatData = beatDataV3;

        if (onLevelLoadCallback != null)
        {
            onLevelLoadCallback(beatmap);
        }
    }
}
