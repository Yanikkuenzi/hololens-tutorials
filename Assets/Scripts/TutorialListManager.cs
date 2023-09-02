using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif

public class TutorialListManager : MonoBehaviour
{
    public GameObject logger;
    private DebugOutput dbg;
    public GameObject panel;
    public TextMeshPro description;
    public TextMeshPro title;
    public TextMeshPro counter;
    public PointCloudAnimation animationRenderer;

    string[] directories = null;
    
    private int idx;
    private string baseDir;
    private string objectName;
    private Matrix4x4 objectPose;

    TutorialListManager() 
    { 
#if WINDOWS_UWP
        StorageFolder o3d = KnownFolders.Objects3D;
        baseDir = o3d.Path + "\\";
#else
        baseDir = "Assets/Resources/PointClouds/";
#endif
    }

    // Start is called before the first frame update
    void Start()
    {
        if (dbg == null)
        {
            dbg = logger.GetComponent<DebugOutput>();
        }
        dbg.Log("Starting tutoriallistmanager");
        panel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void UpdateInfo()
    {
        dbg.Log("Called UpdateInfo");
        if (directories == null)
        {
            dbg.Log("No object to load tutorials from");
            return;
        }
        try
        {
            counter.text = $"{idx + 1} / {directories.Length}";
            title.text = ReadTitle();
            description.text = ReadDescription();
        }
        catch (Exception e)
        {
            dbg.Log(e.Message);
        }
    }

    private string ReadDescription()
    {
        try
        {
            if (directories.Length == 0) return "";
            string path = $"{baseDir}{objectName}\\{directories[idx]}\\tutorial_info.txt";
            return string.Join("\n", System.IO.File.ReadLines(path).Skip(1));
        }
        catch(System.Exception e)
        {
            dbg.Log(e.Message);
            return e.Message;
        }
    }

    private string ReadTitle()
    {
        try
        {
            if (directories.Length == 0)
                return "Object does not have tutorials associated with it";
            string path = $"{baseDir}{objectName}\\{directories[idx]}\\tutorial_info.txt";
            dbg.Log($"Attempting to read title from path {path}");
            return System.IO.File.ReadLines(path).First().Trim();
        }
        catch(System.Exception e)
        {
            dbg.Log(e.Message);
            return e.Message;
        }
    }

    public void Show(string name)
    {
        objectName = name;
        idx = 0;
        panel.SetActive(true);
        string dir = baseDir + name;
        dbg.Log($"Called show, searching {dir} for tutorials");
        try
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            directories = Directory.GetDirectories(dir);
            dbg.Log("Available tutorial directories:\n");
            for (int i = 0; i < directories.Length; ++i)
            {
                directories[i] = directories[i]
                                .Substring(directories[i].LastIndexOf("\\"));
                dbg.Log($"{directories[i]}\n");
            }
        }
        catch (Exception e)
        {
            dbg.Log($"Error: {e.Message}!\n");
        }
        UpdateInfo();
    }
    public void Hide()
    {
        panel.SetActive(false);
    }

    public void Previous()
    {
        if (idx > 0) --idx;
        UpdateInfo();
    }

    public void Next()
    {
        if (idx < directories.Length - 1) ++idx;
        UpdateInfo();
    }

    public void SetObjectPose(Matrix4x4 pose)
    {
        this.objectPose = pose;
    }

    public void TogglePlaying()
    {
        dbg.Log("Starting playback");
        animationRenderer.TogglePointCloud($"{objectName}\\{directories[idx]}", objectPose);
    }

}
