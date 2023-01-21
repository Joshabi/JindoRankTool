using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct LevelStructure
{

    public string _version;
    public string _songName;
    public string _songSubName;
    public string _songAuthorName;
    public string _levelAuthorName;
    public float _beatsPerMinute;
    public int _shuffle;
    public float _shufflePeriod;
    public float _previewStartTime;
    public float _previewDuration;
    public string _songFilename;
    public string _coverImageFilename;
    public string _environmentName;
    public string _allDirectionsEnvironmentName;
    public int _songTimeOffset;
    public object _customData;
    public LevelDifficultyStructure[] _difficultyBeatmapSets;
}

[System.Serializable]
public struct LevelDifficultyStructure
{
    public string _beatmapCharacteristicName;
    public BeatmapStructure[] _difficultyBeatmaps;
}

public enum LevelDifficulty
{
    Easy = 1,
    Normal = 3,
    Hard = 5,
    Expert = 7,
    ExpertPlus = 9,
}
