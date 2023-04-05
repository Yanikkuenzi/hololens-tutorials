using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tutorials;
using Tutorials.ResearchMode;
using Microsoft.MixedReality.Toolkit.Diagnostics;
using UnityEngine.XR.WSA.Input;
using System.Diagnostics.PerformanceData;
#if WINDOWS_UWP
using Windows.Storage;
using System.Threading.Tasks;
#endif

public class PointCloudAnimation : MonoBehaviour
{
    private PointCloudCollection clouds;

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
        if (clouds == null)
        {
            clouds = new PointCloudCollection();
            //clouds.LoadFromPLY("30-03-2023T19_00");
            clouds.AddPointCloud(new PointCloud("Assets/Resources/PointClouds/CoffeBox_downsampled/000000.ply"));
            clouds.AddPointCloud(new PointCloud("Assets/Resources/PointClouds/CoffeBox_downsampled/000000.ply"));
            Debug.Log("Got " + clouds.Count + " clouds");

            Matrix4x4 M = new Matrix4x4(
                    new Vector4(636.65930176f, 0, 0, 0),
                    new Vector4(0, 636.25195312f, 0, 0),
                    new Vector4(635.28388188f, 366.87403535f, 1, 0),
                    new Vector4(0, 0, 0, 0));
            
            Texture2D tex = new Texture2D(1, 1);
            tex.LoadImage(File.ReadAllBytes("Assets/Resources/000000.png"));
            clouds.Get(0).ColorFromImage(tex, M);
        }
        //Debug.Log("Got " + clouds.Count + " clouds");

        PointCloud current = clouds.Get(current_idx);
        pointCloudRenderer.Render(current.Points, current.Colors);

        // Increment frame and wrap around if end is reached
        current_idx = (current_idx + 1) % clouds.Count;
        // If last frame was played and repeat is not set, set playing to false
        playing = current_idx > 0 || repeat;
    }


    public void TogglePointCloud()
    {
        playing = !playing;
        pointCloudRendererGo.SetActive(playing);
    }

}
