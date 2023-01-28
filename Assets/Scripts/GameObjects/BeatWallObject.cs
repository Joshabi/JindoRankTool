using UnityEngine;

public class BeatWallObject : MonoBehaviour
{
    [SerializeField] private GameObject _wall;
    [SerializeField] private Color _wallColour;
    [SerializeField] private float _timeToReachTarget = 0.4f;

    [SerializeField] private int _x;
    [SerializeField] private int _y;
    [SerializeField] private int _w;
    [SerializeField] private int _h;
    [SerializeField] private int _l;

    private IRuntimeLevelContext _runtimeLevelContext;
    private float _elapsedTime = 0.0f;

    public void Init(int x, int y, int w, int h, int l, float moveSpeed, IRuntimeLevelContext runtimeLevelContext)
    {
        _runtimeLevelContext = runtimeLevelContext;
        _timeToReachTarget = 4.0f / moveSpeed;
        _x = x;
        _y = y;
        _w = w;
        _h = h;
        _l = l;

        float xOffsetting = (w % 2 == 1) ? 1 : 0.4f;

        transform.position = LevelUtils.GetWorldXYFromBeatmapCoords(x - (w/2), 2-(y + (h/2)));
        if (w > 1) transform.position = new(transform.position.x + w - xOffsetting, transform.position.y);
        transform.localScale = new Vector3(_w, Mathf.Clamp(_h,2,3), 1);
    }
}
