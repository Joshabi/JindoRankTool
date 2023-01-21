using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatCubeObject : MonoBehaviour
{
    [SerializeField] private GameObject _arrow;
    [SerializeField] private GameObject _dot;
    [SerializeField] private Color _leftColour;
    [SerializeField] private Color _rightColour;
    [SerializeField] private float _timeToReachTarget = 0.4f;

    [SerializeField] private int _x;
    [SerializeField] private int _y;
    [SerializeField] private BeatCutDirection _d;
    [SerializeField] private BeatChirality _c;

    private IRuntimeLevelContext _runtimeLevelContext;
    private float _elapsedTime = 0.0f;

    public void OnCollisionEnter(Collision collision)
    {
        int colliderLayer = 1 << (collision.collider.gameObject.layer);
        GetComponent<MeshRenderer>().material.color = Color.green;
        if (colliderLayer == LayerMask.GetMask("Saber"))
        {
            _runtimeLevelContext.DestroyBlock(gameObject);
        }
    }

    public void Init(int x, int y, BeatCutDirection d, BeatChirality c, float moveSpeed, IRuntimeLevelContext runtimeLevelContext)
    {
        _runtimeLevelContext = runtimeLevelContext;
        _timeToReachTarget = 4.0f / moveSpeed;
        _x = x;
        _y = y;
        _d = d;
        _c = c;
        transform.position = LevelUtils.GetWorldXYFromBeatmapCoords(x, 0);
        GetComponent<MeshRenderer>().material.color = GetColorFromChirality(c);
        if (d == BeatCutDirection.Any)
        {
            _dot.SetActive(true);
        }
        else
        {
            _arrow.SetActive(true);
        }
    }

    private Color GetColorFromChirality(BeatChirality inChirality)
    {
        if (inChirality == BeatChirality.LeftHand)
        {
            return _runtimeLevelContext.GetLeftColour();
        }
        else if (inChirality == BeatChirality.RightHand)
        {
            return _runtimeLevelContext.GetRightColour();
        }
        else
        {
            Debug.LogError("Invalid chirality specified.");
            return Color.magenta;
        }
    }

    public void UpdateYAndRotation()
    {
        if (transform.position.z <= _runtimeLevelContext.GetSaberZ())
        {
            _runtimeLevelContext.DestroyBlock(gameObject);
        }

        _elapsedTime += Time.deltaTime;
        float u = Mathf.Min(1.0f, _elapsedTime / _timeToReachTarget);

        Vector3 startPos = LevelUtils.GetWorldXYFromBeatmapCoords(_x, 0);
        Vector3 endPos = LevelUtils.GetWorldXYFromBeatmapCoords(_x, _y);
        float lerpY = Mathf.Lerp(startPos.y, endPos.y, u);
        Vector3 pos = transform.position;
        pos.y = lerpY;

        float startAngle = DirectionToStartAngleInDegrees(_d);
        float endAngle = DirectionToAngleInDegrees(_d);
        float lerpAngle = Mathf.LerpAngle(startAngle, endAngle, u);

        transform.position = pos;
        transform.rotation = Quaternion.AngleAxis(lerpAngle, Vector3.back);
    }

    private float DirectionToAngleInDegrees(BeatCutDirection inDirection)
    {
        float angle = 0.0f;
        switch (inDirection)
        {
            case BeatCutDirection.Up: angle = 180.0f; break;
            case BeatCutDirection.Left: angle = 90.0f; break;
            case BeatCutDirection.Right: angle = 270.0f; break;
            case BeatCutDirection.UpLeft: angle = 135.0f; break;
            case BeatCutDirection.UpRight: angle = 225.0f; break;
            case BeatCutDirection.DownLeft: angle = 45.0f; break;
            case BeatCutDirection.DownRight: angle = 315.0f; break;
        }
        return angle;
    }

    private float DirectionToStartAngleInDegrees(BeatCutDirection inDirection)
    {
        float angle = 0.0f;
        switch (inDirection)
        {
            case BeatCutDirection.Up: angle = 180.0f; break;
            case BeatCutDirection.Left: angle = DirectionToAngleInDegrees(BeatCutDirection.Left); break;
            case BeatCutDirection.Right: angle = DirectionToAngleInDegrees(BeatCutDirection.Right); break;
            case BeatCutDirection.UpLeft: angle = DirectionToAngleInDegrees(BeatCutDirection.Up); break;
            case BeatCutDirection.UpRight: angle = DirectionToAngleInDegrees(BeatCutDirection.Up); break;
            case BeatCutDirection.DownLeft: angle = DirectionToAngleInDegrees(BeatCutDirection.Down); break;
            case BeatCutDirection.DownRight: angle = DirectionToAngleInDegrees(BeatCutDirection.Down); break;
        }
        return angle;
    }
}
