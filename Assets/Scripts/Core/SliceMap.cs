using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Parity
{
    None,
    Forehand,
    Backhand,
    Reset
}

public struct PositioningData
{
    public float angle;
    public int x;
    public int y;
}

public struct BeatCutData
{
    public Parity sliceParity;
    public float sliceStartBeat;
    public float sliceEndBeat;
    public float swingEBPM;
    public PositioningData startPositioning;
    public PositioningData endPositioning;
    public List<ColourNote> notesInCut;
    public bool isReset;
    public bool isInverted;
}

// Adapted from Joshabi's ParityChecker
public class SliceMap
{
    // FOR RIGHT HAND (slightly mirr'd depending on hand
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
    private static readonly Dictionary<int, float> rightForehandDict = new Dictionary<int, float>()
    { { 0, -180 }, { 1, 0 }, { 2, -90 }, { 3, 90 }, { 4, -135 }, { 5, 135 }, { 6, -45 }, { 7, 45 }, { 8, 0 } };
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
    private static readonly Dictionary<int, float> rightBackhandDict = new Dictionary<int, float>()
    { { 0, 0 }, { 1, -180 }, { 2, 90 }, { 3, -90 }, { 4, 45 }, { 5, -45 }, { 6, 135 }, { 7, -135 }, { 8, 0 } };

    // FOR LEFT HAND
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
    private static readonly Dictionary<int, float> leftForehandDict = new Dictionary<int, float>()
    { { 0, -180 }, { 1, 0 }, { 2, 90 }, { 3, -90 }, { 4, 135 }, { 5, -135 }, { 6, 45 }, { 7, -45 }, { 8, 0 } };
    // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
    private static readonly Dictionary<int, float> leftBackhandDict = new Dictionary<int, float>()
    { { 0, 0 }, { 1, -180 }, { 2, -90 }, { 3, 90 }, { 4, -45 }, { 5, 45 }, { 6, -135 }, { 7, 135 }, { 8, 0 } };

    private static readonly List<int> forehandResetDict = new List<int>()
    { 1, 2, 3, 5, 6, 7, 8 };
    private static readonly List<int> backhandResetDict = new List<int>()
    { 0, 4, 5, 8 };

    private static Dictionary<int, float> ForehandDict { get { return rightForehandDict; } }//(_rightHand) ? rightForehandDict : leftForehandDict; } }
    private static Dictionary<int, float> BackhandDict { get { return rightBackhandDict; } }//return (_rightHand) ? rightBackhandDict : leftBackhandDict; } }

    private List<ColourNote> _blocks;
    private List<BombNote> _bombs;
    private List<BeatCutData> _cuts;
    private static bool _rightHand;
    private float _BPM = 0.0f;
    private int _playerXOffset = 0;

    public int GetSliceCount()
    {
        return _cuts.Count;
    }

    public BeatCutData GetBeatCutData(int index)
    {
        return _cuts[index];
    }

    public SliceMap(float inBPM, List<ColourNote> blocks, List<BombNote> bombs, bool isRightHand)
    {
        _BPM = inBPM;
        _blocks = new List<ColourNote>(blocks);
        _blocks.RemoveAll(x => isRightHand ? x.c == 0 : x.c == 1);
        _blocks.Sort((x, y) => x.b.CompareTo(y.b));
        _bombs = bombs;
        _bombs.Sort((x, y) => x.b.CompareTo(y.b));
        _cuts = GetCutData(_blocks, _bombs, isRightHand);
    }

    List<BeatCutData> GetCutData(List<ColourNote> notes, List<BombNote> bombs, bool isRightHand)
    {
        _rightHand = isRightHand;
        List<BeatCutData> result = new List<BeatCutData>();

        float sliderPrecision = 1 / 10f;
        List<ColourNote> notesInSwing = new List<ColourNote>();

        for (int i = 0; i < notes.Count - 1; i++)
        {
            ColourNote currentNote = notes[i];
            ColourNote nextNote = notes[i + 1];

            notesInSwing.Add(currentNote);

            // If precision falls under "Slider", or time stamp is the same, run
            // checks to figure out if it is a slider, window, stack ect..
            if (Mathf.Abs(currentNote.b - nextNote.b) <= sliderPrecision)
            {
                bool isStack = false;
                if (currentNote.d == nextNote.d) isStack = true;
                if (nextNote.d == 8) isStack = true;
                if (Mathf.Abs(ForehandDict[currentNote.d] - ForehandDict[nextNote.d]) <= 45) isStack = true;
                // For now hard coded to accept dot then arrow as correct no matter what
                if (notesInSwing[notesInSwing.Count-1].d == 8) isStack = true;
                if (isStack)
                {
                    continue;
                }
            }

            // Assume by default swinging forehanded
            BeatCutData sData = new BeatCutData();
            sData.notesInCut = notesInSwing;
            sData.sliceParity = Parity.Forehand;
            sData.sliceStartBeat = notesInSwing[0].b;
            sData.sliceEndBeat = notesInSwing[notesInSwing.Count - 1].b + 0.1f;
            sData.startPositioning.angle = rightForehandDict[notesInSwing[0].d];
            sData.startPositioning.x = notesInSwing[0].x;
            sData.startPositioning.y = notesInSwing[0].y;
            sData.endPositioning.angle = rightForehandDict[notesInSwing[notesInSwing.Count-1].d];
            sData.endPositioning.x = notesInSwing[notesInSwing.Count-1].x;
            sData.endPositioning.y = notesInSwing[notesInSwing.Count-1].y;


            if (result.Count == 0)
            {
                result.Add(sData);
                notesInSwing.Clear();
                continue;
            }
            else
            {
                // If previous swing exists
                BeatCutData lastSwing = result[result.Count-1];
                ColourNote lastNote = lastSwing.notesInCut[lastSwing.notesInCut.Count-1];

                // Get Walls Between the Swings
                /* List<Obstacle> wallsInbetween = dodgeWalls.FindAll(x => x._time > lastNote._time && x._time < notesInSwing[0]._time);
                if (wallsInbetween != null)
                {
                    foreach (var wall in wallsInbetween)
                    {
                        // Duck wall detection
                        if ((wall._width >= 3 && (wall._lineIndex <= 1)) || (wall._width == 2 && wall._lineIndex == 1))
                        {
                            //Console.WriteLine($"Detected Duck wall at: {wall._time}");
                            playerVerticalOffset = -1;
                            lastCrouchTimestamp = wall._time + wall._duration;
                        }

                        // Dodge wall detection
                        if (wall._lineIndex == 1 || wall._lineIndex == 2)
                        {
                            //Console.WriteLine($"Detected Dodge Wall at: {wall._time}");
                            playerHorizontalOffset = (wall._lineIndex == 1) ? 1 : -1;
                            lastWallTimestamp = wall._time + wall._duration;
                        }
                    }
                }*/

                // If time since dodged last exceeds a set amount in Seconds (might convert to ms
                // for everything down the line tbh), undo dodge
                /*var wallEndCheckTime = 0.5f;
                if (BeatToSeconds(curMapBPM, notesInSwing[0]._time - lastWallTimestamp) > wallEndCheckTime)
                {
                    playerHorizontalOffset = 0;
                }
                if (BeatToSeconds(curMapBPM, notesInSwing[0]._time - lastCrouchTimestamp) > wallEndCheckTime)
                {
                    playerVerticalOffset = 0;
                }*/

                //sData.curPlayerHorizontalOffset = playerHorizontalOffset;
                //sData.curPlayerVerticalOffset = playerVerticalOffset;
                sData.swingEBPM = SwingEBPM(_BPM, currentNote.b - lastNote.b);
                if (sData.isReset) { sData.swingEBPM *= 2; }

                // Work out Parity
                List<BombNote> bombsBetweenSwings = bombs.FindAll(x => x.b > lastNote.b && x.b < notesInSwing[notesInSwing.Count-1].b);
                sData.sliceParity = ParityCheck(lastSwing, notesInSwing[0], bombsBetweenSwings);
                if (sData.sliceParity == Parity.Backhand)
                {
                    sData.startPositioning.angle = rightBackhandDict[notesInSwing[0].d];
                    sData.endPositioning.angle = rightBackhandDict[notesInSwing[notesInSwing.Count - 1].d];
                }
                if (sData.sliceParity == lastSwing.sliceParity) { sData.isReset = true; }

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
            }
            // Add swing to list
            result.Add(sData);
            notesInSwing.Clear();
        }
        return result;
    }

    public Parity ParityCheck(BeatCutData lastCut, ColourNote nextNote, List<BombNote> bombs)
    {
        // AFN: Angle from neutral
        // Assuming a forehand down hit is neutral, and a backhand up hit
        // Rotating the hand inwards goes positive, and outwards negative
        // Using a list of definitions, turn cut direction into an angle, and check
        // if said angle makes sense.
        var nextAFN = (lastCut.sliceParity != Parity.Forehand) ?
            BackhandDict[lastCut.notesInCut[0].d] - ForehandDict[nextNote.d] :
            ForehandDict[lastCut.notesInCut[0].d] - BackhandDict[nextNote.d];

        var currentAFN = (lastCut.sliceParity == Parity.Forehand) ?
            BackhandDict[lastCut.notesInCut[0].d] :
            ForehandDict[lastCut.notesInCut[0].d];

        // Checks if either bomb reset bomb locations exist
        var bombCheckLayer = (lastCut.sliceParity == Parity.Forehand) ? 0 : 2;
        bool containsRightmost = bombs.FindIndex(x => x.x == 2 + _playerXOffset && x.y == bombCheckLayer) != -1;
        bool containsLeftmost = bombs.FindIndex(x => x.x == 1 + _playerXOffset && x.y == bombCheckLayer) != -1;

        // If there is a bomb, potentially a bomb reset
        if ((_rightHand && containsLeftmost) || (!_rightHand && containsRightmost))
        {
            List<int> resetDirectionList = (lastCut.sliceParity == Parity.Forehand) ? forehandResetDict : backhandResetDict;
            if (resetDirectionList.Contains(lastCut.notesInCut[0].d))
            {
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }

        // If the next AFN exceeds 180 or -180, this means the algo had to triangle / reset
        if (nextAFN > 180 || nextAFN < -180)
        {
            //Console.WriteLine($"Attempted: {BackhandDict[lastSwing.notes[0]._cutDirection] - ForehandDict[nextNote._cutDirection]} or {ForehandDict[lastSwing.notes[0]._cutDirection] - BackhandDict[nextNote._cutDirection]}" +
            //    $"\n[PARITY WARNING] >> Had to Triangle at {nextNote._time} with an Angle from Neutral of {nextAFN}." +
            //    $"\nLast swing was {lastSwing.swingParity} and current player offset is {playerHorizontalOffset}");
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
    }
    private static bool IsInvert(ColourNote lastNote, ColourNote nextNote)
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
    private static float GetCurvePointValue(float[][] curvePoints, float keyValue)
    {
        for (int i = 0; i < curvePoints.Length; i++)
        {
            if (keyValue > curvePoints[i][0] &&
                keyValue <= curvePoints[i + 1][0])
            {
                var reduction = keyValue / curvePoints[i + 1][0];
                return (curvePoints[i + 1][1] * reduction);
            }
            else if (keyValue < curvePoints[0][0]) { return curvePoints[0][1]; }
            else if (keyValue > curvePoints[curvePoints.Length-1][0]) { return curvePoints[curvePoints.Length-1][1]; }
        }
        return 0;
    }

    private float SwingEBPM(float BPM, float beatDiff)
    {
        var seconds = BeatToSeconds(BPM, beatDiff);
        TimeSpan time = TimeSpan.FromSeconds(seconds);

        return (float)((60000 / time.TotalMilliseconds) / 2);
    }
    public static float DiffNormalize(float min, float max, float value, float maxScale = 1)
    {
        return (value - min) / (max - min) * maxScale;
    }
    public static float BeatToSeconds(float BPM, float beatDiff)
    {
        return (beatDiff / (BPM / 60));
    }
}