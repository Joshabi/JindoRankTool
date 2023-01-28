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

    public void SetStartPosition(int x, int y) { startPositioning.x = x; startPositioning.y = y; }
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
    private static readonly Dictionary<int, float> leftForehandDict = new Dictionary<int, float>()
    { { 0, -180 }, { 1, 0 }, { 2, 90 }, { 3, -90 }, { 4, 135 }, { 5, -135 }, { 6, 45 }, { 7, -45 }, { 8, 0 } };
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
    private static readonly Dictionary<int, float> leftBackhandDict = new Dictionary<int, float>()
    { { 0, 0 }, { 1, -180 }, { 2, -90 }, { 3, 90 }, { 4, -45 }, { 5, 45 }, { 6, -135 }, { 7, 135 }, { 8, 0 } };

    public static readonly Dictionary<int, int> opposingCutDict = new Dictionary<int, int>()
    { { 0, 1 }, { 1, 0 }, { 2, 3 }, { 3, 2 }, { 4, 7 }, { 7, 4 }, { 5, 6 }, { 6, 5 } };

    private static readonly List<int> forehandResetDict = new List<int>()
    { 1, 2, 3, 6, 7 };
    private static readonly List<int> backhandResetDict = new List<int>()
    { 0, 4, 5 };

    public static Dictionary<int, float> ForehandDict { get { return (_rightHand) ? rightForehandDict : leftForehandDict; } }
    public static Dictionary<int, float> BackhandDict { get { return (_rightHand) ? rightBackhandDict : leftBackhandDict; } }

    // Contains a list of directional vecotrs
    public static readonly Vector2[] directionalVectors =
{
        new Vector2(0, 1),   // up
        new Vector2(0, -1),  // down
        new Vector2(-1, 0),  // left
        new Vector2(1, 0),   // right
        new Vector2(1, 1).normalized,   // up right
        new Vector2(-1, 1).normalized,  // up left
        new Vector2(-1, -1).normalized, // down left
        new Vector2(1, -1).normalized   // down right
    };

    private static readonly Dictionary<Vector2, int> directionalVectorToCutDirection = new Dictionary<Vector2, int>()
    {
            { new Vector2(0, 1), 0 },
            { new Vector2(0, -1), 1 },
            { new Vector2(-1, 0), 2 },
            { new Vector2(1, 0), 3 },
            { new Vector2(1, 1), 5 },
            { new Vector2(-1, 1), 4 },
            { new Vector2(-1, -1), 6 },
            { new Vector2(1, -1), 7 }
    };

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

            // If parity is the same as before and not flagged as a bomb reset.
            // LATER: Add logic to determine if adding a swing or rolling is the better option.
            if (sData.sliceParity == lastSwing.sliceParity && sData.resetType != ResetType.Bomb) { sData.resetType = ResetType.Normal; }

            // If current parity method thinks we are upside down, flip values.
            if (_parityMethodology.UpsideDown == true)
            {
                sData.SetStartAngle(sData.startPositioning.angle * -1);
                sData.SetEndAngle(sData.endPositioning.angle * -1);
            }

            // If dot, re-orientate
            if (sData.notesInCut[0].d == 8 && sData.notesInCut.Count == 1) FixDotOrientation(lastSwing, ref sData);

            // Add swing to list
            result.Add(sData);
            notesInSwing.Clear();
        }
        // Add empty swings in for bomb avoidance.
        // Replace later with more advanced movement to avoid bombs in general.
        Debug.Log("Right Hand: " + _rightHand + " | Resets: " + result.Count(x => x.resetType == ResetType.Normal));
        result = AddBombResetAvoidance(result);
        return result;
    }

    #region Dots and Bombs Checks
    // Modifies a Swing if Dot Notes are involved
    public BeatCutData DotChecks(BeatCutData currentSwing, BeatCutData lastSwing)
    {
        // If the entire swing is dots
        if(currentSwing.notesInCut.All(x => x.d == 8))
        {
            // If there is more then 1 note, indicating a dot stack, tower, or zebra slider
            if (currentSwing.notesInCut.Count > 1)
            {
                // Get the first and last note based on beats
                float angle;
                ColourNote firstNote = currentSwing.notesInCut.OrderBy(x => x.b).FirstOrDefault();
                ColourNote lastNote = currentSwing.notesInCut.OrderBy(x => x.b).LastOrDefault();
                float firstToLast = AngleBetweenNotes(firstNote, lastNote);
                float lastToFirst = AngleBetweenNotes(lastNote, firstNote);
                float currentAngle = lastSwing.endPositioning.angle;

                // Determine the angle change between current angle and next
                float FTLChange = currentAngle - firstToLast;
                float LTFChange = currentAngle - lastToFirst;

                // Depending on which is less change, set the angle.
                // Need some decision making logic here if hitting it either way is the same rotation
                if (Mathf.Abs(FTLChange) < Mathf.Abs(LTFChange)) { angle = firstToLast; } 
                else if(Mathf.Abs(FTLChange) > Mathf.Abs(LTFChange)) { angle = lastToFirst; }
                else
                {
                    // In the event the angle change is the same hitting either note first. Do some additional checks
                    // based on the distance to the note
                    Vector2 lastHitNotePosition = new(lastSwing.endPositioning.x, lastSwing.endPositioning.y);
                    Vector2 firstNoteVec = new(firstNote.x, firstNote.y);
                    Vector2 lastNoteVec = new(lastNote.x, lastNote.y);

                    float distToFirst = Vector2.Distance(firstNoteVec, lastHitNotePosition);
                    float distToLast = Vector2.Distance(lastNoteVec, lastHitNotePosition);

                    if (Mathf.Abs(distToFirst) < Mathf.Abs(distToLast)) { angle = firstToLast; }
                    else { angle = lastToFirst; }

                    // NOTE: I dont think it can be the same angle change and equal distance to the next note?
                    // This should work.
                }

                // Configure the swing
                currentSwing.SetStartAngle(angle);
                currentSwing.sliceStartBeat = firstNote.b;
                currentSwing.sliceEndBeat = lastNote.b + 0.1f;
                currentSwing.SetStartPosition(firstNote.x, firstNote.y);
                currentSwing.SetEndPosition(lastNote.x, lastNote.y);

                // Set ending angle equal to starting angle
                currentSwing.SetEndAngle(currentSwing.startPositioning.angle);
            }
        } 
        // Not sure why, but this fixes the right hand on dot stacks and apparently doesn't need to be done to left?
        if (currentSwing.startPositioning.angle == 180 && _rightHand) currentSwing.SetStartAngle(currentSwing.startPositioning.angle = 0);
        return currentSwing;
    }

    // Attempts to fix the orientation in which a dot note is swung based on prior and post dot swings
    private void FixDotOrientation(BeatCutData lastSwing, ref BeatCutData currentSwing)
    {
        ColourNote dotNote = currentSwing.notesInCut.OrderBy(x => x.b).FirstOrDefault();
        ColourNote lastNote = lastSwing.notesInCut[^1];

        Vector2 dir = (new Vector2(dotNote.x, dotNote.y) - new Vector2(lastNote.x, lastNote.y)).normalized;
        Vector2 lowestDotProduct = directionalVectors.OrderBy(v => Vector2.Dot(dir, v)).First();
        Vector2 cutDirection = new Vector2(Mathf.Round(lowestDotProduct.x), Mathf.Round(lowestDotProduct.y));
        int orientation = directionalVectorToCutDirection[cutDirection];

        if (dotNote.x == lastNote.x && dotNote.y == lastNote.y) {
            orientation = opposingCutDict[orientation];
        }

        float angle = (lastSwing.sliceParity == Parity.Backhand) ?
            BackhandDict[orientation] :
            ForehandDict[orientation];

        if (lastSwing.endPositioning.angle == 0 && angle == -180) angle = 0;

        // Checks for clamping on top and bottom row
        float xDiff = Mathf.Abs(dotNote.x - lastNote.x);
        if (xDiff < 3 && (dotNote.y == 2 || dotNote.y == 0)) angle = Mathf.Clamp(angle, -45, 45);
        if (xDiff < 3 && (lastNote.y == 2)) angle = Mathf.Clamp(angle, -90, 90);

        // Clamps inwards backhand hits if the note is only 1 away
        if (xDiff == 1 && lastNote.x > dotNote.x && _rightHand && currentSwing.sliceParity == Parity.Backhand) angle = 0;
        else if (xDiff == 1 && lastNote.x < dotNote.x && !_rightHand && currentSwing.sliceParity == Parity.Backhand) angle = 0;

        currentSwing.SetStartAngle(angle);
        currentSwing.SetEndAngle(angle);

        return;
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