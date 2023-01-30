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
            
            // Re-order the notesInCut in the event all the notes are on the same snap and not dots
            if(notesInSwing.All(x => x.d != 8) && notesInSwing.Count > 1)
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
                if (Vector2.Dot(noteACutVector, ATB) < 0) {
                    ATB = -ATB;   // B before A
                } 

                // Sort the cubes according to their position along the direction vector
                notesInSwing.Sort((a, b) => Vector2.Dot(new Vector2(a.x, a.y) - new Vector2(noteA.x, noteA.y), ATB).CompareTo(Vector2.Dot(new Vector2(b.x, b.y) - new Vector2(noteA.x, noteA.y), ATB)));
            }

            // Assume by default swinging forehanded
            BeatCutData sData = new BeatCutData();
            sData.notesInCut = new List<ColourNote>(notesInSwing);
            sData.sliceParity = Parity.Forehand;
            sData.sliceStartBeat = notesInSwing[0].b;
            sData.sliceEndBeat = notesInSwing[^1].b + 0.1f;
            sData.SetStartPosition(notesInSwing[0].x, notesInSwing[0].y);
            sData.SetEndPosition(notesInSwing[^1].x, notesInSwing[^1].y);

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

            // Re-order the notesInCut in the event all the notes are dots and same snap
            if (sData.notesInCut.Count > 1 && sData.notesInCut.All(x => x.d == 8)) {
                sData.notesInCut = new(DotStackSort(lastSwing, sData.notesInCut));
                sData.SetStartPosition(notesInSwing[0].x, notesInSwing[0].y);
                sData.SetEndPosition(notesInSwing[^1].x, notesInSwing[^1].y);
            }

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
            if (TimeUtils.BeatsToSeconds(_BPM, notesInSwing[^1].b - _lastWallTime) > undodgeCheckTime) { _playerXOffset = 0; }

            // Work out Parity
            List<BombNote> bombsBetweenSwings = bombs.FindAll(x => x.b > lastNote.b && x.b < notesInSwing[^1].b);

            // Perform dot checks depending on swing composition.
            if (sData.notesInCut.All(x => x.d == 8) && sData.notesInCut.Count > 1) CalculateDotStackSwingAngle(lastSwing, ref sData);
            if (sData.notesInCut[0].d == 8 && sData.notesInCut.Count == 1) CalculateDotDirection(lastSwing, ref sData);

            sData.sliceParity = _parityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, _playerXOffset, _rightHand);

            // Depending on parity, set angle
            if (sData.notesInCut.Any(x => x.d != 8))
            {
                if (sData.sliceParity == Parity.Backhand)
                {
                    sData.SetStartAngle(BackhandDict[notesInSwing[0].d]);
                    sData.SetEndAngle(BackhandDict[notesInSwing[^1].d]);
                }
                else
                {
                    sData.SetStartAngle(ForehandDict[notesInSwing[0].d]);
                    sData.SetEndAngle(ForehandDict[notesInSwing[^1].d]);
                }
            }

            // If parity is the same as before and not flagged as a bomb reset.
            // LATER: Add logic to determine if adding a swing or rolling is the better option.
            if (sData.sliceParity == lastSwing.sliceParity && sData.resetType != ResetType.Bomb) { sData.resetType = ResetType.Normal; }

            // If current parity method thinks we are upside down and not dot notes in next hit, flip values.
            // This catch is in place to turn -180 into 180 (because the dictionary only has a definition from all the way around
            // in one direction (which is -180)
            if (_parityMethodology.UpsideDown == true && sData.notesInCut.All(x => x.d != 8))
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
        return result;
    }

    #region Dots and Bombs Checks

    private List<ColourNote> DotStackSort(BeatCutData lastSwing, List<ColourNote> nextNotes) {

        // Find the two notes that are furthest apart
        var furthestNotes = (from c1 in nextNotes
                             from c2 in nextNotes
                             orderby Vector3.Distance(new Vector2(c1.x, c1.y), new Vector2(c2.x, c2.y)) descending
                             select new { c1, c2 }).First();

        ColourNote noteA = furthestNotes.c1;
        ColourNote noteB = furthestNotes.c2;
        Vector2 noteAPos = new(noteA.x, noteA.y);
        Vector2 noteBPos = new(noteB.x, noteB.y);

        // Get the direction vector from noteA to noteB
        Vector2 ATB = noteBPos - noteAPos;

        // Incase the last note was a dot, turn the swing angle into the closest cut direction based on last swing parity
        int lastNoteClosestCutDir = ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastSwing.startPositioning.angle / 45.0) * 45).Key;

        // Convert the cut direction to a directional vector then do the dot product between noteA to noteB and last swing direction
        Vector2 noteACutVector = directionalVectorToCutDirection.FirstOrDefault(x => x.Value == opposingCutDict[lastNoteClosestCutDir]).Key;

        if (Vector2.Dot(noteACutVector, ATB) < 0) {
            ATB = -ATB;
        }

        // Sort the cubes according to their position along the direction vector
        nextNotes.Sort((a, b) => Vector2.Dot(new Vector2(a.x, a.y) - new Vector2(noteA.x, noteA.y), ATB).CompareTo(Vector2.Dot(new Vector2(b.x, b.y) - new Vector2(noteA.x, noteA.y), ATB)));
        return nextNotes;
    }

    // Modifies a Swing if Dot Notes are involved
    private void CalculateDotStackSwingAngle(BeatCutData lastSwing, ref BeatCutData currentSwing)
    {
        // Get the first and last note based on beats
        float angle;
        ColourNote firstNote = currentSwing.notesInCut[0];
        ColourNote lastNote = currentSwing.notesInCut[^1];

        int orientation = CutDirFromNoteToNote(firstNote, lastNote);

        // Okay so originally i was using the dictionary corrosponding to parity,
        // but that just caused issues and I gave up so I put in some catches below to
        // stop it having a fit.
        angle = ForehandDict[orientation];

        if (firstNote.x > lastSwing.notesInCut[^1].x && angle == -90 && lastSwing.sliceParity == Parity.Backhand) angle *= -1;
        if (firstNote.x > lastSwing.notesInCut[^1].x && angle == 90 && lastSwing.sliceParity == Parity.Forehand) angle *= -1;
        if (angle == -180 || angle == 180) angle = 0;

        currentSwing.SetStartAngle(angle);
        currentSwing.SetEndAngle(angle);
    }

    // Calculates how a dot note should be swung according to the prior swing.
    private void CalculateDotDirection(BeatCutData lastSwing, ref BeatCutData currentSwing)
    {
        ColourNote dotNote = currentSwing.notesInCut[0];
        ColourNote lastNote = lastSwing.notesInCut[^1];

        int orientation = CutDirFromNoteToNote(lastNote, dotNote);

        if (dotNote.x == lastNote.x && dotNote.y == lastNote.y) {
            orientation = opposingCutDict[orientation];
        }

        float angle = (lastSwing.sliceParity == Parity.Backhand) ?
            BackhandDict[orientation] :
            ForehandDict[orientation];

        if (lastSwing.endPositioning.angle == 0 && angle == -180) angle = 0;

        // Checks for angle based on X difference between the 2 notes
        float xDiff = Mathf.Abs(dotNote.x - lastNote.x);
        if (xDiff < 3) angle = Mathf.Clamp(angle, -90, 45);
        if (xDiff == 3) angle = Mathf.Clamp(angle, -90, 90);

        // Clamps inwards backhand hits if the note is only 1 away
        if (xDiff == 1 && lastNote.x > dotNote.x && _rightHand && lastSwing.sliceParity == Parity.Forehand) angle = 0;
        else if (xDiff == 1 && lastNote.x < dotNote.x && !_rightHand && lastSwing.sliceParity == Parity.Forehand) angle = 0;

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
                emptySwing.sliceStartBeat = swings[i - 1].sliceEndBeat + TimeUtils.SecondsToBeats(_BPM, 0.15f);
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
    // Given 2 notes, gets the cut direction of the 2nd note based on the direction from first to last
    private int CutDirFromNoteToNote(ColourNote firstNote, ColourNote lastNote)
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
        var seconds = TimeUtils.BeatsToSeconds(BPM, beatDiff);
        TimeSpan time = TimeSpan.FromSeconds(seconds);

        return (float)((60000 / time.TotalMilliseconds) / 2);
    }

    #endregion
}