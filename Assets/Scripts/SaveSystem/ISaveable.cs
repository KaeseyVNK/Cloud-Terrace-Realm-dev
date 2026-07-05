namespace CloudTerraceRealm.SaveSystem
{
    public interface ISaveable
    {
        string CaptureState();
        void RestoreState(string stateJson);
    }
}
