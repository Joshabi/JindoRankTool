using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JindoRankTool : MonoBehaviour
{

    [SerializeField] private string _customLevelPath;
    [SerializeField] private int _desiredDifficulty;
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private LevelPreview _levelPreview;
    [SerializeField] private Text _mapNameTextField;
    [SerializeField] private float _timeScale = 1.0f;

    private void Awake()
    {
        Time.timeScale = _timeScale;
        if (_levelLoader != null)
        {
            _levelLoader.OnLevelLoaded += _levelLoader_OnLevelLoaded;
            _levelLoader.LoadLevel(_customLevelPath, _desiredDifficulty);
        }
    }

    private void _levelLoader_OnLevelLoaded(AudioClip levelAudio, BeatmapData beatmapData)
    {
        if (_levelPreview != null)
        {
            //if (!_levelPreview.IsPreviewing())
            //{
            _mapNameTextField.text = beatmapData.Metadata.mapName + " (" + GetDifficultyString(_desiredDifficulty) + ")";
                _levelPreview.SetBeatmap(levelAudio, beatmapData);
            //}
        }
        else
        {
            Debug.LogError("Null level preview");
        }
    }

    private string GetDifficultyString(int diff)
    {
        switch (diff)
        {
            case 0: return "Easy";
            case 1: return "Normal";
            case 2: return "Hard";
            case 3: return "Expert";
            case 4: return "Expert+";
        }
        return "Other";
    }
}
