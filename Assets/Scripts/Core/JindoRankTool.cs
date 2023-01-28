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

    private MapDatabase _mapDatabase;
    private LevelPreview _levelPreview;
    private LevelLoader _levelLoader;
    private LevelDownloader _levelDownloader;
    private LevelSliceMapOutputter _sliceMapOutputter;
    private System.Guid _doublesAnalyserID;
    private System.Guid _coverageAnalyserID;

    private void Awake()
    {
        Time.timeScale = _timeScale;

        _mapDatabase = new MapDatabase();
        _sliceMapOutputter = new LevelSliceMapOutputter(_mapDatabase);
        _doublesAnalyserID = _sliceMapOutputter.RegisterAnalyser(new SliceMapDoublesAnalyser());
        _coverageAnalyserID = _sliceMapOutputter.RegisterAnalyser(new SliceMapCoverageAnalyser());

        if (_previewMap)
        {
            _levelLoader = new LevelLoader();
            _levelLoader.LoadLevel(_customLevelPath, _desiredDifficulty, OnPreviewLevelLoaded);
        }

        if (_playlistURL.Length > 0)
        {
            _levelDownloader = GetComponent<LevelDownloader>();
            _levelDownloader.OnLevelDownloadsComplete += OnLevelDownloadsComplete;
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
            try
            {
                string json = System.IO.File.ReadAllText(levelPath + "/download.json");
                JSONBeatSaverMapDownloadData downloadData = JsonUtility.FromJson<JSONBeatSaverMapDownloadData>(json);
                string hash = downloadData.versions[^1].hash;
                MapId id = new MapId();
                _mapDatabase.SetMapHash(id, hash);
                _mapDatabase.SetMapFolder(id, levelPath);
                _mapDatabase.SetMapBPM(id, downloadData.metadata.bpm);
                _mapDatabase.SetMapDuration(id, downloadData.metadata.duration);
                OnLevelDownloadComplete(levelPath, downloadData);
            }
            catch (System.ArgumentOutOfRangeException e)
            {
                Debug.LogError(e);
                Debug.LogError("Not sure how this one is happening yet.");
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                Debug.LogError("json parse error for download.json in directory \"" + levelPath + "\".");
            }
        }
    }

    private void OnLevelDownloadComplete(string levelPath, JSONBeatSaverMapDownloadData downloadData)
    {
        if (_levelLoader == null)
        {
            _levelLoader = new LevelLoader();
        }

        string hash = downloadData.versions[^1].hash;
        if (_mapDifficulties.ContainsKey(hash))
        {
            foreach (BeatmapDifficultyRank difficulty in _mapDifficulties[hash])
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

    private void _levelLoader_OnLevelLoaded(string levelFolder, LevelStructure loadedLevel, BeatmapData beatmapData)
    {
        MapId id = _mapDatabase.GetMapIdFromFolderPath(levelFolder);
        beatmapData.Metadata.id = id;
        _sliceMapOutputter.ProcessBeatmap(beatmapData);
    }

    private void OnPreviewLevelLoaded(string levelFolder, LevelStructure loadedLevel, BeatmapData beatmapData)
    {
        _levelPreview = GetComponent<LevelPreview>();
        MapId id = new MapId();
        _mapDatabase.SetMapFolder(id, levelFolder);
        _levelPreview.PreviewMap(levelFolder, loadedLevel, beatmapData);
    }
}
