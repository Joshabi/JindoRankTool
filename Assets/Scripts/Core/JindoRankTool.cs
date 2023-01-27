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

    private Dictionary<string, HashSet<BeatmapDifficultyRank>> _mapDifficulties;

    private LevelPreview _levelPreview;
    private LevelLoader _levelLoader;
    private LevelDownloader _levelDownloader;
    private LevelSliceMapOutputter _sliceMapOutputter;
    private System.Guid _doublesAnalyserID;
    private System.Guid _coverageAnalyserID;

    private void Awake()
    {
        Time.timeScale = _timeScale;

        _sliceMapOutputter = new LevelSliceMapOutputter();
        _doublesAnalyserID = _sliceMapOutputter.RegisterAnalyser(new SliceMapDoublesAnalyser());
        _coverageAnalyserID = _sliceMapOutputter.RegisterAnalyser(new SliceMapCoverageAnalyser());

        if (_previewMap)
        {
            _levelPreview = GetComponent<LevelPreview>();
            _levelPreview.PreviewMap(_customLevelPath, _desiredDifficulty);
            _levelLoader = new LevelLoader();
            _levelLoader.LoadLevel(_customLevelPath, _levelLoader_OnLevelLoaded);
        }

        if (_beatSaberMapIDCSV.Length > 0)
        {
            _mapDifficulties = new Dictionary<string, HashSet<BeatmapDifficultyRank>>();
            string[] codes = _beatSaberMapIDCSV.Split(',');
            foreach (string code in codes)
            {
                if (code.Contains(':'))
                {
                    string levelID = code.Split(':')[0];
                    BeatmapDifficultyRank difficulty = StringToDifficulty(code.Split(':')[1]);
                    if (_mapDifficulties.ContainsKey(levelID))
                    {
                        _mapDifficulties[levelID].Add(difficulty);
                    }
                    else
                    {
                        _mapDifficulties.Add(levelID, new HashSet<BeatmapDifficultyRank>() { difficulty });
                    }
                }
            }

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
            string code = levelPath.Split('\\')[^1];
            if (_mapDifficulties.ContainsKey(code))
            {
                foreach (BeatmapDifficultyRank difficulty in _mapDifficulties[code])
                {
                    _levelLoader.LoadLevel(levelPath, difficulty, _levelLoader_OnLevelLoaded);
                }
            }
            else
            {
                _levelLoader.LoadLevel(levelPath, _levelLoader_OnLevelLoaded);
            }
        }
    }

    private void OnDestroy()
    {
        _sliceMapOutputter.UnregisterAnalyser(_doublesAnalyserID);
        _sliceMapOutputter.UnregisterAnalyser(_coverageAnalyserID);
    }

    private void _levelLoader_OnLevelLoaded(BeatmapData beatmapData)
    {
        _sliceMapOutputter.ProcessBeatmap(beatmapData);
    }
    private BeatmapDifficultyRank StringToDifficulty(string inDifficulty)
    {
        switch (inDifficulty.ToLower())
        {
            case "easy": return BeatmapDifficultyRank.Easy;
            case "normal": return BeatmapDifficultyRank.Normal;
            case "hard": return BeatmapDifficultyRank.Hard;
            case "expert": return BeatmapDifficultyRank.Expert;
            case "expertplus": return BeatmapDifficultyRank.ExpertPlus;
            case "expert+": return BeatmapDifficultyRank.ExpertPlus;
        }

        return BeatmapDifficultyRank.Easy;
    }
}
