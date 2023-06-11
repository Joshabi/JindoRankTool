using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JoshaParity;
using UnityEngine;
using System.Runtime;
using UnityEngine.UI;

public interface IRuntimeLevelContext
{
    Color GetLeftColour();
    Color GetRightColour();

    float GetSaberZ();

    void DestroyBlock(GameObject go);
}

[RequireComponent(typeof(LevelAudioLoader))]
public class LevelPreview : MonoBehaviour, IRuntimeLevelContext
{
    [SerializeField] private LevelAudioLoader _levelAudioLoader;

    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private BeatmapData _beatmap;
    [SerializeField] private BeatCubeObject _cubePrefab;
    [SerializeField] private SaberController _saberPrefab;
    [SerializeField] private GameObject _wallPrefab;
    [SerializeField] private GameObject _bombPrefab;
    [SerializeField] private float _speed = 100.0f;
    [SerializeField] private Color _leftColour;
    [SerializeField] private Color _rightColour;
    [SerializeField] private float _saberZ = -30.0f;
    [SerializeField] private Text _mapNameText;
    [SerializeField] private Text _timingDataText;
    [SerializeField] private Text _leftSaberDataText;
    [SerializeField] private Text _rightSaberDataText;
    [SerializeField] private Text _offsetDataText;
    [SerializeField] private float _startTime = 0.0f;

    private BeatmapDifficultyRank _desiredDifficulty;
    private string _currentMapDirectory;

    private float _songTime = 0.0f;
    private float _beatTime = 0.0f;

    private float _startBeatOffset = 0.0f;
    private float _beatsPerSecond = 0.0f;
    private MapAnalyser _analyser;
    private List<ColourNote> _blocks = new List<ColourNote>();
    private List<BombNote> _bombs = new List<BombNote>();
    private List<Obstacle> _obstacles = new List<Obstacle>();
    private int _blockIndex = 0;
    private int _bombIndex = 0;
    private int _obstaclesIndex = 0;

    private SaberController _leftSaber;
    private SaberController _rightSaber;
    private List<SwingData> _leftHandSwings;
    private List<SwingData> _rightHandSwings;
    private int _leftSliceIndex = 0;
    private int _rightSliceIndex = 0;

    private List<GameObject> _pendingRemoval;
    private List<GameObject> _goInstances;
    private bool _isPreviewing = false;
    private float _timeToReachSabers;
    private float _beatTimeToReachSabers;
    private float _beatTimeToPrepareSwing;
    private float _BPM;

    private void Awake()
    {
        _levelAudioLoader = GetComponent<LevelAudioLoader>();

        _leftColour.a = 1.0f;
        _rightColour.a = 1.0f;

        _pendingRemoval = new List<GameObject>();
        _timeToReachSabers = Mathf.Abs((Mathf.Abs(_saberZ)-3.0f) / _speed);
        _blocks = new List<ColourNote>();
        _bombs = new List<BombNote>();
        _obstacles = new List<Obstacle>();
        _goInstances = new List<GameObject>();
        _leftSaber = GameObject.Instantiate<SaberController>(_saberPrefab);
        _leftSaber.transform.position = Vector3.zero;
        _leftSaber.SetSaberColour(GetLeftColour());
        _leftSaber.SetRestingWristPosition(0, 1);
        _leftSaber.SetRestingWristOrientation(10.0f);
        _rightSaber = GameObject.Instantiate<SaberController>(_saberPrefab);
        _rightSaber.SetSaberColour(GetRightColour());
        _rightSaber.transform.position = Vector3.zero;
        _rightSaber.SetRestingWristPosition(3, 1);
        _rightSaber.SetRestingWristOrientation(-10.0f);
        _leftSaber.SetSaberZ(_saberZ);
        _rightSaber.SetSaberZ(_saberZ);
        _leftSaber.SetRestingTargets();
        _rightSaber.SetRestingTargets();
        _leftSaberDataText.color = GetLeftColour();
        _rightSaberDataText.color = GetRightColour();
    }

    public Color GetLeftColour()
    {
        return _leftColour;
    }

    public Color GetRightColour()
    {
        return _rightColour;
    }

    public float GetSaberZ()
    {
        return _saberZ;
    }

    public void DestroyBlock(GameObject go)
    {
        _pendingRemoval.Add(go);
    }

    public bool IsPreviewing()
    {
        return _isPreviewing;
    }

    public void PreviewMap(string levelFolder, LevelStructure loadedLevel, BeatmapData beatmapData)
    {
        _BPM = loadedLevel._beatsPerMinute;
        _beatmap = beatmapData;
        _currentMapDirectory = levelFolder;

        MapAnalyser mapAnalyser = new(levelFolder, new GenericParityCheck());
        _analyser = mapAnalyser;

        _mapNameText.text = beatmapData.Metadata.mapName + " (" + beatmapData.Metadata._difficultyRank.ToString() + ")";
        _levelAudioLoader.LoadSong(_currentMapDirectory + "/" + beatmapData.Metadata.songFilename, OnLevelAudioLoaded);
    }

    private void OnLevelAudioLoaded(AudioClip audio)
    {
        SetBeatmap(audio, _beatmap);
    }


    public void SetBeatmap(AudioClip inAudioClip, BeatmapData inBeatmapData)
    {
        _songTime = 0.0f;
        _beatTime = 0.0f;
        _blockIndex = 0;
        _bombIndex = 0;

        _beatmap = inBeatmapData;
        _beatsPerSecond = _BPM / 60;
        _beatTimeToReachSabers = TimeUtils.SecondsToBeats(_BPM, _timeToReachSabers);
        _beatTimeToPrepareSwing = _beatTimeToReachSabers * 0.5f;

        if (_audioSource != null)
        {
            _audioSource.clip = inAudioClip;
            _audioSource.pitch = Time.timeScale;
            _audioSource.Play();
        }
        else
        {
            Debug.LogError("No audio source.");
        }

        foreach (GameObject go in _goInstances)
        {
            GameObject.Destroy(go);
        }
        _goInstances.Clear();

        _blocks.Clear();
        _bombs.Clear();
        _obstacles.Clear();
        _startBeatOffset = 2.0f;
        foreach (ColourNote block in _beatmap.BeatData.colorNotes)
        {
            _blocks.Add(block);
        }

        if (_beatmap.BeatData.burstSliders != null)
        {
            foreach (BurstSlider slider in _beatmap.BeatData.burstSliders)
            {
                _blocks.Add(new ColourNote()
                    {
                        x = slider.x,
                        y = slider.y,
                        c = slider.c,
                        d = slider.d
                    }

                );
            }
        }

        _blocks.Sort((x, y) => x.b.CompareTo(y.b));
        foreach (BombNote bomb in _beatmap.BeatData.bombNotes)
        {
            _bombs.Add(bomb);
        }
        _bombs.Sort((x, y) => x.b.CompareTo(y.b));
        foreach(Obstacle obst in _beatmap.BeatData.obstacles)
        {
            _obstacles.Add(obst);
        }
        _obstacles.Sort((x, y) => x.b.CompareTo(y.b));

        _desiredDifficulty = _beatmap.Metadata._difficultyRank;
        List<SwingData> swings = _analyser.GetSwingData(_desiredDifficulty, "standard");
        _leftHandSwings = swings.FindAll(x => !x.rightHand);
        _rightHandSwings = swings.FindAll(x => x.rightHand);

        _leftSliceIndex = 0;
        _rightSliceIndex = 0;

        if (_startTime > 0.0f)
        {
            SetTime(_startTime);
        }

        _isPreviewing = true;
    }

    private void Update()
    {
        if (!_isPreviewing)
        {
            return;
        }

        if (_beatmap.Metadata.mapName.Length == 0)
        {
            return;
        }

        foreach (GameObject go in _pendingRemoval)
        {
            _goInstances.Remove(go);
            GameObject.Destroy(go);
        }
        _pendingRemoval.Clear();

        _songTime = _audioSource.time;
        _beatTime = TimeUtils.SecondsToBeats(_BPM, _songTime);
        _timingDataText.text = _songTime.ToString("F2")+"s (beat: "+_beatTime.ToString("F1")+")";

        if (_blockIndex < _blocks.Count)
        {
            while (_beatTime > _blocks[_blockIndex].b - _startBeatOffset)
            {
                SpawnNote(_blocks[_blockIndex].x, _blocks[_blockIndex].y, _blocks[_blockIndex].d, _blocks[_blockIndex].c);
                ++_blockIndex;
                if (_blockIndex >= _blocks.Count)
                {
                    break;
                }
            }
        }
        if (_bombIndex < _bombs.Count)
        {
            while (_beatTime > _bombs[_bombIndex].b - _startBeatOffset)
            {
                SpawnBomb(_bombs[_bombIndex].x, _bombs[_bombIndex].y);
                ++_bombIndex;
                if (_bombIndex >= _bombs.Count)
                {
                    break;
                }
            }
        }
        if (_obstaclesIndex < _obstacles.Count)
        {
            while (_beatTime > _obstacles[_obstaclesIndex].b - _startBeatOffset)
            {
                Obstacle wall = _obstacles[_obstaclesIndex];
                SpawnWall(wall.x, wall.y, wall.w, wall.h, wall.d);
                ++_obstaclesIndex;
                if (_obstaclesIndex >= _obstacles.Count) { break; }
            }
        }


        if (_leftSliceIndex < _leftHandSwings.Count)
        {
            // Change beatcutdata into swing data
            SwingData swingData = _leftHandSwings[_leftSliceIndex];
            _leftSaber.SetRestingWristPosition(1+(int)swingData.playerHorizontalOffset, 0);
            if (_beatTime > swingData.swingStartBeat - _startBeatOffset)
            {
                _leftSaber.SetTargetWristPosition(swingData.startPos.x, swingData.startPos.y);
                _leftSaber.SetTargetWristOrientation(swingData.startPos.rotation * -1);
                _leftSaber.SetTargetEBPM(swingData.swingEBPM);
            }
            if (_beatTime > swingData.swingStartBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                _leftSaber.SetTargetPalmOrientation(swingData.swingParity == Parity.Forehand ? 180.0f : 0.0f);
            }
            if (_beatTime > swingData.swingEndBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                ++_leftSliceIndex;
                if (_leftSliceIndex < _leftHandSwings.Count)
                {
                    SwingData nextSwingData = _leftHandSwings[_leftSliceIndex];
                    float timeTilNextBeat = TimeUtils.BeatsToSeconds(_BPM, nextSwingData.swingStartBeat - swingData.swingEndBeat);
                    _leftSaber.SetTimeToNextBeat(timeTilNextBeat);
                }
            }
            UpdateSaberCutText(_leftSaberDataText, swingData);
            UpdateOffsetInfoText(_offsetDataText, swingData);
        }
        if (_rightSliceIndex < _rightHandSwings.Count)
        {
            SwingData swingData = _rightHandSwings[_rightSliceIndex];
            _rightSaber.SetRestingWristPosition(2 + (int)swingData.playerHorizontalOffset, 0);
            if (_beatTime > swingData.swingStartBeat - _startBeatOffset)
            {
                _rightSaber.SetTargetWristPosition(swingData.startPos.x, swingData.startPos.y);
                _rightSaber.SetTargetWristOrientation(swingData.startPos.rotation);
                _rightSaber.SetTargetEBPM(swingData.swingEBPM);
            }
            if (_beatTime > swingData.swingStartBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                _rightSaber.SetTargetPalmOrientation(swingData.swingParity == Parity.Forehand ? 180.0f : 0.0f);
            }
            if (_beatTime > swingData.swingEndBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                ++_rightSliceIndex;
                if (_rightSliceIndex < _rightHandSwings.Count)
                {
                    SwingData nextSwingData = _rightHandSwings[_rightSliceIndex];
                    float timeTilNextBeat = TimeUtils.BeatsToSeconds(_BPM, nextSwingData.swingStartBeat - swingData.swingEndBeat);
                    _rightSaber.SetTimeToNextBeat(timeTilNextBeat);
                }
            }
            UpdateSaberCutText(_rightSaberDataText, swingData);
            UpdateOffsetInfoText(_offsetDataText, swingData);
        }

        List<GameObject> removals = new List<GameObject>();
        foreach (GameObject go in _goInstances)
        {
            BeatCubeObject cube = go.GetComponent<BeatCubeObject>();
            if (cube != null)
            {
                cube.UpdateYAndRotation();
            }

            Vector3 pos = go.transform.position;
            pos.z -= _speed * Time.deltaTime;
            go.transform.position = pos;

            if (pos.z <= Camera.main.transform.position.z)
            {
                removals.Add(go);
            }
        }
        foreach (GameObject go in removals)
        {
            _goInstances.Remove(go);
            GameObject.Destroy(go);
        }
    }

    private void UpdateSaberCutText(Text text, SwingData data)
    {
        text.text = "Parity: " + data.swingParity.ToString() + ",\tAngle: " + data.startPos.rotation + ",\tPosition: (" + data.startPos.x + "," + data.startPos.y + ")";
    }

    private void UpdateOffsetInfoText(Text text, SwingData data)
    {
        text.text = "X offset: " + data.playerHorizontalOffset + ",\tY offset: " + data.playerVerticalOffset;
    }

    public void SpawnNote(int x, int y, int d, int c)
    {
        BeatCubeObject cube = GameObject.Instantiate<BeatCubeObject>(_cubePrefab);
        cube.Init(x, y, (BeatCutDirection)d, (BeatChirality)c, _speed, this);
        _goInstances.Add(cube.gameObject);
    }

    public void SpawnBomb(int x, int y)
    {
        GameObject bomb = GameObject.Instantiate(_bombPrefab);
        bomb.transform.position = LevelUtils.GetWorldXYFromBeatmapCoords(x, y);
        _goInstances.Add(bomb);
    }

    public void SpawnWall(int x, int y, int w, int h, int d)
    {
        BeatWallObject wall = GameObject.Instantiate(_wallPrefab).GetComponent<BeatWallObject>();
        wall.Init(x, y, w, h, d, _speed, this);
        _goInstances.Add(wall.gameObject);
    }

    public void SetTime(float t)
    {
        _songTime = t;
        _beatTime = _songTime * _beatsPerSecond;
        _audioSource.time = _songTime;

        foreach (GameObject go in _goInstances)
        {
            GameObject.Destroy(go);
        }
        _goInstances.Clear();

        for (int i = 0; i < _blocks.Count; ++i)
        {
            if (_blocks[i].b > _beatTime)
            {
                _blockIndex = i;
                break;
            }
        }

        if (_blockIndex == 0)
        {
            _blockIndex = _blocks.Count;
        }

        for (int i = 0; i < _bombs.Count; ++i)
        {
            if (_bombs[i].b > _beatTime)
            {
                _bombIndex = i;
                break;
            }
        }

        if (_bombIndex == 0)
        {
            _bombIndex = _bombs.Count;
        }

        for (int i = 0; i < _obstacles.Count; ++i)
        {
            if (_obstacles[i].b > _beatTime)
            {
                _obstaclesIndex = i;
                break;
            }
        }
        if (_obstaclesIndex == 0)
        {
            _obstaclesIndex = _bombs.Count;
        }

        int numLeftSwingData = _leftHandSwings.Count;
        for (int i = 0; i < numLeftSwingData; ++i)
        {
            if (_leftHandSwings[i].swingStartBeat > _beatTime)
            {
                _leftSliceIndex = i;
                break;
            }
        }

        int numRightSwingData = _rightHandSwings.Count;
        for (int i = 0; i < numRightSwingData; ++i)
        {
            if (_rightHandSwings[i].swingStartBeat > _beatTime)
            {
                _rightSliceIndex = i;
                break;
            }
        }
    }

}
