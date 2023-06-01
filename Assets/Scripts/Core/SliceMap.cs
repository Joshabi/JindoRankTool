using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Presets;
using UnityEngine;

[System.Serializable]
public enum Parity
{
    Forehand,
    Backhand
}

[System.Serializable]
public enum ResetType
{
    None,
    Normal,
    Bomb
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
    public int playerXOffset;
    public int playerYOffset;

    public void SetResetType(ResetType type) { resetType = type; }
    public void SetParity(Parity sliceParity) { this.sliceParity = sliceParity; }
    public void SetStartPosition(int x, int y) { startPositioning.x = x; startPositioning.y = y; }
    public void SetEndPosition(int x, int y) { endPositioning.x = x; endPositioning.y = y; }
    public void SetStartAngle(float angle) { startPositioning.angle = angle; }
    public void SetEndAngle(float angle) { endPositioning.angle = angle; }
    public bool IsReset => (int)resetType is 1 or 2;

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

    public static Dictionary<int, float> ForehandDict { get { return (_rightHand) ? rightForehandDict : leftForehandDict; } }
    public static Dictionary<int, float> BackhandDict { get { return (_rightHand) ? rightBackhandDict : leftBackhandDict; } }

    // Contains a list of directional vectors
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

    public static readonly Dictionary<Vector2, int> directionalVectorToCutDirection = new Dictionary<Vector2, int>()
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
    public float _BPM = 0.0f;
    private int _playerXOffset = 0;
    private int _playerYOffset = 0;
    private float _lastWallTime = 0;
    private float _lastDuckTime = 0;

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

        //foreach (BeatCutData swing in _cuts)
        //{
       //     if (!isRightHand) continue;
        //    Debug.Log($"Swing Note/s or Bomb/s {swing.sliceStartBeat} " +
       //               $"| Parity of this swing: {swing.sliceParity}" + " | AFN: " + swing.startPositioning.angle +
       //               $"\nPlayer Offset: {swing.playerXOffset}x {swing.playerYOffset}y | " +
        //              $"Swing EBPM: {swing.swingEBPM} | Reset Type: {swing.resetType}");
       // }

        _cuts = AddBombResetAvoidance(_cuts);
    }

    public void WriteBeatCutDataToList(List<BeatCutData> inOutCutData)
    {
        inOutCutData ??= new List<BeatCutData>();
        inOutCutData.AddRange(_cuts);
    }

    List<BeatCutData> GetCutData(List<ColourNote> notes, List<BombNote> bombs, List<Obstacle> walls, bool isRightHand)
    {
        // Init Variables
        _rightHand = isRightHand;
        List<BeatCutData> result = new List<BeatCutData>();

        float dotSliderPrecision = 59f; // in MS
        float beatMS = 60 * 1000 / _BPM; // beat to ms
        List<ColourNote> notesInSwing = new List<ColourNote>();

        // For every note in the list of notes, attempt to construct a swing
        for (int i = 0; i <= notes.Count - 1; i++)
        {
            ColourNote currentNote = notes[i];

            // If this is not the final note, check for slider or stack
            if (i != notes.Count - 1)
            {
                ColourNote nextNote = notes[i + 1];
                notesInSwing.Add(currentNote);

                //  && MathF.Abs(currentNote.b - notesInSwing[0].b) <= sliderPrecision
                // If precision falls under "Slider", or time stamp is the same, run
                // checks to figure out if it is a slider, window, stack ect..
                float currentNoteMS = currentNote.b * beatMS;
                float nextNoteMS = nextNote.b * beatMS;
                float firstNoteInSwingMS = notesInSwing[0].b * beatMS;

                float timeDiff = Mathf.Abs(currentNoteMS - nextNoteMS);
                float timeSinceSwingStart = MathF.Abs(currentNoteMS - firstNoteInSwingMS);
                if (timeDiff <= dotSliderPrecision)
                {
                    if (nextNote.d == 8 || notesInSwing[^1].d == 8 ||
                        currentNote.d == nextNote.d || Mathf.Abs(ForehandDict[currentNote.d] - ForehandDict[nextNote.d]) <= 45 ||
                         Mathf.Abs(BackhandDict[currentNote.d] - BackhandDict[nextNote.d]) <= 45)
                    { continue; }
                }
            }
            else { notesInSwing.Add(currentNote); }

            // Re-order the notesInCut in the event all the notes are on the same snap and not dots
            if (notesInSwing.All(x => x.d != 8) && notesInSwing.Count > 1)
            {
                // Find the two notes that are furthest apart
                var furthestNotes = (from c1 in notesInSwing
                                     from c2 in notesInSwing
                                     orderby Vector3.Distance(new Vector2(c1.x, c1.y), new Vector2(c2.x, c2.y)) descending
                                     select new { c1, c2 }).First();

                ColourNote noteA = furthestNotes.c1;
                ColourNote noteB = furthestNotes.c2;
                Vector2 noteAPos = new(noteA.x, noteA.y);
                Vector2 noteBPos = new(noteB.x, noteB.y);

                // Get the direction vector from noteA to noteB
                Vector2 ATB = noteBPos - noteAPos;

                Vector2 noteACutVector = directionalVectorToCutDirection.FirstOrDefault(x => x.Value == noteA.d).Key;
                float dotProduct = Vector2.Dot(noteACutVector, ATB);
                if (dotProduct < 0)
                {
                    ATB = -ATB;   // B before A
                }

                // Sort the cubes according to their position along the direction vector
                notesInSwing.Sort((a, b) => Vector2.Dot(new Vector2(a.x, a.y) - new Vector2(noteA.x, noteA.y), ATB).CompareTo(Vector2.Dot(new Vector2(b.x, b.y) - new Vector2(noteA.x, noteA.y), ATB)));
            }

            // Assume by default swinging forehanded
            BeatCutData sData = new();
            sData.notesInCut = new List<ColourNote>(notesInSwing);
            sData.sliceParity = Parity.Forehand;
            sData.sliceStartBeat = notesInSwing[0].b;
            sData.sliceEndBeat = notesInSwing[^1].b + 0.1f;
            sData.SetStartPosition(notesInSwing[0].x, notesInSwing[0].y);
            sData.SetEndPosition(notesInSwing[^1].x, notesInSwing[^1].y);

            // If first swing, check if potentially upswing start based on orientation
            if (result.Count == 0)
            {
                if (currentNote.d == 0 || currentNote.d == 4 || currentNote.d == 5)
                {
                    sData.sliceParity = Parity.Backhand;

                    sData.SetStartAngle(BackhandDict[notesInSwing[0].d]);
                    sData.SetEndAngle(BackhandDict[notesInSwing[^1].d]);
                }
                result.Add(sData);
                notesInSwing.Clear();
                continue;
            }

            // Get previous swing
            BeatCutData lastSwing = result[^1];
            ColourNote lastNote = lastSwing.notesInCut[^1];

            // Re-order the notesInCut in the event all the notes are dots and same snap
            if (sData.notesInCut.Count > 1 && sData.notesInCut.All(x => x.d == 8))
            {
                notesInSwing = new(DotStackSort(lastSwing, notesInSwing, lastSwing.sliceParity));
                sData.SetStartPosition(notesInSwing[0].x, notesInSwing[0].y);
                sData.SetEndPosition(notesInSwing[^1].x, notesInSwing[^1].y);
                Debug.Log(notesInSwing[0].b + " Count:  + no\tSTART POS: " + sData.startPositioning.x+","+sData.startPositioning.y + "\tEND POS: " + sData.endPositioning.x+","+sData.endPositioning.y);
            }

            // Get swing EBPM, if reset then double
            sData.swingEBPM = SwingEBPM(_BPM, currentNote.b - lastNote.b);
            lastSwing.sliceEndBeat = (lastNote.b - currentNote.b) / 2 + lastNote.b;
            if (lastSwing.IsReset) { sData.swingEBPM *= 2; }

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
            if (wallsInBetween.Count != 0)
            {
                foreach (Obstacle wall in wallsInBetween)
                {
                    // Duck wall detection
                    if ((wall.w >= 3 && wall.x <= 1) || (wall.w == 2 && wall.x == 1))
                    {
                        _playerYOffset = -1;
                        _lastDuckTime = wall.b;
                    }

                    // Dodge wall detection
                    if (wall.x == 1 || wall.x == 0 && wall.w > 1)
                    {
                        _playerXOffset = 1;
                        _lastWallTime = wall.b;
                    }
                    else if (wall.x == 2)
                    {
                        _playerXOffset = -1;
                        _lastWallTime = wall.b;
                    }
                }
            }

            // If time since dodged exceeds a set amount in seconds, undo dodge
            var undodgeCheckTime = 0.35f;
            if (TimeUtils.BeatsToSeconds(_BPM, notesInSwing[^1].b - _lastWallTime) > undodgeCheckTime) { _playerXOffset = 0; }
            if (TimeUtils.BeatsToSeconds(_BPM, notesInSwing[^1].b - _lastDuckTime) > undodgeCheckTime) { _playerYOffset = 0; }

            sData.playerXOffset = _playerXOffset;
            sData.playerYOffset = _playerYOffset;

            // Work out Parity
            List<BombNote> bombsBetweenSwings = bombs.FindAll(x => x.b > lastNote.b && x.b < notesInSwing[^1].b);

            // Perform dot checks depending on swing composition.
            if (sData.notesInCut.All(x => x.d == 8) && sData.notesInCut.Count > 1) CalculateDotStackSwingAngle(lastSwing, ref sData);
            if (sData.notesInCut[0].d == 8 && sData.notesInCut.Count == 1) CalculateDotDirection(lastSwing, ref sData);

            float timeSinceLastNote = TimeUtils.BeatsToSeconds(_BPM, currentNote.b - lastSwing.notesInCut[^1].b);
            sData.sliceParity = _parityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, _playerXOffset, _rightHand, timeSinceLastNote);

            // Depending on parity, set angle
            if (sData.notesInCut.Any(x => x.d != 8))
            {
                if (sData.sliceParity == Parity.Backhand)
                {
                    sData.SetStartAngle(BackhandDict[notesInSwing.First(x => x.d != 8).d]);
                    sData.SetEndAngle(BackhandDict[notesInSwing.Last(x => x.d != 8).d]);
                }
                else
                {
                    sData.SetStartAngle(ForehandDict[notesInSwing.First(x => x.d != 8).d]);
                    sData.SetEndAngle(ForehandDict[notesInSwing.Last(x => x.d != 8).d]);
                }
            }

            // If current parity method thinks we are upside down and not dot notes in next hit, flip values.
            // This catch is in place to turn -180 into 180 (because the dictionary only has a definition from all the way around
            // in one direction (which is -180)
            if (_parityMethodology.UpsideDown == true)
            {
                if (sData.notesInCut.All(x => x.d != 8))
                {
                    sData.SetStartAngle(sData.startPositioning.angle * -1);
                    sData.SetEndAngle(sData.endPositioning.angle * -1);
                }
            }

            // Add swing to list
            result.Add(sData);
            notesInSwing.Clear();
        }

        return result;
    }

    #region Dots and Bombs Checks

    private List<ColourNote> DotStackSort(BeatCutData lastSwing, List<ColourNote> nextNotes, Parity lastSwingParity)
    {

        // Find the two notes that are furthest apart
        var furthestNotes = (from c1 in nextNotes
                             from c2 in nextNotes
                             orderby Vector2.Distance(new Vector2(c1.x, c1.y), new Vector2(c2.x, c2.y)) descending
                             select new { c1, c2 }).First();

        ColourNote noteA = furthestNotes.c1;
        ColourNote noteB = furthestNotes.c2;
        Vector2 noteAPos = new(noteA.x, noteA.y);
        Vector2 noteBPos = new(noteB.x, noteB.y);

        // Get the direction vector from noteA to noteB
        Vector2 ATB = noteBPos - noteAPos;

        // In-case the last note was a dot, turn the swing angle into the closest cut direction based on last swing parity
        int lastCutDirApprox = CutDirectionGivenAngle(lastSwing.endPositioning.angle, lastSwing.sliceParity, 45.0f);

        // Convert the cut direction to a directional vector then do the dot product between noteA to noteB and last swing direction
        Vector2 priorCutVector = directionalVectorToCutDirection.FirstOrDefault(x => x.Value == opposingCutDict[lastCutDirApprox]).Key;
        priorCutVector = priorCutVector.normalized;
        ATB = ATB.normalized;
        float dotProduct = Vector2.Dot(priorCutVector, ATB);
        if (dotProduct < 0)
        {
            ATB = -ATB;
        }
        else if (dotProduct == 0)
        {
            // In the event its at a right angle, pick the note with the closest distance
            ColourNote lastNote = lastSwing.notesInCut[^1];

            float aDist = Vector2.Distance(noteAPos, new Vector2(lastNote.x, lastNote.y));
            float bDist = Vector2.Distance(noteBPos, new Vector2(lastNote.x, lastNote.y));

            if (Mathf.Abs(aDist) < Mathf.Abs(bDist))
            {
                nextNotes.Sort((a, b) => Vector2.Distance(new Vector2(a.x, a.y), new Vector2(lastNote.x, lastNote.y))
                    .CompareTo(Vector2.Distance(new Vector2(b.x, b.y), new Vector2(lastNote.x, lastNote.y))));
                return nextNotes;
            }
            else
            {
                nextNotes.Sort((a, b) => Vector2.Distance(new Vector2(b.x, b.y), new Vector2(lastNote.x, lastNote.y))
                    .CompareTo(Vector2.Distance(new Vector2(a.x, a.y), new Vector2(lastNote.x, lastNote.y))));
                return nextNotes;
            }

        }

        // Sort the cubes according to their position along the direction vector
        nextNotes.Sort((a, b) => {
            float distA = Vector2.Dot(new Vector2(a.x, a.y) - noteAPos, ATB);
            float distB = Vector2.Dot(new Vector2(b.x, b.y) - noteAPos, ATB);
            return distA.CompareTo(distB);
        }); return nextNotes;
    }

    // Modifies a Swing if Dot Notes are involved
    private static void CalculateDotStackSwingAngle(BeatCutData lastSwing, ref BeatCutData currentSwing)
    {
        // Get the first and last note based on array order
        float angle, altAngle, change, altChange;
        ColourNote firstNote = currentSwing.notesInCut[0];
        ColourNote lastNote = currentSwing.notesInCut[^1];

        int orientation = CutDirFromNoteToNote(firstNote, lastNote);
        int altOrientation = CutDirFromNoteToNote(lastNote, firstNote);

        angle = (currentSwing.sliceParity == Parity.Forehand) ? ForehandDict[orientation] : BackhandDict[orientation];
        altAngle = (currentSwing.sliceParity == Parity.Forehand) ? ForehandDict[altOrientation] : BackhandDict[altOrientation];

        change = lastSwing.endPositioning.angle - angle;
        altChange = lastSwing.endPositioning.angle - altAngle;


        if (Mathf.Abs(altChange) < Mathf.Abs(change)) angle = altAngle;

        currentSwing.SetStartAngle(angle);
        currentSwing.SetEndAngle(angle);
    }

    /// <summary>
    /// Given previous and current swing (singular dot note), calculate and clamp saber rotation.
    /// </summary>
    /// <param name="lastSwing">Last swing the player would have done</param>
    /// <param name="currentSwing">Swing you want to calculate swing angle for</param>
    /// <param name="clamp">True if you want to perform clamping on the angle</param>
    private static void CalculateDotDirection(BeatCutData lastSwing, ref BeatCutData currentSwing, bool clamp = true)
    {
        ColourNote dotNote = currentSwing.notesInCut[0];
        ColourNote lastNote = lastSwing.notesInCut[^1];

        int orientation = CutDirFromNoteToNote(lastNote, dotNote);

        // If same grid position, just maintain angle
        if (dotNote.x == lastNote.x && dotNote.y == lastNote.y)
        {
            orientation = opposingCutDict[orientation];
        }

        float angle = (lastSwing.sliceParity == Parity.Forehand) ?
            ForehandDict[orientation] :
            BackhandDict[orientation];

        if (clamp)
        {
            int xDiff = Math.Abs(dotNote.x - lastNote.x);
            int yDiff = Math.Abs(dotNote.y - lastNote.y);
            if (xDiff == 3) { angle = Math.Clamp(angle, -90, 90); }
            else if (yDiff == 0 && xDiff < 2) { angle = Math.Clamp(angle, -45, 45); }
            else if (yDiff > 0 && xDiff > 0) { angle = Math.Clamp(angle, -45, 45); }
        }

        currentSwing.SetStartAngle(angle);
        currentSwing.SetEndAngle(angle);

        return;
    }

    // Attempts to add bomb avoidance based on the isReset tag for a list of swings.
    // NOTE: To improve this, probably want bomb detection in its own function and these swings
    // would be added for each bomb in the sabers path rather then only for bomb resets.
    private List<BeatCutData> AddBombResetAvoidance(List<BeatCutData> swings)
    {
        List<BeatCutData> result = new(swings);
        int swingsAdded = 0;

        for (int i = 0; i < swings.Count - 1; i++)
        {
            // If Reset
            if (swings[i].IsReset)
            {
                // Reference to last swing
                BeatCutData lastSwing = swings[i - 1];
                BeatCutData currentSwing = swings[i];
                ColourNote lastNote = lastSwing.notesInCut[^1];
                ColourNote nextNote = currentSwing.notesInCut[0];

                // Time difference between last swing and current note
                float timeDifference = TimeUtils.BeatsToSeconds(_BPM, nextNote.b - lastNote.b);

                BeatCutData swing = new();
                swing.sliceParity = (currentSwing.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                swing.sliceStartBeat = lastSwing.sliceEndBeat + TimeUtils.SecondsToBeats(_BPM, timeDifference / 5);
                swing.sliceEndBeat = swing.sliceStartBeat + TimeUtils.SecondsToBeats(_BPM, timeDifference / 4);
                swing.SetStartPosition(lastNote.x, lastNote.y);

                // If the last hit was a dot, pick the opposing direction based on parity.
                float diff = currentSwing.startPositioning.angle - lastSwing.endPositioning.angle;
                float mid = diff / 2;
                mid += lastSwing.endPositioning.angle;

                // Set start and end angle, should be the same
                swing.SetStartAngle(mid);
                swing.SetEndAngle(mid);

                // Calculate the direction, set it, then insert this swing into the returned result list.
                Vector3 dir = Quaternion.Euler(0, 0, swing.startPositioning.angle) * Vector3.up;
                Vector2 endPosition = new(Mathf.Clamp(lastNote.x + dir.x, 0, 3), Mathf.Clamp(lastNote.y + dir.y, 0, 2));
                swing.SetEndPosition((int)endPosition.x, (int)endPosition.y);

                result.Insert(i + swingsAdded, swing);
                swingsAdded++;
            }
        }
        return result;
    }

    #endregion

    #region Helper Functions

    // Given 2 notes, gets the cut direction of the 2nd note based on the direction from first to last
    private static int CutDirFromNoteToNote(ColourNote firstNote, ColourNote lastNote)
    {
        Vector2 dir = (new Vector2(lastNote.x, lastNote.y) - new Vector2(firstNote.x, firstNote.y)).normalized;
        Vector2 lowestDotProduct = directionalVectors.OrderBy(v => Vector2.Dot(dir, v)).First();
        Vector2 cutDirection = new Vector2(Mathf.Round(lowestDotProduct.x), Mathf.Round(lowestDotProduct.y));
        int orientation = directionalVectorToCutDirection[cutDirection];
        return orientation;
    }

    // Given a cut direction ID, return angle from appropriate dictionary
    public static float AngleGivenCutDirection(int cutDirection, Parity parity)
    {
        return (parity == Parity.Forehand) ? ForehandDict[cutDirection] : BackhandDict[cutDirection];
    }

    // Returns cut direction given angle and parity
    public static int CutDirectionGivenAngle(float angle, Parity parity, float interval = 0.0f)
    {
        float roundedAngle = 0;
        if (interval != 0.0f)
        {
            float intervalTimes = angle / interval;
            roundedAngle = (float)((intervalTimes >= 0)
                ? Math.Floor(intervalTimes) * interval
                : Math.Ceiling(intervalTimes) * interval);
        }
        else
        {
            roundedAngle = MathF.Floor(angle / 45) * 45;
        }

        return (parity == Parity.Forehand) ?
            SliceMap.ForehandDict.FirstOrDefault(x => x.Value == roundedAngle).Key :
            SliceMap.BackhandDict.FirstOrDefault(x => x.Value == roundedAngle).Key;
    }

    // Determines if a Note is inverted
    private bool IsInvert(ColourNote lastNote, ColourNote nextNote)
    {
        // Is Note B in the direction of Note A's cutDirection.
        // There is 100% a more efficient method using dot product and vectors here for diagonals
        // Plus this does not account for bottom row ups and top row downs ect.. being "semi inverted"
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
        var seconds = TimeUtils.BeatsToSeconds(BPM, beatDiff);
        TimeSpan time = TimeSpan.FromSeconds(seconds);

        return (float)((60000 / time.TotalMilliseconds) / 2);
    }

    #endregion
}