using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IParityMethod {
    Parity ParityCheck(BeatCutData lastCut, ref BeatCutData currentSwing, List<BombNote> bombs, float playerXOffset, bool rightHand);
    bool UpsideDown { get; }
}

public class DefaultParityCheck : IParityMethod
{
    public bool UpsideDown { get { return _upsideDown; } }
    private bool _upsideDown;

    // Returns true if the inputted note and bomb coordinates cause a reset potentially
    private Dictionary<int, Func<Vector2, int, int, bool>> _bombDetectionConditions = new()
    {
        { 0, (note, x, y) => ((y >= note.y && y != 0) || (y > note.y && y > 0)) && x == note.x },
        { 1, (note, x, y) => ((y <= note.y && y != 2) || (y < note.y && y < 2)) && x == note.x },
        { 2, (note, x, y) => ((y == note.y) || (y == note.y - 1)) && x <= note.x },
        { 3, (note, x, y) => ((y == note.y) || (y == note.y - 1)) && x >= note.x },
        { 4, (note, x, y) => y == note.y && x == note.x },
        { 5, (note, x, y) => y == note.y && x == note.x },
        { 6, (note, x, y) => x == note.x && y == note.y },
        { 7, (note, x, y) => x == note.x && y <= note.y },
        { 8, (note,x,y) => false }
    };

    public bool BombResetCheck(BeatCutData lastCut, List<BombNote> bombs)
    {
        // Not found yet
        bool bombReset = false;
        for (int i = 0; i < bombs.Count; i++)
        {
            // Get current bomb
            BombNote bomb = bombs[i];
            ColourNote note;

            // If in the center 2 grid spaces, no point trying
            if ((bomb.x == 1 || bomb.x == 2) && bomb.y == 1) continue;

            // Get the last note. In the case of a stack, picks the note that isnt at 2 or 0 as
            // it triggers a reset when it shouldn't.
            note = lastCut.notesInCut[^1];
            if (lastCut.notesInCut.Count > 1)
            {
                if (lastCut.endPositioning.angle == 0 && lastCut.sliceParity == Parity.Forehand)
                {
                    note = lastCut.notesInCut.FirstOrDefault(x => x.y != 0);
                }
                else if (lastCut.endPositioning.angle == 0 && lastCut.sliceParity == Parity.Backhand)
                {
                    note = lastCut.notesInCut.FirstOrDefault(x => x.y != 2);
                }
            }

            // Get the last notes cut direction based on the last swings angle
            var lastNoteCutDir = (lastCut.sliceParity == Parity.Forehand) ?
                SliceMap.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key :
                SliceMap.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key;

            // Offset the checking if the entire outerlane is full of bombs
            int xOffset = 0;
            if (note.x == 0)
            {
                bool a = bombs.Where(x => x.x == 0)
                    .Where(x => x.y == 1 || x.y == 2 || x.y == 3).Any();
                if (a) xOffset = 1;
            }
            else if (note.y == 1)
            {
                bool a = bombs.Where(x => x.x == 3)
                    .Where(x => x.y == 1 || x.y == 2 || x.y == 3).Any();
                if (a) xOffset = -1;
            }

            // Determine if lastnote and current bomb cause issue
            // If we already found reason to reset, no need to try again
            bombReset = _bombDetectionConditions[lastNoteCutDir](new Vector2(note.x + xOffset, note.y), bomb.x, bomb.y);
            if (bombReset) return true;
        }
        return false;
    }

    public Parity ParityCheck(BeatCutData lastCut, ref BeatCutData currentSwing, List<BombNote> bombs, float playerXOffset, bool rightHand)
    {
        // AFN: Angle from neutral
        // Assuming a forehand down hit is neutral, and a backhand up hit
        // Rotating the hand inwards goes positive, and outwards negative
        // Using a list of definitions, turn cut direction into an angle, and check
        // if said angle makes sense.

        ColourNote nextNote = currentSwing.notesInCut[0];

        float currentAFN = (lastCut.sliceParity != Parity.Forehand) ?
            SliceMap.BackhandDict[lastCut.notesInCut[0].d] :
            SliceMap.ForehandDict[lastCut.notesInCut[0].d];

        int orient = nextNote.d;
        if(nextNote.d == 8) orient = (lastCut.sliceParity == Parity.Forehand) ?
                SliceMap.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key :
                SliceMap.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key;

        float nextAFN = (lastCut.sliceParity != Parity.Forehand) ?
            SliceMap.BackhandDict[orient] :
            SliceMap.ForehandDict[orient];

        float angleChange = currentAFN - nextAFN;
        _upsideDown = false;

        // Determines if potentially an upside down hit based on note cut direction and last swing angle
        if (lastCut.sliceParity == Parity.Backhand && lastCut.endPositioning.angle > 0 && (nextNote.d == 0 || nextNote.d == 8)) {
            _upsideDown = true;
        }
        else if (lastCut.sliceParity == Parity.Forehand && lastCut.endPositioning.angle > 0 && (nextNote.d == 1 || nextNote.d == 8)) {
            _upsideDown = true;
        }

        // Alters big rotation swings to fall under the triangle condition below.
        // This works in the anticlockwise direction. Somehow, triangling still works.
        if (currentAFN > 0) {
            if (angleChange > 180 && !UpsideDown)
            { angleChange -= 180; }
        } else if (currentAFN < 0) {
            if (angleChange < -180 && !UpsideDown)
            { angleChange += 180; }
        }

        // Check for potential bomb resets
        bool bombReset = BombResetCheck(lastCut, bombs);

        if (bombReset)
        {
            if(Mathf.Abs(angleChange) < 180)
            {
                // Set as bomb reset and return same parity as last swing
                currentSwing.resetType = ResetType.Bomb;
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
        }

        // If the angle change exceeds 180 even after accounting for bigger rotations then triangle
        if (Mathf.Abs(angleChange) > 180 && !UpsideDown)
        {
            currentSwing.resetType = ResetType.Normal;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
    }
}