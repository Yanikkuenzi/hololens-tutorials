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

    public bool repeat = true;
    public bool playing = false;

    public GameObject pointCloudRendererGo;
    private PointCloudRenderer pointCloudRenderer;

    // Start is called before the first frame update
    void Start()
    {
        if (pointCloudRenderer == null)
        {
            pointCloudRenderer = pointCloudRendererGo.GetComponent<PointCloudRenderer>();
            pointCloudRendererGo.SetActive(playing);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!playing) return;
        if (coordinates == null) LoadAnimation("Assets/PointClouds/CoffeBox");

        pointCloudRenderer.Render(coordinates[current_idx], colors[current_idx++]);

    }

    private void LoadAnimation(string directory)
    {
        string[] filenames = Directory.GetFiles(directory, "*.ply");
        int n = filenames.Length;

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

    }

    public void TogglePointCloud()
    {
        playing = !playing;
    }
}
