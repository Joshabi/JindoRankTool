using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(LevelDownloader))]
[RequireComponent(typeof(LevelPreview))]
public class JindoRankTool : MonoBehaviour
{

    [SerializeField] private string _customLevelPath;
    [SerializeField] private BeatmapDifficultyRank _desiredDifficulty;
    [SerializeField] private bool _previewMap = true;
    [SerializeField] private string _playlistURL;

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

        if (_playlistURL.Length > 0)
        {
            _levelDownloader = GetComponent<LevelDownloader>();
            _levelDownloader.OnLevelDownloaded += OnLevelDownloadComplete;
            StartCoroutine(DownloadPlaylist(_playlistURL));
        }
        else
        {
            OnLevelDownloadsComplete();
        }
    }

    private IEnumerator DownloadPlaylist(string url)
    {
        using (UnityWebRequest playlistRequest = UnityWebRequest.Get(_playlistURL))
        {
            yield return playlistRequest.SendWebRequest();

            if (playlistRequest.isDone)
            {
                string playlistJSON = playlistRequest.downloadHandler.text;
                PlaylistReader playlistReader = new PlaylistReader();
                _mapDifficulties = playlistReader.GetMapsFromPlaylist(playlistJSON);
                List<MapDownloadRequest> requestList = new List<MapDownloadRequest>();
                foreach (string code in _mapDifficulties.Keys)
                {
                    requestList.Add(new MapDownloadRequest(MapCodeType.Hash, code));
                }
                _levelDownloader.DownloadLevels(requestList);
            }
        }
    }

    private void OnLevelDownloadsComplete()
    {
        _levelLoader = new LevelLoader();
        string[] levelPaths = System.IO.Directory.GetDirectories(PathUtils.GetImportDirectory());
        foreach (string levelPath in levelPaths)
        {
            OnLevelDownloadComplete(levelPath);
        }
    }

    private void OnLevelDownloadComplete(string levelPath)
    {
        if (_levelLoader == null)
        {
            _levelLoader = new LevelLoader();
        }
        string code = levelPath.Remove(levelPath.Length-1).Split('/')[^1];
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

    private void OnDestroy()
    {
        _sliceMapOutputter.UnregisterAnalyser(_doublesAnalyserID);
        _sliceMapOutputter.UnregisterAnalyser(_coverageAnalyserID);
    }

    private void _levelLoader_OnLevelLoaded(BeatmapData beatmapData)
    {
        _sliceMapOutputter.ProcessBeatmap(beatmapData);
    }
}
