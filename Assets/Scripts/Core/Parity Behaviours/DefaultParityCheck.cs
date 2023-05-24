using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IParityMethod {
    Parity ParityCheck(BeatCutData lastCut, ref BeatCutData currentSwing, List<BombNote> bombs, int playerXOffset, bool rightHand, float timeTillNextNote = 0.1f);
    bool UpsideDown { get; }
}

public class DefaultParityCheck : IParityMethod
{
    public bool UpsideDown { get { return _upsideDown; } }
    private bool _upsideDown;

    // Returns true if the inputted note and bomb coordinates cause a reset potentially
    private readonly Dictionary<int, Func<Vector2, int, int, Parity, bool>> _bombDetectionConditions = new()
    {
        { 0, (note, x, y, parity) => ((y >= note.y && y != 0) || (y > note.y && y > 0)) && x == note.x },
        { 1, (note, x, y, parity) => ((y <= note.y && y != 2) || (y < note.y && y < 2)) && x == note.x },
        { 2, (note, x, y, parity) => (parity == Parity.Forehand && (y == note.y || y == note.y - 1) && ((note.x != 3 && x < note.x) || (note.x < 3 && x <= note.x))) ||
            (parity == Parity.Backhand && y == note.y && ((note.x != 0 && x < note.x) || (note.x > 0 && x <= note.x))) },
        { 3, (note, x, y, parity) => (parity == Parity.Forehand && (y == note.y || y == note.y - 1) && ((note.x != 0 && x > note.x) || (note.x > 0 && x >= note.x))) ||
            (parity == Parity.Backhand && y == note.y && ((note.x != 3 && x > note.x) || (note.x < 3 && x >= note.x))) },
        { 4, (note, x, y, parity) => ((y >= note.y && y != 0) || (y > note.y && y > 0)) && x == note.x && x != 3 && parity != Parity.Forehand },
        { 5, (note, x, y, parity) => ((y >= note.y && y != 0) || (y > note.y && y > 0)) && x == note.x && x != 0 && parity != Parity.Forehand },
        { 6, (note, x, y, parity) => ((y <= note.y && y != 2) || (y < note.y && y < 2)) && x == note.x && x != 3 && parity != Parity.Backhand },
        { 7, (note, x, y, parity) => ((y <= note.y && y != 2) || (y < note.y && y < 2)) && x == note.x && x != 0 && parity != Parity.Backhand },
        { 8, (note,x,y, parity) => false }
    };

    public bool BombResetCheck(BeatCutData lastCut, List<BombNote> bombs, int xPlayerOffset)
    {
        // Not found yet
        bool bombResetIndicated = false;
        for (int i = 0; i < bombs.Count; i++)
        {
            // Get current bomb
            BombNote bomb = bombs[i];
            ColourNote note;

            // If in the center 2 grid spaces, no point trying
            if ((bomb.x == 1 || bomb.x == 2) && bomb.y == 1) continue;

            // Get the last note. In the case of a stack, picks the note that isnt at 2 or 0 as
            // it triggers a reset when it shouldn't.

            note = lastCut.notesInCut.Where(note => note.x == lastCut.endPositioning.x && note.y == lastCut.endPositioning.y).FirstOrDefault();

            // Get the last notes cut direction based on the last swings angle
            var lastNoteCutDir = (lastCut.sliceParity == Parity.Forehand) ?
                SliceMap.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.startPositioning.angle / 45.0) * 45).Key :
                SliceMap.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.startPositioning.angle / 45.0) * 45).Key;

            // Offset the checking if the entire outerlane bombs indicate moving inwards
            int xOffset = 0;

            bool bombOffsetting = bombs.Any(bomb => bomb.x == note.x && (bomb.y <= note.y && lastCut.sliceParity == Parity.Backhand && lastCut.endPositioning.angle >= 0)) ||
                bombs.Any(bomb => bomb.x == note.x && (bomb.y >= note.y && lastCut.sliceParity == Parity.Forehand && lastCut.endPositioning.angle >= 0));

            if (bombOffsetting && note.x == 0) xOffset = 1;
            if (bombOffsetting && note.x == 3) xOffset = -1;

            // Determine if lastnote and current bomb cause issue
            // If we already found reason to reset, no need to try again
            bombResetIndicated = _bombDetectionConditions[lastNoteCutDir](new Vector2(note.x, note.y), bomb.x - (xPlayerOffset*2) - xOffset, bomb.y, lastCut.sliceParity);
            if (bombResetIndicated) return true;
        }
        return false;
    }

    public Parity ParityCheck(BeatCutData lastCut, ref BeatCutData currentSwing, List<BombNote> bombs, int playerXOffset, bool rightHand, float timeTillNextNote = 0.1f)
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
                SliceMap.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key :
                SliceMap.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key;

        float nextAFN = (lastCut.sliceParity == Parity.Forehand) ?
            SliceMap.BackhandDict[orient] :
            SliceMap.ForehandDict[orient];

        // Angle from neutral difference
        float angleChange = currentAFN - nextAFN;
        _upsideDown = false;

        // Determines if potentially an upside down hit based on note cut direction and last swing angle
        if (lastCut.sliceParity == Parity.Backhand && lastCut.endPositioning.angle > 0 && (nextNote.d == 0 || nextNote.d == 8)) {
            _upsideDown = true;
        }
        else if (lastCut.sliceParity == Parity.Forehand && lastCut.endPositioning.angle > 0 && (nextNote.d == 1 || nextNote.d == 8)) {
            _upsideDown = true;
        }

        // Check if bombs are in the position to indicate a reset
        bool bombResetIndicated = BombResetCheck(lastCut, bombs, playerXOffset);

        // Want to do a seconday check:
        // Checks whether resetting will cause another reset, which helps to catch some edge cases
        // in bomb detection where it triggers for decor bombs.
        bool bombResetParityImplied = false;
        if (bombResetIndicated) {
            if (nextNote.d == 8 && lastCut.notesInCut.All(x => x.d == 8)) bombResetParityImplied = true;
            else {
                // In case of dots, calculate using previous swing swing-angle
                int altOrient = (lastCut.sliceParity == Parity.Forehand) ?
                        SliceMap.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key :
                        SliceMap.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key;

                if (lastCut.sliceParity == Parity.Forehand)
                {
                    if (Mathf.Abs(SliceMap.ForehandDict[altOrient] + SliceMap.BackhandDict[nextNote.d]) >= 90) { bombResetParityImplied = true; }
                } else
                {
                    if (Mathf.Abs(SliceMap.BackhandDict[altOrient] + SliceMap.ForehandDict[nextNote.d]) >= 90) { bombResetParityImplied = true; }
                }
            }
        }

        // If bomb reset indicated and direction implies, then reset
        if (bombResetIndicated && bombResetParityImplied) {
            currentSwing.resetType = ResetType.Bomb;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }

        // If last cut is entirely dot notes and next cut is too, then parity is assumed to be maintained
        if (lastCut.notesInCut.All(x => x.d == 8) && currentSwing.notesInCut.All(x => x.d == 8)) {
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
        }

        // If time exceeds a certain amount, just reset. This will be made way harsher for the less
        // parity strict mode applied to maps like routine ect...
        if (timeTillNextNote > 3) {
            currentSwing.resetType = ResetType.Normal;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }

        // AKA, If a 180 anticlockwise (right) clockwise (left) rotation
        // FIXES ISSUES with uhh, some upside down hits?
        if (lastCut.endPositioning.angle == 180)
        {
            var altNextAFN = 180 + nextAFN;
            if (altNextAFN >= 0)
            {
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
            }
            else
            {
                currentSwing.resetType = ResetType.Normal;
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
        }

        // If the angle change exceeds 180 even after accounting for bigger rotations then triangle
        if (Mathf.Abs(angleChange) > 270 && !UpsideDown)
        {
            currentSwing.resetType = ResetType.Normal;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
    }
}