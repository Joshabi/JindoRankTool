using System.Collections.Generic;
using UnityEngine;

public interface IParityMethod {
    Parity ParityCheck(BeatCutData lastCut, ColourNote nextNote, List<BombNote> bombs, float playerXOffset, bool rightHand);
}

public class DefaultParityCheck : IParityMethod
{
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

            // Set the angle tolerance
            float angleTolerance = 90;
            if (lastCut.sliceParity == Parity.Backhand)
            {
                angleTolerance = 45;
            }

            // If the swing falls under the angle tolerance, we predict its a bomb reset
            // This catches bomb reset detection involving dot notes.
            if (Mathf.Abs(lastCut.startPositioning.angle) <= angleTolerance)
            {
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
        }
        // If the next AFN exceeds 180 or -180, this means the algo had to triangle / reset
        if (Mathf.Abs(nextAFN) > 180)
        {
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
    }
}