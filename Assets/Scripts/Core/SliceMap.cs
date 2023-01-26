using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public enum Parity
{
    None,
    Forehand,
    Backhand
}

[System.Serializable]
public enum ResetType
{
    None,
    Normal,
    Bomb,
    Roll
}

[System.Serializable]
public struct PositioningData
{
    public float angle;
    public int x;
    public int y;
}

[System.Serializable]
public struct BeatCutData
{
    public Parity sliceParity;
    public ResetType resetType;
    public float sliceStartBeat;
    public float sliceEndBeat;
    public float swingEBPM;
    public bool isInverted;
    public List<ColourNote> notesInCut;

    public void SetStartPosition(int x, int y) { startPositioning.x = x; endPositioning.y = y; }
    public void SetEndPosition(int x, int y) { endPositioning.x = x; endPositioning.y = y; }
    public void SetStartAngle(float angle) { startPositioning.angle = angle; }
    public void SetEndAngle(float angle) { endPositioning.angle = angle; }
    public bool IsReset { get { return resetType != 0; } }

    public PositioningData startPositioning;
    public PositioningData endPositioning;

}

// Adapted from Joshabi's ParityChecker
public class SliceMap
{

    #region Parity and Orientation Dictionaries

    // RIGHT HAND PARITY DICTIONARIES
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
    public static readonly Dictionary<int, float> rightForehandDict = new Dictionary<int, float>()
    { { 0, -180 }, { 1, 0 }, { 2, -90 }, { 3, 90 }, { 4, -135 }, { 5, 135 }, { 6, -45 }, { 7, 45 }, { 8, 0 } };
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
    public static readonly Dictionary<int, float> rightBackhandDict = new Dictionary<int, float>()
    { { 0, 0 }, { 1, -180 }, { 2, 90 }, { 3, -90 }, { 4, 45 }, { 5, -45 }, { 6, 135 }, { 7, -135 }, { 8, 0 } };

    // LEFT HAND PARITY DICTIONARIES
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
    public static readonly Dictionary<int, float> leftForehandDict = new Dictionary<int, float>()
    { { 0, -180 }, { 1, 0 }, { 2, 90 }, { 3, -90 }, { 4, 135 }, { 5, -135 }, { 6, 45 }, { 7, -45 }, { 8, 0 } };
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
    public static readonly Dictionary<int, float> leftBackhandDict = new Dictionary<int, float>()
    { { 0, 0 }, { 1, -180 }, { 2, -90 }, { 3, 90 }, { 4, -45 }, { 5, 45 }, { 6, -135 }, { 7, 135 }, { 8, 0 } };

    public static readonly Dictionary<int, int> opposingCutDict = new Dictionary<int, int>()
    { { 0, 1 }, { 1, 0 }, { 2, 3 }, { 3, 2 }, { 4, 7 }, { 7, 4 }, { 5, 6 }, { 6, 5 } };

    public static readonly List<int> forehandResetDict = new List<int>()
    { 1, 2, 3, 6, 7 };
    public static readonly List<int> backhandResetDict = new List<int>()
    { 0, 4, 5 };

    public static Dictionary<int, float> ForehandDict { get { return (_rightHand) ? rightForehandDict : leftForehandDict; } }
    public static Dictionary<int, float> BackhandDict { get { return (_rightHand) ? rightBackhandDict : leftBackhandDict; } }

    #endregion

    // Parity Methodology can be hotswapped between DefaultParityCheck and ResetParityCheck (True Acc) currently.
    // The idea is later on that you could have a list of <IParityChecks> and this would be more modularised.
    // Means you can easily customize the parity deciding behaviour to change how it reads the map.
    private IParityMethod _parityMethodology = new DefaultParityCheck();
    private List<ColourNote> _blocks;
    private List<BombNote> _bombs;
    private List<Obstacle> _walls;
    private List<BeatCutData> _cuts;
    private static bool _rightHand;
    private float _BPM = 0.0f;
    private int _playerXOffset = 0;
    private float _lastWallTime = 0;

    public int GetSliceCount()
    {
        return _cuts.Count;
    }

    public BeatCutData GetBeatCutData(int index)
    {
        return _cuts[index];
    }

    public SliceMap(float inBPM, List<ColourNote> blocks, List<BombNote> bombs, List<Obstacle> walls, bool isRightHand)
    {
        _BPM = inBPM;
        _blocks = new List<ColourNote>(blocks);
        _blocks.RemoveAll(x => isRightHand ? x.c == 0 : x.c == 1);
        _blocks.Sort((x, y) => x.b.CompareTo(y.b));
        _bombs = bombs;
        _bombs.Sort((x, y) => x.b.CompareTo(y.b));
        _walls = walls;
        _walls.Sort((x, y) => x.b.CompareTo(y.b));
        _cuts = GetCutData(_blocks, _bombs, walls, isRightHand);
    }

    public void WriteBeatCutDataToList(List<BeatCutData> inOutCutData)
    {
        if (inOutCutData == null)
        {
            inOutCutData = new List<BeatCutData>();
        }

        foreach (BeatCutData cutData in _cuts)
        {
            inOutCutData.Add(cutData);
        }
    }

    List<BeatCutData> GetCutData(List<ColourNote> notes, List<BombNote> bombs, List<Obstacle> walls, bool isRightHand)
    {
        _rightHand = isRightHand;
        List<BeatCutData> result = new List<BeatCutData>();

        float sliderPrecision = 1 / 6f;
        List<ColourNote> notesInSwing = new List<ColourNote>();

        for (int i = 0; i < notes.Count - 1; i++)
        {
            ColourNote currentNote = notes[i];
            ColourNote nextNote = notes[i + 1];

            notesInSwing.Add(currentNote);

            // If precision falls under "Slider", or time stamp is the same, run
            // checks to figure out if it is a slider, window, stack ect..
            if (Mathf.Abs(currentNote.b - nextNote.b) <= sliderPrecision) {
                if (nextNote.d == 8 || notesInSwing[^1].d == 8 ||
                    currentNote.d == nextNote.d || Mathf.Abs(ForehandDict[currentNote.d] - ForehandDict[nextNote.d]) <= 45 ||
                     Mathf.Abs(BackhandDict[currentNote.d] - BackhandDict[nextNote.d]) <= 45)
                    { continue; }
            }

            // Assume by default swinging forehanded
            BeatCutData sData = new BeatCutData();
            sData.notesInCut = new List<ColourNote>(notesInSwing);
            sData.sliceParity = Parity.Forehand;
            sData.sliceStartBeat = notesInSwing[0].b;
            sData.sliceEndBeat = notesInSwing[^1].b + 0.1f;
            sData.SetStartPosition(notesInSwing[0].x, notesInSwing[0].y);
            sData.SetStartAngle(ForehandDict[notesInSwing[0].d]);
            sData.SetEndPosition(notesInSwing[^1].x, notesInSwing[^1].y);
            sData.SetEndAngle(ForehandDict[notesInSwing[^1].d]);

            // If first swing, figure out starting orientation based on cut direction
            if (result.Count == 0) {
                if (currentNote.d == 0 || currentNote.d == 4 || currentNote.d == 5) {
                    sData.sliceParity = Parity.Backhand;
                    sData.SetStartAngle(BackhandDict[notesInSwing[0].d]);
                    sData.SetEndAngle(BackhandDict[notesInSwing[^1].d]);
                }
                result.Add(sData);
                notesInSwing.Clear();
                continue;
            }

            // If previous swing exists
            BeatCutData lastSwing = result[^1];
            ColourNote lastNote = lastSwing.notesInCut[^1];

            // Performs Dot Checks under the assumption is a forehand swing
            sData = DotChecks(sData, lastSwing);

            // Get swing EBPM, if reset then double
            sData.swingEBPM = SwingEBPM(_BPM, currentNote.b - lastNote.b);
            if (sData.IsReset) { sData.swingEBPM *= 2; }

            // Invert Check
            if (sData.isInverted == false)
            {
                for (int last = 0; last < lastSwing.notesInCut.Count; last++)
                {
                    for (int next = 0; next < notesInSwing.Count; next++)
                    {
                        if (IsInvert(lastSwing.notesInCut[last], notesInSwing[next]))
                        {
                            sData.isInverted = true;
                            break;
                        }
                    }
                }
            }

            // Work out current player XOffset for bomb calculations
            List<Obstacle> wallsInBetween = walls.FindAll(x => x.b > lastNote.b && x.b < notesInSwing[^1].b);
            if(wallsInBetween.Count != 0) {
                foreach(Obstacle wall in wallsInBetween)
                {
                    if (wall.x == 1 || wall.x == 0 && wall.w > 1) {
                        _playerXOffset = 1;
                        _lastWallTime = wall.b;
                    } else if (wall.x == 2) {
                        _playerXOffset = -1;
                        _lastWallTime = wall.b;
                    }
                }
            }

            // If time since dodged exceeds a set amount in seconds, undo dodge
            var undodgeCheckTime = 0.35f;
            if (BeatToSeconds(_BPM, notesInSwing[^1].b - _lastWallTime) > undodgeCheckTime) { _playerXOffset = 0; }

            // Work out Parity
            List<BombNote> bombsBetweenSwings = bombs.FindAll(x => x.b > lastNote.b && x.b < notesInSwing[^1].b);
            sData.sliceParity = _parityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, _playerXOffset, _rightHand);

            // If backhand, readjust start and end angle
            if (sData.sliceParity == Parity.Backhand) {
                sData.SetStartAngle(BackhandDict[notesInSwing[0].d]);
                sData.SetEndAngle(BackhandDict[notesInSwing[^1].d]);
                sData = DotChecks(sData, result[^1]);
            }

            // If dot, re-orientate
            if (sData.notesInCut[0].d == 8 && sData.notesInCut.Count == 1) sData = FixDotOrientation(lastSwing, sData);

            // If parity is the same as before and not flagged as a bomb reset.
            // LATER: Add logic to determine if adding a swing or rolling is the better option.
            if (sData.sliceParity == lastSwing.sliceParity && sData.resetType != ResetType.Bomb) { sData.resetType = ResetType.Normal; }

            // If current parity method thinks we are upside down, flip values.
            if (_parityMethodology.UpsideDown == true)
            {
                sData.SetStartAngle(sData.startPositioning.angle * -1);
                sData.SetEndAngle(sData.endPositioning.angle * -1);
            }

            // Add swing to list
            result.Add(sData);
            notesInSwing.Clear();
        }
        // Add empty swings in for bomb avoidance.
        // Replace later with more advanced movement to avoid bombs in general.
        result = AddBombResetAvoidance(result);
        Debug.Log("Right Hand: " + _rightHand + " | Resets: " + result.Count(x => x.resetType == ResetType.Normal));
        return result;
    }

    #region Dots and Bombs Checks
    // Modifies a Swing if Dot Notes are involved
    public BeatCutData DotChecks(BeatCutData currentSwing, BeatCutData lastSwing)
    {
        // If the entire swing is dots
        if(currentSwing.notesInCut.Count(x => x.d == 8) == currentSwing.notesInCut.Count)
        {
            // If there is more then 1 note, indicating a dot stack, tower, or zebra slider
            if (currentSwing.notesInCut.Count > 1)
            {
                // Check which note is closer from last swing
                // Depending on which is closer, calculate angle
                Vector2 lastSwingVec = LevelUtils.GetWorldXYFromBeatmapCoords(lastSwing.notesInCut[0].x, lastSwing.notesInCut[0].y);
                Vector2 firstNoteVec = LevelUtils.GetWorldXYFromBeatmapCoords(currentSwing.notesInCut[0].x, currentSwing.notesInCut[0].y);
                Vector2 lastNoteVec = LevelUtils.GetWorldXYFromBeatmapCoords(currentSwing.notesInCut[^1].x, currentSwing.notesInCut[^1].y);

                float distanceToStart = Vector2.Distance(firstNoteVec, lastSwingVec);
                float distanceToEnd = Vector2.Distance(lastNoteVec, lastSwingVec);

                // Depending on Parity, calculate the cut direction of the dot stack
                float angle = (currentSwing.sliceParity == Parity.Forehand) ?
                    AngleBetweenNotes(currentSwing.notesInCut[^1], currentSwing.notesInCut[0]) :
                    AngleBetweenNotes(currentSwing.notesInCut[0], currentSwing.notesInCut[^1]);
                currentSwing.SetStartAngle(angle);

                if (distanceToStart > distanceToEnd)
                {
                    currentSwing.notesInCut.Reverse();
                    angle = (currentSwing.sliceParity == Parity.Forehand) ?
                        AngleBetweenNotes(currentSwing.notesInCut[^1], currentSwing.notesInCut[0]) :
                        AngleBetweenNotes(currentSwing.notesInCut[0], currentSwing.notesInCut[^1]);
                    currentSwing.SetStartAngle(angle);
                    currentSwing.sliceStartBeat = currentSwing.notesInCut[0].b;
                    currentSwing.sliceEndBeat = currentSwing.notesInCut[^1].b + 0.1f;
                    currentSwing.SetStartPosition(currentSwing.notesInCut[0].x, currentSwing.notesInCut[0].y);
                    currentSwing.SetEndPosition(currentSwing.notesInCut[^1].x, currentSwing.notesInCut[^1].y);
                }

                // Can possibly remove? Fixes weird backhand down stack hits for the right hand? Not sure why
                if (currentSwing.startPositioning.angle == 180 && _rightHand) currentSwing.SetStartAngle(currentSwing.startPositioning.angle * -1);

                // Set ending angle equal to starting angle
                currentSwing.SetEndAngle(currentSwing.startPositioning.angle);
            }
        } else if (currentSwing.notesInCut[0].d == 8 && currentSwing.notesInCut[^1].d != 8) {
            // In the event its a dot then an arrow
            currentSwing.SetStartAngle(AngleGivenCutDirection(currentSwing.notesInCut[^1].d, currentSwing.sliceParity));
            float angle = (currentSwing.sliceParity == Parity.Forehand) ?
                ForehandDict[currentSwing.notesInCut[^1].d] :
                BackhandDict[currentSwing.notesInCut[^1].d];
            currentSwing.SetEndAngle(angle);
        }
        return currentSwing;
    }

    // Attempts to fix the orientation in which a dot note is swung based on prior and post dot swings
    private BeatCutData FixDotOrientation(BeatCutData lastSwing, BeatCutData currentSwing)
    {
        // Get the previous and current notes
        ColourNote lastNote = lastSwing.notesInCut[^1];
        ColourNote currentNote = currentSwing.notesInCut[0];

        if (lastNote.d != 8) {
            float angle = (currentSwing.sliceParity == Parity.Forehand) ?
                AngleGivenCutDirection(opposingCutDict[lastNote.d], Parity.Forehand) :
                AngleGivenCutDirection(opposingCutDict[lastNote.d], Parity.Backhand);
            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        } else {
            // If the notes are on the same layer, generate the angle based on the end and starting hand positions
            Vector2 lastHandCoords = new Vector2(lastSwing.endPositioning.x, lastSwing.endPositioning.y);
            Vector2 nextHandCoords = new Vector2(currentNote.x, currentNote.y);
            float angle = Vector3.SignedAngle(Vector3.up, lastHandCoords - nextHandCoords, Vector3.forward);
            
            // Correct the angle for backhand hits
            if (currentSwing.sliceParity == Parity.Backhand) {
                if (angle < 0) { angle += 180; } else if (angle > 0) { angle -= 180; }
            }

            // Flip for left hand
            if (!_rightHand) angle *= -1;

            angle = Mathf.Clamp(angle, -90, 90);
            if (currentNote.y != lastNote.y) angle = Mathf.Clamp(angle, -45, 45);

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        }
        return currentSwing;
    }

    // Attempts to add bomb avoidance based on the isReset tag for a list of swings.
    // NOTE: To improve this, probably want bomb detection in its own function and these swings
    // would be added for each bomb in the sabers path rather then only for bomb resets.
    private List<BeatCutData> AddBombResetAvoidance(List<BeatCutData> swings)
    {
        List<BeatCutData> result = new List<BeatCutData>(swings);
        int swingsAdded = 0;

        for (int i = 0; i < swings.Count - 1; i++)
        {
            // Later on, different reset types will have different behaviours
            if (swings[i].resetType == ResetType.Bomb || swings[i].resetType == ResetType.Normal)
            {
                // Reference to last swing
                ColourNote lastNote = swings[i - 1].notesInCut[^1];

                // Create a new swing with inverse parity to the last.
                BeatCutData emptySwing = new BeatCutData();
                emptySwing.sliceParity = (swings[i].sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                emptySwing.sliceStartBeat = swings[i - 1].sliceEndBeat + SecondsToBeats(_BPM, 0.15f);
                emptySwing.sliceEndBeat = emptySwing.sliceStartBeat + 0.2f;
                emptySwing.SetStartPosition(lastNote.x, lastNote.y);

                // If the last hit was a dot, pick the opposing direction based on parity.
                float angle;
                if (lastNote.d == 8) {
                    angle = (emptySwing.sliceParity == Parity.Forehand) ?
                        ForehandDict[1] : BackhandDict[0];
                } else {
                    // If the last hit was arrowed, figure out the opposing cut direction and use that.
                    angle = (emptySwing.sliceParity == Parity.Forehand) ?
                        ForehandDict[opposingCutDict[lastNote.d]] :
                        BackhandDict[opposingCutDict[lastNote.d]];
                }

                // Set start and end angle, should be the same
                emptySwing.SetStartAngle(angle);
                emptySwing.SetEndAngle(angle);

                // Calculate the direction, set it, then insert this swing into the returned result list.
                Vector3 dir = Quaternion.Euler(0, 0, emptySwing.startPositioning.angle) * Vector3.up;
                Vector2 endPosition = new Vector2(dir.x * 2f, dir.y * 2f);
                emptySwing.SetEndPosition((int)endPosition.x, (int)endPosition.y);

                result.Insert(i + swingsAdded, emptySwing);
                swingsAdded++;
            }
        }
        return result;
    }


    #endregion

    #region Helper Functions
    // Given a cut direction ID, return angle from appropriate dictionary
    public static float AngleGivenCutDirection(int cutDirection, Parity parity)
    {
        return (parity == Parity.Forehand) ? ForehandDict[cutDirection] : BackhandDict[cutDirection];
    }
    // Returns the angle between any 2 given notes
    private float AngleBetweenNotes(ColourNote firstNote, ColourNote lastNote) {
        Vector3 firstNoteCoords = new Vector3(firstNote.x, firstNote.y, 0);
        Vector3 lastNoteCoords = new Vector3 (lastNote.x, lastNote.y, 0);
        float angle = Vector3.SignedAngle(Vector3.up, lastNoteCoords - firstNoteCoords, Vector3.forward);
        if (!_rightHand) angle *= -1;
        return angle;
    }
    // Determines if a Note is inverted
    private bool IsInvert(ColourNote lastNote, ColourNote nextNote)
    {
        // Is Note B in the direction of Note A's cutDirection.
        switch (lastNote.d)
        {
            case 0:
                // Up note
                return (nextNote.x > lastNote.x);
            case 1:
                // Down note
                return (nextNote.x < lastNote.x);
            case 2:
                // Left note
                return (nextNote.x < lastNote.x);
            case 3:
                // Right note
                return (nextNote.y > lastNote.y);
            case 4:
                // Up, Left note
                return (nextNote.x < lastNote.x && nextNote.y > lastNote.y);
            case 5:
                // Up, Right note
                return (nextNote.x > lastNote.x && nextNote.y > lastNote.y);
            case 6:
                // Down, Left note
                return (nextNote.x < lastNote.x && nextNote.y < lastNote.y);
            case 7:
                // Down, Right note
                return (nextNote.x > lastNote.x && nextNote.y < lastNote.y);
        }
        return false;
    }
    // Calculates the effective BPM of a swing
    private float SwingEBPM(float BPM, float beatDiff)
    {
        var seconds = BeatToSeconds(BPM, beatDiff);
        TimeSpan time = TimeSpan.FromSeconds(seconds);

        return (float)((60000 / time.TotalMilliseconds) / 2);
    }
    // Converts beats to seconds based on BPM
    public float BeatToSeconds(float BPM, float beatDiff)
    {
        return (beatDiff / (BPM / 60));
    }
    // Converts seconds to beats based on BPM
    public float SecondsToBeats(float BPM, float seconds)
    {
        return (BPM/60) * seconds;
    }
    #endregion
}