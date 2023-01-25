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

    private Dictionary<int, Func<ColourNote, int, int, bool>> _bombDetectionConditions = new()
    {
        { 0, (note,x,y) => ((y >= note.y && y == 2) || (y < note.y && y != 0)) && x == note.x },
        { 1, (note,x,y) => ((y <= note.y && y == 0) || (y < note.y && y != 2)) && x == note.x },
        { 2, (note,x,y) => y == note.y && x <= note.x  },
        { 3, (note,x,y) => y == note.y && y >= note.x },
        { 4, (note,x,y) => (y > note.y && x <= note.x) || (y >= note.y && x < note.x) || (y == note.y && x == note.x) },
        { 5, (note,x,y) => (y > note.y && x >= note.x) || (y >= note.y && x > note.x) || (y == note.y && x == note.x) },
        { 6, (note,x,y) => (y < note.y && x <= note.x) || (y <= note.y && x < note.x) || (y == note.y && x == note.x) },
        { 7, (note,x,y) => (y < note.y && x >= note.x) || (y <= note.y && x > note.x) || (y == note.y && x == note.x) },
        { 8, (note,x,y) => false }
    };

    public Parity ParityCheck(BeatCutData lastCut, ref BeatCutData currentSwing, List<BombNote> bombs, float playerXOffset, bool rightHand)
    {
        // AFN: Angle from neutral
        // Assuming a forehand down hit is neutral, and a backhand up hit
        // Rotating the hand inwards goes positive, and outwards negative
        // Using a list of definitions, turn cut direction into an angle, and check
        // if said angle makes sense.

        ColourNote nextNote = currentSwing.notesInCut[0];

        float angleChange = 0;
        float currentAFN = (lastCut.sliceParity != Parity.Forehand) ?
            SliceMap.BackhandDict[lastCut.notesInCut[0].d] :
            SliceMap.ForehandDict[lastCut.notesInCut[0].d];
        float nextAFN = (lastCut.sliceParity != Parity.Forehand) ?
            SliceMap.BackhandDict[nextNote.d] :
            SliceMap.ForehandDict[nextNote.d];

        angleChange = currentAFN - nextAFN;
        _upsideDown = false;

        #region Bomb Reset Checks

        // Structured so that successive bombs (for example, in a spiral) can be accounted for to some degree.
        // Alternates the orientation each time so if bombs appear in the opposite spots, it flips
        bool bombReset = false;
        for (int i = 0; i < bombs.Count; i++)
        {
            // Get current bomb
            BombNote bomb = bombs[i];
            ColourNote note;

            // Get the last note. In the case of a stack, picks the note that isnt at 2 or 0 as
            // it triggers a reset when it shouldn't.
            note = lastCut.notesInCut[^1];
            if(lastCut.notesInCut.Count > 1)
            {
                if (lastCut.endPositioning.angle == 0 && lastCut.sliceParity == Parity.Forehand) {
                    note = lastCut.notesInCut.First(x => x.y != 0);
                } else if(lastCut.endPositioning.angle == 0 && lastCut.sliceParity == Parity.Backhand) {
                    note = lastCut.notesInCut.First(x => x.y != 2);
                }
            }

            // Get the last notes cut direction based on the last swings angle
            var lastNoteCutDir = (lastCut.sliceParity == Parity.Forehand) ?
                SliceMap.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key :
                SliceMap.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPositioning.angle / 45.0) * 45).Key;

            // Use the dictionary to determine if a reset should be called given bomb position and note position
            bombReset = _bombDetectionConditions[lastNoteCutDir](note, bomb.x, bomb.y);
            if (bombReset) break;
        }

        if (bombReset)
        {
            // TEMP: This IF statement seemed to help catch dot spirals for now, but probably causes issues somewhere
            if ((rightHand && nextAFN > -180) || (!rightHand && nextAFN < 180))
            {
                // Set as bomb reset and return same parity as last swing
                currentSwing.resetType = ResetType.Bomb;
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
        }

        #endregion

        // Determines if potentially an upside down hit based on note cut direction and last swing angle
        if (lastCut.sliceParity == Parity.Backhand && lastCut.endPositioning.angle > 0 && (nextNote.d == 0 || nextNote.d == 8)) {
            _upsideDown = true;
        } else if(lastCut.sliceParity == Parity.Forehand && lastCut.endPositioning.angle > 0 && (nextNote.d == 1 || nextNote.d == 8)) {
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

        // If the angle change exceeds 180 even after accounting for bigger rotations then triangle
        if (Mathf.Abs(angleChange) > 180 && !UpsideDown)
        {
            currentSwing.resetType = ResetType.Normal;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
    }
}