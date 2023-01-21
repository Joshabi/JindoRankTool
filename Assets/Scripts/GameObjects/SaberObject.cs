using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaberObject : MonoBehaviour
{
    [SerializeField] private MeshRenderer _saberMesh;
    [SerializeField] private TrailRenderer _saberTrail;


    public void SetColour(Color c)
    {
        _saberMesh.material.color = c;
        _saberTrail.material.color = c;
    }

}
