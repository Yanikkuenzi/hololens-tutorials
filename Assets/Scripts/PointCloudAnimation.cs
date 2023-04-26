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
    private int nFramesPerCloud = 60;
    private int nFramesShown = 0;

    public bool repeat = false;
    public bool playing = false;

    // TODO: remove
    public double distance = .5;

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
            clouds.AddPointCloud(new PointCloud("Assets/Resources/PointClouds/26-04-2023T05_30/000025.ply"));
            clouds.AddPointCloud(new PointCloud("Assets/Resources/PointClouds/26-04-2023T05_30/000025.ply"));

            clouds.Get(0).LeftHandPosition = new Vector3(-0.1f, -0.1f, 0.1f);
            clouds.Get(0).RightHandPosition = new Vector3(0.1f, -0.1f, 0.2f);
            clouds.Get(0).Segment(distance);
            clouds.Get(0).Points.Add(new Vector3(-0.1f, -0.1f, 0.1f));
            clouds.Get(0).Points.Add(new Vector3(0.1f, -0.1f, 0.2f));
            clouds.Get(0).Colors.Add(Color.yellow);
            clouds.Get(0).Colors.Add(Color.yellow);
        }

        // Don't update anything
        if (nFramesShown != nFramesPerCloud)
        {
            nFramesShown++;
            return;
        }

        // Reset counter
        nFramesShown = 0;

        // Render point cloud
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
