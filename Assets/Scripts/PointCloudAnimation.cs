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

    public bool repeat = true;
    private bool playing = false;
    private string currentPointCloudName = "";

    public GameObject cube_record;
    public GameObject cube_play;
    public GameObject pointCloudRendererGo;
    private PointCloudRenderer pointCloudRenderer;

    public GameObject logger;
    private DebugOutput dbg;

    // Start is called before the first frame update
    void Start()
    {
        pointCloudRenderer = pointCloudRendererGo.GetComponent<PointCloudRenderer>();
        pointCloudRendererGo.SetActive(playing);
        if (dbg == null)
        {
            dbg = logger.GetComponent<DebugOutput>();
        }
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
        dbg.Log($"Current cloud {current_idx} contains {current.Count} points");
        pointCloudRenderer.Render(current.Points, current.Colors);

        // Increment frame and wrap around if end is reached
        current_idx = (current_idx + 1) % clouds.Count;
        // If last frame was played and repeat is not set, set playing to false
        playing = (current_idx > 0) || repeat;
    }


    public void TogglePointCloud()
    {
        if (clouds == null || !name.Equals(currentPointCloudName))
        {
            clouds = new PointCloudCollection();
            Matrix4x4 teaching_pose_inv = new Matrix4x4(
                new Vector4(-0.2733338f, 0.9619197f, 0, 0.8491958f),
                new Vector4(-0.01537001f, -0.004351974f, 0.9998727f, -0.3713996f),
                new Vector4(0.9617969f, 0.2732987f, 0.01597387f, 0.3999121f),
                new Vector4(0f, 0f, 0f, 1)).transpose.inverse;

            Matrix4x4 pose = Matrix4x4.TRS(cube_play.transform.position,
                                            cube_play.transform.rotation,
                                            Vector3.one);

            //cube_record.transform.position = teaching_pose_inv.MultiplyPoint(cube_record.transform.position);

            //for(int i = 0; i < 4; ++i)
            //{
            //    Debug.Log($"{teaching_pose_inv[i, 0]}\t{teaching_pose_inv[i, 1]}\t{teaching_pose_inv[i, 2]}\t{teaching_pose_inv[i, 3]}");
            //}
            try
            {
                //Matrix4x4 axis_transform = Matrix4x4.identity;
                //axis_transform[0, 0] = axis_transform[1, 1] = 0;
                //axis_transform[0, 1] = axis_transform[1, 0] = 1;
                //clouds.LoadFromPLY("TestCloud", pose);
                clouds.LoadFromPLY("TestCloud", pose);
                //clouds.LoadFromPLY("TestCloud", Matrix4x4.identity);
                //clouds.LoadFromPLY("TestCloud", axis_transform);
            } catch (Exception ex)
            {
                dbg.Log($"Error while loading the point clouds: {ex.Message}");
                return;
            }
            dbg.Log($"Successfully loaded {clouds.Count} point clouds from 3d\\{name}*.ply");
        }
        playing = !playing;
        pointCloudRendererGo.SetActive(playing);
    }


    public void TogglePointCloud(string name, Matrix4x4 objectPose)
    {
        // Load point cloud from memory if none is currently loaded
        // or we want to display another one
        if (clouds == null || !name.Equals(currentPointCloudName))
        {
            clouds = new PointCloudCollection();
            try
            {
                clouds.LoadFromPLY(name, objectPose);
            } catch (Exception ex)
            {
                dbg.Log($"Error while loading the point clouds: {ex.Message}");
                return;
            }
            dbg.Log($"Successfully loaded {clouds.Count} point clouds from 3d\\{name}*.ply");
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
