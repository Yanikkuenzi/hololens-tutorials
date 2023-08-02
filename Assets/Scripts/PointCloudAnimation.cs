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


    public void TogglePointCloud()
    {
        if (clouds == null)
        {
            clouds = new PointCloudCollection();
            clouds.LoadFromPLY("TestCloud");
            //Matrix4x4 K = new Matrix4x4();
            //K[0, 0] = 687.6602f;
            //K[1, 1] = 688.903f;
            //K[0, 2] = 442.8347f;
            //K[1, 2] = 238.9398f;
            //K[2, 2] = 1f;

            //Debug.Log(K);
            //Texture2D tex = new Texture2D(1,1);
            //byte[] data = File.ReadAllBytes("Assets/Resources/image_1.jpg");
            //tex.LoadImage(data);
            ////RenderTexture(tex);
            //clouds.GetLast().cameraMatrix = K;
            //clouds.GetLast().ColorFromImage(tex);
            //clouds.GetLast().Colors[12] = Color.red;
            //clouds.GetLast().Colors[100] = Color.green;
            //clouds.GetLast().Colors[1000] = Color.blue;
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
