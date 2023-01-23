using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public interface IRuntimeLevelContext
{
    Color GetLeftColour();
    Color GetRightColour();

    float GetSaberZ();

    void DestroyBlock(GameObject go);
}

public class LevelPreview : MonoBehaviour, IRuntimeLevelContext
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private BeatmapData _beatmap;
    [SerializeField] private BeatCubeObject _cubePrefab;
    [SerializeField] private SaberController _saberPrefab;
    [SerializeField] private GameObject _bombPrefab;
    [SerializeField] private float _speed = 100.0f;
    [SerializeField] private Color _leftColour;
    [SerializeField] private Color _rightColour;
    [SerializeField] private float _saberZ = -30.0f;
    [SerializeField] private Text _timingDataText;
    [SerializeField] private Text _leftSaberDataText;
    [SerializeField] private Text _rightSaberDataText;
    [SerializeField] private float _startTime = 0.0f;

    private float _songTime = 0.0f;
    private float _beatTime = 0.0f;

    private float _startBeatOffset = 0.0f;
    private float _beatsPerSecond = 0.0f;
    private List<ColourNote> _blocks = new List<ColourNote>();
    private List<BombNote> _bombs = new List<BombNote>();
    private List<Obstacle> _obstacles = new List<Obstacle>();
    private int _blockIndex = 0;
    private int _bombIndex = 0;

    private SaberController _leftSaber;
    private SaberController _rightSaber;
    private SliceMap _sliceMapLeft;
    private SliceMap _sliceMapRight;
    private int _leftSliceIndex = 0;
    private int _rightSliceIndex = 0;

    private List<GameObject> _pendingRemoval;
    private List<GameObject> _goInstances;
    private bool _isPreviewing = false;
    private float _timeToReachSabers;
    private float _beatTimeToReachSabers;

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

    private void Awake()
    {
        _pendingRemoval = new List<GameObject>();
        _timeToReachSabers = Mathf.Abs((Mathf.Abs(_saberZ)-2.0f) / _speed);
        _blocks = new List<ColourNote>();
        _bombs = new List<BombNote>();
        _goInstances = new List<GameObject>();
        _leftSaber = GameObject.Instantiate<SaberController>(_saberPrefab);
        _leftSaber.transform.position = Vector3.zero;
        _leftSaber.SetSaberColour(GetLeftColour());
        _rightSaber = GameObject.Instantiate<SaberController>(_saberPrefab);
        _rightSaber.SetSaberColour(GetRightColour());
        _rightSaber.transform.position = Vector3.zero;
        _leftSaber.SetSaberZ(_saberZ);
        _rightSaber.SetSaberZ(_saberZ);
    }

    public bool IsPreviewing()
    {
        return _isPreviewing;
    }

    public void SetBeatmap(AudioClip inAudioClip, BeatmapData inBeatmapData)
    {
        _songTime = 0.0f;
        _beatTime = 0.0f;
        _blockIndex = 0;
        _bombIndex = 0;

        _beatmap = inBeatmapData;
        _beatsPerSecond = _beatmap.Metadata.bpm / 60.0f;
        _beatTimeToReachSabers = _timeToReachSabers * _beatsPerSecond;

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
        _startBeatOffset = 2.0f;
        foreach (ColourNote block in _beatmap.BeatData.colorNotes)
        {
            _blocks.Add(block);
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

        _sliceMapRight = new SliceMap(_beatmap.Metadata.bpm, _blocks, _bombs, _obstacles, true);
        _sliceMapLeft = new SliceMap(_beatmap.Metadata.bpm, _blocks, _bombs, _obstacles, false);
        _leftSliceIndex = 0;
        _rightSliceIndex = 0;

        _isPreviewing = true;

        if (_startTime > 0.0f)
        {
            SetTime(_startTime);
        }
    }

    private void Update()
    {
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

        _songTime += Time.deltaTime;
        _beatTime = _songTime * _beatsPerSecond;
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
        if (_leftSliceIndex < _sliceMapLeft.GetSliceCount())
        {
            BeatCutData cutData = _sliceMapLeft.GetBeatCutData(_leftSliceIndex);
            if (_beatTime > cutData.sliceStartBeat - _startBeatOffset)
            {
                _leftSaber.SetTargetWristPosition(cutData.startPositioning.x, cutData.startPositioning.y);
                _leftSaber.SetTargetWristOrientation(cutData.startPositioning.angle);
            }
            if (_beatTime > cutData.sliceStartBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                _leftSaber.SetTargetPalmOrientation(cutData.sliceParity == Parity.Forehand ? 180.0f : 0.0f);
            }
            if (_beatTime > cutData.sliceEndBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                ++_leftSliceIndex;
            }
            UpdateSaberCutText(_leftSaberDataText, cutData);
        }
        if (_rightSliceIndex < _sliceMapRight.GetSliceCount())
        {
            BeatCutData cutData = _sliceMapRight.GetBeatCutData(_rightSliceIndex);
            if (_beatTime > cutData.sliceStartBeat - _startBeatOffset)
            {
                _rightSaber.SetTargetWristPosition(cutData.startPositioning.x, cutData.startPositioning.y);
                _rightSaber.SetTargetWristOrientation(cutData.startPositioning.angle);
            }
            if (_beatTime > cutData.sliceStartBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                _rightSaber.SetTargetPalmOrientation(cutData.sliceParity == Parity.Forehand ? 180.0f : 0.0f);
            }
            if (_beatTime > cutData.sliceEndBeat - _startBeatOffset + _beatTimeToReachSabers)
            {
                ++_rightSliceIndex;
            }
            UpdateSaberCutText(_rightSaberDataText, cutData);
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

    private void UpdateSaberCutText(Text text, BeatCutData data)
    {
        text.text = "Parity: " + data.sliceParity.ToString() + ",\tAngle: " + data.startPositioning.angle + ",\tPosition: (" + data.startPositioning.x + "," + data.startPositioning.y + ")";
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

        int numLeftCutData = _sliceMapLeft.GetSliceCount();
        for (int i = 0; i < numLeftCutData; ++i)
        {
            if (_sliceMapLeft.GetBeatCutData(i).sliceStartBeat > _beatTime)
            {
                _leftSliceIndex = i;
                break;
            }
        }
        int numRightCutData = _sliceMapRight.GetSliceCount();
        for (int i = 0; i < numRightCutData; ++i)
        {
            if (_sliceMapRight.GetBeatCutData(i).sliceStartBeat > _beatTime)
            {
                _rightSliceIndex = i;
                break;
            }
        }
    }

}
