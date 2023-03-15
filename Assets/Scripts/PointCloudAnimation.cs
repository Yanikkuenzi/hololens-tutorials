using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tutorials;
using Tutorials.ResearchMode;
using Unity.PlasticSCM.Editor.WebApi;

public class PointCloudAnimation : MonoBehaviour
{
    private Vector3[][] coordinates;
    private Color[][] colors;
    private int current_idx = 0;

    public bool repeat = false;
    public bool playing = false;

    public GameObject pointCloudRendererGo;
    private PointCloudRenderer pointCloudRenderer;

    public GameObject logger;
    private DebugOutput dbg;

    // Start is called before the first frame update
    void Start()
    {
        if (pointCloudRenderer == null)
        {
            pointCloudRenderer = pointCloudRendererGo.GetComponent<PointCloudRenderer>();
            pointCloudRendererGo.SetActive(playing);
        }

        if(dbg == null)
        {
            dbg = logger.GetComponent<DebugOutput>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!playing) return;
        if (coordinates == null) LoadAnimation("Assets/PointClouds/CoffeBox_downsampled");

        pointCloudRenderer.Render(coordinates[current_idx], colors[current_idx]);
        // Increment frame and wrap around if end is reached
        current_idx = (current_idx + 1) % coordinates.Length;
        // If last frame was played and repeat is not set, set playing to false
        playing = current_idx > 0 || repeat;

    }

    private void LoadAnimation(string directory)
    {
        string[] filenames = Directory.GetFiles(directory, "*.ply");
        int n = 10;//filenames.Length;
        dbg.Log(string.Format("Loading {0} ply files", n));

        // Initialize enough space for all the point clouds
        coordinates = new Vector3[n][];
        colors = new Color[n][];

        // Make sure we get the correct order
        Array.Sort(filenames);

        // Load all the point clouds belonging to the animation
        for (int i = 0; i < n; i++)
        {
            FileHandler.LoadPointsFromPLY(filenames[i], out coordinates[i], out colors[i]);
        }

        dbg.Log("Animation loaded");

    }

    public void TogglePointCloud()
    {
        Debug.Log("PC toggled");
        playing = !playing;
        pointCloudRendererGo.SetActive(playing);
    }
}
