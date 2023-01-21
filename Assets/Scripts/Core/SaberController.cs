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
    private float _targetWristOrientation;
    private float _targetPalmOrientation;
    private Vector2 _targetWristPosition;
    private float _saberZ;
    private Color _saberColour;
    private float _wristOrientSpeed = 45.0f;
    private float _palmOrientSpeed = 30.0f;
    private float _wristPositSpeed = 45.0f;
    
    // Start is called before the first frame update
    void Awake()
    {
        _wrist = new GameObject("wrist");
        _wrist.transform.parent = transform;
        _palm = new GameObject("palm");
        _palm.transform.parent = _wrist.transform;
        _saber = GameObject.Instantiate<SaberObject>(SaberPrefab);
        _saber.transform.parent = _palm.transform;
        _saber.SetColour(_saberColour);
        _wristOrientSpeed /= Time.timeScale;
        _palmOrientSpeed /= Time.timeScale;
        _wristPositSpeed /= Time.timeScale;
    }

    // Update is called once per frame
    void Update()
    {
        _wristOrientation -= (_wristOrientation - _targetWristOrientation) / _wristOrientSpeed;
        _palmOrientation -= (_palmOrientation - _targetPalmOrientation) / _palmOrientSpeed;
        _wristPosition -= (_wristPosition - _targetWristPosition) / _wristPositSpeed;
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

}
