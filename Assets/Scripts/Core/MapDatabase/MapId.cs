using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapId
{
    public MapId()
    {
        _id = System.Guid.NewGuid();
    }

    public static MapId InvalidMapId;

    public override bool Equals(object obj)
    {
        MapId other = (MapId)obj;
        if (other != null)
        {
            return _id == other._id;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }

    private System.Guid _id;

}
