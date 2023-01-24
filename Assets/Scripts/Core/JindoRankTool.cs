using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LevelDownloader))]
[RequireComponent(typeof(LevelPreview))]
public class JindoRankTool : MonoBehaviour
{

    [SerializeField] private string _customLevelPath;
    [SerializeField] private BeatmapDifficultyRank _desiredDifficulty;
    [SerializeField] private bool _previewMap = true;
    [SerializeField] private string _beatSaberMapIDCSV;

    [SerializeField] private float _timeScale = 1.0f;

    private LevelPreview _levelPreview;
    private LevelLoader _levelLoader;
    private LevelDownloader _levelDownloader;
    private LevelSliceMapOutputter _sliceMapOutputter;
    private System.Guid _doublesAnalyserID;

    private void Awake()
    {
        Time.timeScale = _timeScale;

        _sliceMapOutputter = new LevelSliceMapOutputter();
        _doublesAnalyserID = _sliceMapOutputter.RegisterAnalyser(new SliceMapDoublesAnalyser());

        if (_previewMap)
        {
            _levelPreview = GetComponent<LevelPreview>();
            _levelPreview.PreviewMap(_customLevelPath, _desiredDifficulty);
            _levelLoader = new LevelLoader();
            _levelLoader.LoadLevel(_customLevelPath, _levelLoader_OnLevelLoaded);
        }

        if (_beatSaberMapIDCSV.Length > 0)
        {
            _levelDownloader = GetComponent<LevelDownloader>();
            _levelDownloader.OnLevelDownloadsCompleted += OnLevelDownloadsComplete;
            _levelDownloader.DownloadLevels(_beatSaberMapIDCSV);
        }
        else
        {
            OnLevelDownloadsComplete();
        }
    }

    private void OnLevelDownloadsComplete()
    {
        _levelLoader = new LevelLoader();
        string[] levelPaths = System.IO.Directory.GetDirectories(PathUtils.GetImportDirectory());
        foreach (string levelPath in levelPaths)
        {
            _levelLoader.LoadLevel(levelPath, _levelLoader_OnLevelLoaded);
        }
    }

    private void OnDestroy()
    {
        _sliceMapOutputter.UnregisterAnalyser(_doublesAnalyserID);
    }

    private void _levelLoader_OnLevelLoaded(BeatmapData beatmapData)
    {
        _sliceMapOutputter.ProcessBeatmap(beatmapData);
    }
}
