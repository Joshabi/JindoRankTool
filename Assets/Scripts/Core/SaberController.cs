using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaberController : MonoBehaviour
{
    [SerializeField] private SaberObject SaberPrefab;

    private GameObject _wrist;
    private GameObject _palm;
    private SaberObject _saber;
    private float _wristOrientation;
    private float _palmOrientation;
    private Vector2 _wristPosition;
    private float _restingWristOrientation;
    private float _targetWristOrientation;
    private float _targetPalmOrientation;
    private Vector2 _restingWristPosition;
    private Vector2 _targetWristPosition;
    private float _saberZ;
    private Color _saberColour;
    private float _maxWristOrientDrag = 120.0f;
    private float _maxPalmOrientDrag = 50.0f;
    private float _maxWristPositDrag = 120.0f;
    private float _minWristOrientDrag = 40.0f;
    private float _minPalmOrientDrag = 24.0f;
    private float _minWristPositDrag = 40.0f;
    private float _wristOrientDrag;
    private float _palmOrientDrag;
    private float _wristPositDrag;
    private float _targetWristOrientDrag;
    private float _targetPalmOrientDrag;
    private float _targetWristPositDrag;

    // Start is called before the first frame update
    void Awake()
    {
        _wristOrientDrag = _maxWristOrientDrag;
        _palmOrientDrag = _maxPalmOrientDrag;
        _wristPositDrag = _maxWristPositDrag;
        _targetWristOrientDrag = _wristOrientDrag;
        _targetPalmOrientDrag = _palmOrientDrag;
        _targetWristPositDrag = _wristPositDrag;
        _wrist = new GameObject("wrist");
        _wrist.transform.parent = transform;
        _palm = new GameObject("palm");
        _palm.transform.parent = _wrist.transform;
        _saber = GameObject.Instantiate<SaberObject>(SaberPrefab);
        _saber.transform.parent = _palm.transform;
        _saber.SetColour(_saberColour);
        _wristOrientDrag /= Time.timeScale;
        _palmOrientDrag /= Time.timeScale;
        _wristPositDrag /= Time.timeScale;
    }

    // Update is called once per frame
    void Update()
    {
        _wristOrientDrag -= (_wristOrientDrag - _targetWristOrientDrag) / 10.0f;
        _palmOrientDrag -= (_palmOrientDrag - _targetPalmOrientDrag) / 5.0f;
        _wristPositDrag -= (_wristPositDrag - _targetWristPositDrag) / 10.0f;
        _wristOrientation -= (_wristOrientation - (_targetWristOrientation + 5.0f*Mathf.Sin(Time.time))) / _wristOrientDrag;
        _palmOrientation -= (_palmOrientation - (_targetPalmOrientation + 5.0f*Mathf.Cos(Time.time))) / _palmOrientDrag;
        _wristPosition -= (_wristPosition - _targetWristPosition) / _wristPositDrag;
        Vector3 pos = _wrist.transform.position;
        pos.x = _wristPosition.x;
        pos.y = _wristPosition.y;
        pos.z = _saberZ;
        _wrist.transform.position = pos;
        _wrist.transform.localRotation = Quaternion.AngleAxis(_wristOrientation, Vector3.forward);
        _palm.transform.localRotation = Quaternion.AngleAxis(_palmOrientation, Vector3.right);
    }

    public void SetTargetWristOrientation(float a)
    {
        if (_wristOrientation - a > 180.0f)
        {
            a -= 360.0f;
        }
        _targetWristOrientation = a;
    }

    public void SetTargetPalmOrientation(float a)
    {
        _targetPalmOrientation = a;
    }

    public void SetTargetWristPosition(int x, int y)
    {
        _targetWristPosition = LevelUtils.GetWorldXYFromBeatmapCoords(x, y);
    }

    public void SetRestingWristPosition(int x, int y)
    {
        _restingWristPosition.x = x;
        _restingWristPosition.y = y;
    }

    public void SetRestingWristOrientation(float a)
    {
        _restingWristOrientation = a;
    }

    public void SetSaberColour(Color c)
    {
        if (_saber != null)
        {
            _saber.SetColour(c);
        }
        else
        {
            _saberColour = c;
        }
    }

    public void SetSaberZ(float inZ)
    {
        _saberZ = inZ;
    }

    public void SetTimeToNextBeat(float t)
    {
        if (t < 0.2f)
        {
            _targetWristOrientDrag = _minWristOrientDrag;
            _targetPalmOrientDrag = _minPalmOrientDrag;
            _targetWristPositDrag = _minWristPositDrag;
        }
        else
        {
            SetTargetWristPosition((int)_restingWristPosition.x, (int)_restingWristPosition.y);
            SetTargetWristOrientation(_restingWristOrientation);

            float u = Mathf.Clamp((t - 0.2f), 0.0f, 1.0f);

            _targetWristOrientDrag = Mathf.Lerp(_minWristOrientDrag, _maxWristOrientDrag, u) / Time.timeScale;
            _targetPalmOrientDrag = Mathf.Lerp(_minPalmOrientDrag, _maxPalmOrientDrag, u) / Time.timeScale;
            _targetWristPositDrag = Mathf.Lerp(_minWristPositDrag, _maxWristPositDrag, u) / Time.timeScale;
        }
    }

    public void SetRestingTargets()
    {
        SetTargetWristPosition((int)_restingWristPosition.x, (int)_restingWristPosition.y);
        SetTargetWristOrientation(_restingWristOrientation);
    }

}
