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
    private int nFramesPerCloud = 10;
    private int nFramesShown = 0;

    public bool repeat = false;
    public bool playing = false;
    private string currentPointCloudName = "";

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
        Debug.Log($"Current cloud {current_idx} contains {current.Count} points");
        pointCloudRenderer.Render(current.Points, current.Colors);

        // Increment frame and wrap around if end is reached
        current_idx = (current_idx + 1) % clouds.Count;
        // If last frame was played and repeat is not set, set playing to false
        playing = current_idx > 0 || repeat;
    }


    public void TogglePointCloud(string name)
    {
        // Load point cloud from memory if none is currently loaded
        // or we want to display another one
        if (clouds == null || !name.Equals(currentPointCloudName))
        {
            clouds = new PointCloudCollection();
            clouds.LoadFromPLY(name);
        }
        playing = !playing;
        pointCloudRendererGo.SetActive(playing);
    }

    public void RenderTexture(Texture2D texture)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // Set the position of the quad
        quad.transform.position = new Vector3(0, 0, 0);

        // Get the Renderer component from the quad
        Renderer quadRenderer = quad.GetComponent<Renderer>();

        // Create a new Material using the Standard shader
        Material material = new Material(Shader.Find("Standard"));

        // Assign the texture to the material's main texture
        material.mainTexture = texture;

        // Assign the new material to the quad's renderer
        quadRenderer.material = material;
    }

}
