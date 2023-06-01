using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RestrictedParityBehaviour : IParityMethod
{
    public bool UpsideDown => false;

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
        if (nextNote.d == 8) orient = SliceMap.CutDirectionGivenAngle(lastCut.endPositioning.angle, lastCut.sliceParity, 45.0f);

        float nextAFN = (lastCut.sliceParity == Parity.Forehand) ?
            SliceMap.BackhandDict[orient] :
            SliceMap.ForehandDict[orient];

        // Angle from neutral difference
        float angleChange = currentAFN - nextAFN;

        List<BeatGrid> intervalGrids = new();
        List<BombNote> bombsToAdd = new();
        const float timeSnap = 0.05f;

        // Construct play-space grid with bombs at a set interval of beats
        foreach (BombNote bomb in bombs.OrderBy(x => x.b))
        {
            if (bombsToAdd.Count == 0 || Mathf.Abs(bomb.b - bombsToAdd.First().b) <= timeSnap)
            {
                bombsToAdd.Add(bomb);
            }
            else
            {
                BeatGrid grid = new(bombsToAdd, bombsToAdd[0].b);
                intervalGrids.Add(grid);
                bombsToAdd.Clear();
                bombsToAdd.Add(bomb);
            }
        }

        if (bombsToAdd.Count > 0)
        {
            BeatGrid lastGrid = new(bombsToAdd, bombsToAdd[0].b);
            intervalGrids.Add(lastGrid);
        }

        Vector2 handPos = new(lastCut.endPositioning.x, lastCut.endPositioning.y);
        Parity bombParity = lastCut.sliceParity;
        for (int i = 0; i < intervalGrids.Count; i++)
        {
            int cutDir = (lastCut.notesInCut.All(x => x.d == 8)) ?
                SliceMap.CutDirectionGivenAngle(lastCut.endPositioning.angle, bombParity) :
                SliceMap.CutDirectionGivenAngle(lastCut.endPositioning.angle, bombParity, 45.0f);

            Vector3 result = intervalGrids[i].SaberUpdateCalc(handPos, cutDir, bombParity, playerXOffset);
            handPos.x = result.x;
            handPos.y = result.y;

            if (result.z > 0)
            {
                bombParity = (bombParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
            }

            if (i != intervalGrids.Count - 1) continue;

            bool bombResetIndicated = bombParity != lastCut.sliceParity;
            // Last grid, parity is settled, now we check if we need to reset?

            // If parity hasn't changed we likely didn't reset
            if (!bombResetIndicated) continue;


            if (nextNote.d != 8)
            {
                // If angle difference between the 2 cut directions is slim < 90,
                if (bombParity == Parity.Forehand && (!(MathF.Abs(angleChange) >= 90))) continue;
                if (bombParity == Parity.Backhand && (!(MathF.Abs(angleChange) >= 45))) continue;
            }

            if (currentSwing.notesInCut.All(x => x.d == 8) && currentSwing.notesInCut.Count == 1 && lastCut.notesInCut.All(x => x.d == 8) && lastCut.notesInCut.Count == 1)
            {
                float orientAngle = Mathf.Clamp(currentSwing.endPositioning.angle, -45, 45);
                currentSwing.SetStartAngle(orientAngle);
                currentSwing.SetEndAngle(orientAngle);
            }

            currentSwing.resetType = ResetType.Bomb;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }

        // If last cut is entirely dot notes and next cut is too, then parity is assumed to be maintained
        if (currentSwing.notesInCut.All(x => x.d == 8))
        {
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
        }

        float altNextAFN = (lastCut.sliceParity == Parity.Backhand) ?
            SliceMap.BackhandDict[orient] :
            SliceMap.ForehandDict[orient];

        if (MathF.Abs(currentAFN - altNextAFN) < 90)
        {
            currentSwing.resetType = ResetType.Normal;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }

        if (nextAFN > 90 
            || nextAFN < -135)
        {
            currentSwing.resetType = ResetType.Normal;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }

        // If the angle change exceeds 180 even after accounting for bigger rotations then triangle
        if (Mathf.Abs(angleChange) > 135)
        {
            currentSwing.resetType = ResetType.Normal;
            return (lastCut.sliceParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastCut.sliceParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
    }
}
