using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

/// <summary>
/// Representation of the grid
/// </summary>
/// <summary>
/// Representation of the grid
/// </summary>
public class BeatGrid
{
    private readonly Dictionary<Vector2, Vector2> _positionToAvoidanceVector = new()
    {
        { new Vector2(0, 0), new Vector2(1, 1) },
        { new Vector2(0, 1), new Vector2(1, 0) },
        { new Vector2(0, 2), new Vector2(1, -1) },
        { new Vector2(1, 0), new Vector2(0, 2) },
        { new Vector2(1, 1), new Vector2(1, 0) },
        { new Vector2(1, 2), new Vector2(0, -2) },
        { new Vector2(2, 0), new Vector2(0, 2) },
        { new Vector2(2, 1), new Vector2(-1, 0) },
        { new Vector2(2, 2), new Vector2(0, -2) },
        { new Vector2(3, 0), new Vector2(-1, -1) },
        { new Vector2(3, 1), new Vector2(0, -1) },
        { new Vector2(3, 2), new Vector2(-1, -1) },
    };

    // Returns true if the inputted note and bomb coordinates cause a reset potentially
    private readonly Dictionary<int, Func<Vector2, int, int, Parity, bool>> _bombDetectionConditions = new()
    {
        { 0, (note, x, y, parity) => ((y >= note.y && y != 0) || (y > note.y && y > 0)) && x == note.x },
        { 1, (note, x, y, parity) => ((y <= note.y && y != 2) || (y < note.y && y < 2)) && x == note.x },
        { 2, (note, x, y, parity) => (parity == Parity.Forehand && (y == note.y || y == note.y - 1) && ((note.x != 3 && x < note.x) || (note.x < 3 && x <= note.x))) ||
                                     (parity == Parity.Backhand && y == note.y && ((note.x != 0 && x < note.x) || (note.x > 0 && x <= note.x))) },
        { 3, (note, x, y, parity) => (parity == Parity.Forehand && (y == note.y || y == note.y - 1) && ((note.x != 0 && x > note.x) || (note.x > 0 && x >= note.x))) ||
                                     (parity == Parity.Backhand && y == note.y && ((note.x != 3 && x > note.x) || (note.x < 3 && x >= note.x))) },
        { 4, (note, x, y, parity) => ((y >= note.y && y != 0) || (y > note.y && y > 0)) && x == note.x && !(x == 3 && y is 1) && parity != Parity.Forehand },
        { 5, (note, x, y, parity) => ((y >= note.y && y != 0) || (y > note.y && y > 0)) && x == note.x && !(x == 0 && y is 1) && parity != Parity.Forehand },
        { 6, (note, x, y, parity) => ((y <= note.y && y != 2) || (y < note.y && y < 2)) && x == note.x && !(x == 3 && y is 1) && parity != Parity.Backhand },
        { 7, (note, x, y, parity) => ((y <= note.y && y != 2) || (y < note.y && y < 2)) && x == note.x && !(x == 0 && y is 1) && parity != Parity.Backhand },
        { 8, (note,x,y, parity) => false }
    };

    private readonly List<GridPosition> _positions;
    public float Time { get; }

    public BeatGrid(List<BombNote> bombs, float timeStamp)
    {
        _positions = new List<GridPosition>();
        Time = timeStamp;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                GridPosition newPosition = new() { X = i, Y = j, bomb = false };
                _positions.Add(newPosition);
            }
        }

        foreach (BombNote bomb in bombs)
        {
            _positions.First(x => x.X == bomb.x && x.Y == bomb.y).bomb = true;
        }
    }

    public bool BombCheckResetIndication(List<GridPosition> positionsWithBombs, Vector2 handPos, int inferredCutDir, Parity lastParity, int xPlayerOffset = 0)
    {
        //Debug.Log("Time: "+Time+"\tHand Pos:"+handPos.x+","+handPos.y+"\tInferred:"+inferredCutDir+"\tlast parity:"+lastParity);
        foreach (Vector2 bombPos in positionsWithBombs.Select(t => new Vector2(t.X, t.Y)))
        {
            // If in the center 2 grid spaces, no point trying
            if ((bombPos.x is 1 or 2) && bombPos.y is 1) return false;

            // If we already found reason to reset, no need to try again
            bool bombResetIndicated = _bombDetectionConditions[inferredCutDir](new Vector2(handPos.x, handPos.y), (int)(bombPos.x - (xPlayerOffset * 2)), (int)bombPos.y, lastParity);
            if (bombResetIndicated) return true;
        }
        return false;
    }

    // Calculate if saber movement needed
    public Vector3 SaberUpdateCalc(Vector2 handPos, int inferredCutDir, Parity lastParity, int xPlayerOffset = 0)
    {
        // Check if given hand position and inferred cut direction this is any reset indication
        List<GridPosition> positionsWithBombs = _positions.FindAll(x => x.bomb);
        bool resetIndication =
            BombCheckResetIndication(positionsWithBombs, handPos, inferredCutDir, lastParity, xPlayerOffset);

        // If there is an inferred reset, we will pretend to reset the player
        bool parityFlip = false;
        Vector2 awayFromBombVector = new(0, 0);
        if (resetIndication)
        {
            awayFromBombVector = _positionToAvoidanceVector[new Vector2(handPos.x, handPos.y)];
            parityFlip = true;
        }

        handPos.x = Math.Clamp(handPos.x + awayFromBombVector.x, 0, 3);
        handPos.y = Math.Clamp(handPos.y + awayFromBombVector.y, 0, 2);

        return (parityFlip) ? new Vector3(handPos.x, handPos.y, 1) : new Vector3(handPos.x, handPos.y, 0);
    }
}

public class GridPosition
{
    public bool bomb;
    public int X, Y;
}