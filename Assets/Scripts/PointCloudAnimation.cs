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
    private int nFramesPerCloud = 30;
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
        pointCloudRenderer = pointCloudRendererGo.GetComponent<PointCloudRenderer>();
        pointCloudRendererGo.SetActive(playing);
        dbg = logger.GetComponent<DebugOutput>();

    }

    // Update is called once per frame
    void Update()
    {
        if (!playing) return;

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
        if (clouds == null)
        {
            clouds = new PointCloudCollection();
            clouds.LoadFromPLY("01-05-2023T09_21");
            clouds.ColorFromVideo("Assets/Resources/recording_2.mp4");
            Debug.Log("Loaded color from video");
        }
        playing = !playing;
        pointCloudRendererGo.SetActive(playing);
    }

}
