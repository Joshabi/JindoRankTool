using System.Collections.Generic;
using UnityEngine;

public interface IParityMethod {
    Parity ParityCheck(BeatCutData lastCut, ref BeatCutData currentSwing, List<BombNote> bombs, float playerXOffset, bool rightHand);
    bool UpsideDown { get; }
}

public class DefaultParityCheck : IParityMethod
{
    public bool UpsideDown { get { return _upsideDown; } }
    private bool _upsideDown;

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

        angleChange = (lastCut.sliceParity != Parity.Forehand) ?
            SliceMap.BackhandDict[lastCut.notesInCut[0].d] - SliceMap.ForehandDict[nextNote.d] :
            SliceMap.ForehandDict[lastCut.notesInCut[0].d] - SliceMap.BackhandDict[nextNote.d];

        _upsideDown = false;

        #region Bomb Reset Checks

        // Checks if either bomb reset bomb locations exist
        var bombCheckLayer = (lastCut.sliceParity == Parity.Forehand) ? 0 : 2;
        bool containsRightmost = bombs.FindIndex(x => x.x == 2 + playerXOffset && x.y == bombCheckLayer 
        && Mathf.Abs(x.b - lastCut.notesInCut[^1].b) !<= 0.15 && Mathf.Abs(x.b - nextNote.b) !<= 0.15) != -1;
        bool containsLeftmost = bombs.FindIndex(x => x.x == 1 + playerXOffset && x.y == bombCheckLayer 
        && Mathf.Abs(x.b - lastCut.notesInCut[^1].b) !<= 0.15 && Mathf.Abs(x.b - nextNote.b)! <= 0.15) != -1;

        // If there is a bomb, potentially a bomb reset
        if ((!rightHand && containsLeftmost) || (rightHand && containsRightmost))
        {
            // First check 
            /*List<int> resetDirectionList = (lastCut.sliceParity == Parity.Forehand) ? SliceMap.forehandResetDict : SliceMap.backhandResetDict;
            if (resetDirectionList.Contains(lastCut.notesInCut[0].d))
            {
                currentSwing.resetType = ResetType.Bomb;
                return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }*/

            // Set the angle tolerance
            float angleTolerance = 90;

            // If the swing falls under the angle tolerance, we predict its a bomb reset
            // This catches bomb reset detection involving dot notes.
            if (Mathf.Abs(lastCut.endPositioning.angle) <= angleTolerance)
            {
                if ((lastCut.sliceParity == Parity.Forehand && nextNote.y > 0 && nextNote.d == 8)
                    || (lastCut.sliceParity == Parity.Backhand && nextNote.y < 2 && nextNote.d == 8))
                {
                    // If a dot note thats above or below the interactive bombs, and its dots, just swing
                    // around the bombs
                    return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                } else if ((lastCut.sliceParity == Parity.Forehand && nextNote.y > 0 && SliceMap.BackhandDict[nextNote.d] > 45)
                   || (lastCut.sliceParity == Parity.Backhand && nextNote.y < 2 && SliceMap.ForehandDict[nextNote.d] > 45)) {
                    // Not a reset, can be swung with parity based on note orientation?
                    return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                } else {
                    currentSwing.resetType = ResetType.Bomb;
                    return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
                }
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