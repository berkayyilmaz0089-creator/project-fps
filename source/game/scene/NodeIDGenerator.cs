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

        Node currentScene = targetNode.GetTree()?.CurrentScene;
        if (currentScene != null && targetNode.IsInsideTree() && currentScene.IsAncestorOf(targetNode))
        {
            return currentScene.GetPathTo(targetNode).ToString();
        }

        return targetNode.GetPath().ToString();
    }
}