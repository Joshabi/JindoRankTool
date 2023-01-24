using System.Collections.Generic;
using UnityEngine;

public class ResetParityCheck : IParityMethod
{
    public bool UpsideDown { get { return _upsideDown; } }
    private bool _upsideDown;

    public Parity ParityCheck(BeatCutData lastCut, ColourNote nextNote, List<BombNote> bombs, float playerXOffset, bool rightHand)
    {
        // AFN: Angle from neutral
        // Assuming a forehand down hit is neutral, and a backhand up hit
        // Rotating the hand inwards goes positive, and outwards negative
        // Using a list of definitions, turn cut direction into an angle, and check
        // if said angle makes sense.
        var nextAFN = (lastCut.sliceParity != Parity.Forehand) ?
            SliceMap.BackhandDict[lastCut.notesInCut[0].d] - SliceMap.ForehandDict[nextNote.d] :
            SliceMap.ForehandDict[lastCut.notesInCut[0].d] - SliceMap.BackhandDict[nextNote.d];

        // Checks if either bomb reset bomb locations exist
        var bombCheckLayer = (lastCut.sliceParity == Parity.Forehand) ? 0 : 2;
        bool containsRightmost = bombs.FindIndex(x => x.x == 2 + playerXOffset && x.y == bombCheckLayer) != -1;
        bool containsLeftmost = bombs.FindIndex(x => x.x == 1 + playerXOffset && x.y == bombCheckLayer) != -1;

        // If there is a bomb, potentially a bomb reset
        if ((!rightHand && containsLeftmost) || (rightHand && containsRightmost))
        {
            List<int> resetDirectionList = (lastCut.sliceParity == Parity.Forehand) ? SliceMap.forehandResetDict : SliceMap.backhandResetDict;
            if (resetDirectionList.Contains(lastCut.notesInCut[0].d))
            {
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
        }

        // If note is a dot, play it as a down hit
        if (nextNote.d == 8 && nextNote.y == 0) { return Parity.Forehand; }
        // If note is a left or right hit in the outerlane, hit as down
        if ((nextNote.d == 3 || nextNote.d == 4) && (nextNote.x == 0 || nextNote.x == 3) && (nextNote.y == 0)) { return Parity.Forehand; }

        // If the next AFN exceeds 180 or -180, this means the algo had to triangle / reset
        if (Mathf.Abs(nextAFN) > 90)
        {
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
    }
}
