using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterfaceWindows;


public class RegisterConvenientQuestLogWindow : MonoBehaviour
{
    public static Mod mod;
    public static RegisterConvenientQuestLogWindow instance;

    public static RegisterConvenientQuestLogWindow Instance
    {
        get { return instance != null ? instance : (instance = FindObjectOfType<RegisterConvenientQuestLogWindow>()); }
    }

    // Use this for initialization
    [Invoke(StateManager.StateTypes.Start, 0)]
    public static void Init(InitParams initParams)
    {
        // Get mod
        RegisterConvenientQuestLogWindow.mod = initParams.Mod;


        // Register Custom UI Window
        UIWindowFactory.RegisterCustomUIWindow(UIWindowType.QuestJournal, typeof(ConvenientQuestLogWindow));

        // Add script to the scene.
        instance = new GameObject("ConvenientQuestLog").AddComponent<RegisterConvenientQuestLogWindow>();
    }
}
