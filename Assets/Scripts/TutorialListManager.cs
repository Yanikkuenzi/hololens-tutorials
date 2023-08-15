using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using TMPro;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Input;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif

public class TutorialListManager : MonoBehaviour
{
    public GameObject panel;
    public TextMeshPro description;
    public TextMeshPro title;
    public TextMeshPro counter;
    public PointCloudAnimation animationRenderer;

    string[] directories = null;
    
    private int idx;
    private string baseDir;
    private string objectName;
    // Start is called before the first frame update
    void Start()
    {
    #if WINDOWS_UWP
            StorageFolder o3d = KnownFolders.Objects3D;
            baseDir = o3d.Path + "/";
    #else
            // TODO: remove
            baseDir = "Assets/Resources/PointClouds/"  ;
#endif
        //panel.SetActive(false);

        // Hide hand mesh that is shown for some reason
        MixedRealityInputSystemProfile inputSystemProfile = Microsoft.MixedReality.Toolkit.CoreServices.InputSystem?.InputSystemProfile;
        if (inputSystemProfile == null)
        {
            return;
        }

        MixedRealityHandTrackingProfile handTrackingProfile = inputSystemProfile.HandTrackingProfile;
        if (handTrackingProfile != null)
        {
            handTrackingProfile.EnableHandMeshVisualization = false;
        }
        title.text = "Disabled hand mesh visualization";



    }
    // Update is called once per frame
    void Update()
    {
        // Don't do anything if the panel is not visible
        if (!panel.activeSelf)
            return;

        //TODO: remove
        if (directories == null) return;

        counter.text = $"{idx + 1} / {directories.Length}";
        title.text = ReadTitle();
        description.text = ReadDescription();
    }

    private string ReadDescription()
    {
        try
        {
            string path = $"{baseDir}{objectName}/{directories[idx]}/tutorial_info.txt";
            return string.Join("\n", System.IO.File.ReadLines(path).Skip(1));
        }
        catch(System.Exception e)
        {
            Debug.Log(e.Message);
            return e.Message;
        }
    }

    private string ReadTitle()
    {
        try
        {
            string path = $"{baseDir}{objectName}/{directories[idx]}/tutorial_info.txt";
            return System.IO.File.ReadLines(path).First().Trim();
        }
        catch(System.Exception e)
        {
            Debug.Log(e.Message);
            return e.Message;
        }
    }

    public void Show(string name)
    {
        objectName = name;
        idx = 0;
        panel.SetActive(true);
        // Todo: change
        title.text = name;

        string dir = baseDir + name;
        Debug.Log(dir);
        directories = Directory.GetDirectories(dir);
        for (int i = 0; i < directories.Length; ++i)
        {
            directories[i] = directories[i]
                            .Substring(directories[i].LastIndexOf("\\"));
            Debug.Log(directories[i]);
        }
    }
    public void Hide()
    {
        panel.SetActive(false);
    }

    public void Previous()
    {
        if (idx > 0) --idx;
    }

    public void Next()
    {
        if (idx < directories.Length - 1) ++idx;
    }

    // TODO: implement
    public void TogglePlaying()
    {
        //animationRenderer.TogglePointCloud("");
    }

}
