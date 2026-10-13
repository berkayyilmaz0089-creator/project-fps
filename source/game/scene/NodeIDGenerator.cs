using Godot;

public partial class NodeIDGenerator : Node
{
    public string GetSaveID(Node targetNode)
    {
        if (targetNode == null)
        {
            return string.Empty;
        }

        if (targetNode is ISaveable saveable)
        {
            string customId = saveable.GetSaveId();
            if (!string.IsNullOrEmpty(customId))
            {
                return customId;
            }
        }

        Node pathOwner = targetNode.GetTree()?.CurrentScene;
        if (pathOwner != null && targetNode != pathOwner)
        {
            return pathOwner.GetPathTo(targetNode).ToString();
        }

        return targetNode.GetPath().ToString();
    }
}