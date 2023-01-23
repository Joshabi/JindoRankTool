using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JindoRankTool : MonoBehaviour
{

    [SerializeField] private string _customLevelPath;
    [SerializeField] private BeatmapDifficultyRank _desiredDifficulty;
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private LevelPreview _levelPreview;
    [SerializeField] private Text _mapNameTextField;
    [SerializeField] private float _timeScale = 1.0f;

    private LevelSliceMapOutputter _sliceMapOutputter;
    private System.Guid _doublesAnalyserID;

    private void Awake()
    {
        Time.timeScale = _timeScale;
        if (_levelLoader != null)
        {
            _sliceMapOutputter = new LevelSliceMapOutputter(_levelLoader);
            _doublesAnalyserID = _sliceMapOutputter.RegisterAnalyser(new SliceMapDoublesAnalyser());

            _levelLoader.OnLevelLoaded += _levelLoader_OnLevelLoaded;
            _levelLoader.LoadLevel(_customLevelPath, _desiredDifficulty);
        }
    }

    private void OnDestroy()
    {
        if (_sliceMapOutputter != null)
        {
            _sliceMapOutputter.UnregisterAnalyser(_doublesAnalyserID);
        }
    }

    private void _levelLoader_OnLevelLoaded(AudioClip levelAudio, BeatmapData beatmapData)
    {
        if (_levelPreview != null)
        {
            //if (!_levelPreview.IsPreviewing())
            //{
            _mapNameTextField.text = beatmapData.Metadata.mapName + " (" + beatmapData.Metadata._difficultyRank.ToString() + ")";
                _levelPreview.SetBeatmap(levelAudio, beatmapData);
            //}
        }
        else
        {
            Debug.LogError("Null level preview");
        }
    }
}
