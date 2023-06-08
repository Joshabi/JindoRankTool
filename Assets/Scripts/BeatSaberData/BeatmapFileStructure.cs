using System.Collections;
using System.Collections.Generic;
using JoshaParity;
using UnityEngine;


public class BeatmapFileStructure
{
    public static BeatmapFileStructureV3 ConvertV2ToV3(BeatmapFileStructureV2 inV2File)
    {
        BeatmapFileStructureV3 returnV3File = new BeatmapFileStructureV3();
        returnV3File.version = inV2File._version;
        List<ColourNote> newNotes = new List<ColourNote>();
        List<BombNote> newBombs = new List<BombNote>();
        if (inV2File._notes != null)
        {
            foreach (NoteV2 note in inV2File._notes)
            {
                if (note._type == 3)
                {
                    BombNote bomb = new BombNote();
                    bomb.b = note._time;
                    bomb.x = note._lineIndex;
                    bomb.y = note._lineLayer;
                    newBombs.Add(bomb);
                }
                else if (note._type != 2)
                {
                    ColourNote newNote = new ColourNote();
                    newNote.b = note._time;
                    newNote.c = note._type;
                    newNote.x = note._lineIndex;
                    newNote.y = note._lineLayer;
                    newNote.d = note._cutDirection;
                    newNotes.Add(newNote);
                }
            }
        }
        returnV3File.bombNotes = newBombs.ToArray();
        returnV3File.colorNotes = newNotes.ToArray();

        List<Slider> newSliders = new List<Slider>();
        if (inV2File._sliders != null)
        {
            foreach (SliderV2 slider in inV2File._sliders)
            {
                Slider newSlider = new Slider();
                newSlider.b = slider._headTime;
                newSlider.x = slider._headLineIndex;
                newSlider.y = slider._headLineLayer;
                newSlider.mu = slider._headControlPointLengthMultiplier;
                newSlider.d = slider._headCutDirection;
                newSlider.tb = slider._tailTime;
                newSlider.tx = slider._tailLineIndex;
                newSlider.ty = slider._tailLineLayer;
                newSlider.tmu = slider._tailControlPointLengthMultiplier;
                newSlider.m = slider._sliderMidAnchorMode;
                newSliders.Add(newSlider);
            }
        }
        returnV3File.sliders = newSliders.ToArray();

        List<Obstacle> newObstacles = new List<Obstacle>();
        if (inV2File._obstacles != null)
        {
            foreach (ObstacleV2 obstacle in inV2File._obstacles)
            {
                Obstacle newOb = new Obstacle();
                newOb.b = obstacle._time;
                newOb.x = obstacle._lineIndex;
                newOb.y = 0;
                newOb.w = obstacle._width;
                newOb.h = (obstacle._type == 0) ? 3 : 1;
                newObstacles.Add(newOb);
            }
        }
        returnV3File.obstacles = newObstacles.ToArray();
        return returnV3File;
    }

}

[System.Serializable]
public struct BeatmapFileStructureV3
{
    public string version;
    public ColourNote[] colorNotes;
    public BombNote[] bombNotes;
    public Obstacle[] obstacles;
    public Slider[] sliders;
    public BurstSlider[] burstSliders;
}


[System.Serializable]
public struct BeatmapFileStructureV2
{
    public string _version;
    public NoteV2[] _notes;
    public SliderV2[] _sliders;
    public ObstacleV2[] _obstacles;
}

[System.Serializable]
public struct BeatmapStructure
{
    public string _difficulty;
    public BeatmapDifficultyRank _difficultyRank;
    public string _beatmapFilename;
    public float _noteJumpMovementSpeed;
    public float _noteJumpStartBeatOffset;
    public object _customData;
    public string hash;
    public string mapName;
    public string songFilename;
    public MapId id;
}

[System.Serializable]
public struct BeatmapData
{
    public BeatmapStructure Metadata;
    public BeatmapFileStructureV3 BeatData;
}

[System.Serializable]
public struct BeatmapDataV2
{
    public BeatmapStructure Metadata;
    public BeatmapFileStructureV3 BeatDataV2;
}

[System.Serializable]
public struct ColourNote
{
    public float b; // beat
    public int x; // 0-3
    public int y; // 0-2
    public int c; // 0-1
    public int d; // 0-8 direction
    public int a; // counter-clockwise angle in degrees
}

public enum BeatChirality
{
    LeftHand = 0,
    RightHand = 1,
}

public enum BeatCutDirection
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3,
    UpLeft = 4,
    UpRight = 5,
    DownLeft = 6,
    DownRight = 7,
    Any = 8
}

[System.Serializable]
public struct NoteV2
{
    public float _time; // beat (b)
    public int _lineIndex; // 0-3 (x)
    public int _lineLayer; // 0-2 (y)
    public int _type; // 0=left,1=right,2=unused,3=bomb
    public int _cutDirection; // 0-8 (d)
}

[System.Serializable]
public struct BombNote
{
    public float b; // beat
    public int x; // 0-3
    public int y; // 0-2
}

[System.Serializable]
public struct ObstacleV2
{
    public float _time; // beat (b)
    public int _lineIndex; // 0-3 (x)
    public int _type; // 0 = full height, 1 = crouch/duck wall
    public int _duration; // duration in beats (d)
    public int _width; // width (w)
}

[System.Serializable]
public struct Obstacle
{
    public float b; // beat
    public int x; // 0-3
    public int y; // 0-2
    public int d; // duration in beats
    public int w; // width
    public int h; // height
}

[System.Serializable]
public struct SliderV2
{
    public int colorType; // c
    public float _headTime; // b
    public int _headLineIndex; // x
    public int _headLineLayer; // y
    public float _headControlPointLengthMultiplier; // mu
    public int _headCutDirection; // d
    public float _tailTime; // tb
    public int _tailLineIndex; // tx
    public int _tailLineLayer; // ty
    public float _tailControlPointLengthMultiplier; // tmu
    public int _tailCutDirection; // not a thing in v3
    public int _sliderMidAnchorMode; // m
}

[System.Serializable]
public struct Slider
{
    public float b; // beat
    public int c; // 0-1
    public int x; // 0-3
    public int y; // 0-2
    public int d; // 0-8 (head direction)
    public float mu; // head multiplier (how far the arc goes from the head)
    public float tb; // tail beat
    public int tx; // 0-3
    public int ty; // 0-2
    public int tc; // 0-1
    public float tmu; // tail multiplier (how far the arc goes from the tail)
    public int m; // mid-anchor mode
}

public enum MidAnchorMode
{
    Straight = 0,
    Clockwise = 1,
    CounterClockwise = 2,
}

[System.Serializable]
public struct BurstSlider
{
    public float b; // beat
    public int x; // 0-3
    public int y; // 0-2
    public int c; // 0-1
    public int d; // head direction
    public float tb; // tail beat
    public int tx; // 0-3
    public int ty; // 0-2
    public int sc; // segment count
    public float s; // squish factor (should not be 0 or it crashes beat saber, apparently they never fixed this???)
}
