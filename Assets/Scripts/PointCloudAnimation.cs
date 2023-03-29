using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tutorials;
using Tutorials.ResearchMode;
using Microsoft.MixedReality.Toolkit.Diagnostics;
#if WINDOWS_UWP
using Windows.Storage;
using System.Threading.Tasks;
#endif

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
            Debug.Log(string.Format("dbg in PCA: {0}", dbg));
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!playing) return;
        if (coordinates == null)
        {
            //LoadAnimation("CoffeBox_downsampled");
            LoadAnimation("29-03-2023T08_51");
            // TODO: move this to export
            FilterPoints(.5);
        }

        pointCloudRenderer.Render(coordinates[current_idx], colors[current_idx]);
        // Increment frame and wrap around if end is reached
        current_idx = (current_idx + 1) % coordinates.Length;
        // If last frame was played and repeat is not set, set playing to false
        playing = current_idx > 0 || repeat;

    }

    private void LoadAnimation(string directory)
    {
        string[] filenames = null;
        try
        {
#if WINDOWS_UWP
            StorageFolder o3d = KnownFolders.Objects3D;
            string dir = o3d.Path + "/" + directory;
            dbg.Log("Looking for ply files in: " + dir);
            filenames = Directory.GetFiles(dir, "*.ply");

#else
            filenames = Directory.GetFiles("Assets/Resources/PointClouds/" + directory, "*.ply");
#endif
        }
        catch (Exception e)
        {
            dbg.Log(e.ToString());
            playing = false;
            return;
        }

        //int n = Math.Min(filenames.Length, 50);
        int n = filenames.Length;
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

    public void FilterPoints(double dist)
    {
        // Square distance to avoid having to take square roots in loop for norm
        dist *= dist;
        for (int i = 0; i < coordinates.Length; ++i) 
        {
            Vector3[] pointCloud = coordinates[i];
            for (int j = 0; j < pointCloud.Length; ++j) 
            {
                if (pointCloud[j].sqrMagnitude > dist) 
                {
                    //pointCloud[j] = Vector3.zero;
                    colors[i][j] = Color.red;
                }
                else
                {
                    colors[i][j] = Color.green;
                }
            }
        }
    }
}
