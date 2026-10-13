using Godot.Collections;

public interface ISaveable
{
    string GetSaveId();
    Dictionary CaptureState();
    void RestoreState(Dictionary state);
}