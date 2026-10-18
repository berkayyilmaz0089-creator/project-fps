using Godot;
using System.Collections.Generic;

public partial class SceneTransitionVolume : Area3D
{
    public List<Node3D> GetTransferables()
    {
        var list = new List<Node3D>();
        foreach (Node3D body in GetOverlappingBodies())
        {
            list.Add(body);
        }
        return list;
    }
}