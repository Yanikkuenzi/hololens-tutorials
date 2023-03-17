using UnityEngine;

/// <summary>
/// Handles functionalities provided by a scene manager panel
/// </summary>
public class SceneManagerPanel : MonoBehaviour
{
    [SerializeField]
    private GameObject panel;

    // Todo: remove
    public GameObject logger;
    public DebugOutput dbg;

    private void Start()
    {
        panel.SetActive(false);
        if(dbg == null)
        {
            dbg = logger.GetComponent<DebugOutput>();
        }
    }

    /// <summary>
    /// Toggles a manager panel to control specific aspects of the scene
    /// </summary>
    public void ToggleSceneManager()
    {
        panel.SetActive(!panel.activeSelf);
        dbg.Log("Toggled scene manager");
    }
}
