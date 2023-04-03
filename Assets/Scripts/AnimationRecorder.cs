using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Microsoft.MixedReality.Toolkit.Input;
using System.Runtime.InteropServices;
using System;
using UnityEngine.Windows.WebCam;
using System.Linq;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
#endif

public class AnimationRecorder : MonoBehaviour
{
    public GameObject logger;
    private DebugOutput dbg;

    private PointCloudCollection current_animation;

    private PhotoCapture photoCaptureObject = null;
    private Texture2D targetTexture = null;
    private CameraParameters cameraParameters;
    private bool cameraReady = false;

    private bool recording;
#if ENABLE_WINMD_SUPPORT
        HL2ResearchMode researchMode;
        Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
#endif

    // Start is called before the first frame update
    void Start()
    {
        if(dbg == null)
        {
            dbg = logger.GetComponent<DebugOutput>();
        }

#if ENABLE_WINMD_SUPPORT
        IntPtr WorldOriginPtr = UnityEngine.XR.WindowsMR.WindowsMREnvironment.OriginSpatialCoordinateSystem;
        unityWorldOrigin = Marshal.GetObjectForIUnknown(WorldOriginPtr) as Windows.Perception.Spatial.SpatialCoordinateSystem;
#endif
        // Set up everything for capturing images, giving color to point cloud
        // See: https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Windows.WebCam.PhotoCapture.html

        current_animation = new PointCloudCollection(); 

        recording = false;

        InitResearchMode();

        // Camera setup
        try
        {
            PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
        } catch (Exception e)
        {
            Debug.Log("Caught exception: " + e.ToString());
        }


    }

    private void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        dbg.Log("Starting to initialize camera");

        this.photoCaptureObject = captureObject;
        Resolution res = PhotoCapture.SupportedResolutions.OrderByDescending(resolution => resolution.width * resolution.height).First();
        CameraParameters cameraParams = new CameraParameters(WebCamMode.PhotoMode);
        cameraParameters.hologramOpacity = 0f;
        cameraParameters.cameraResolutionWidth = res.width;
        cameraParameters.cameraResolutionHeight = res.height;
        cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;
        this.targetTexture = new Texture2D(res.width, res.height);

        photoCaptureObject.StartPhotoModeAsync(cameraParameters, OnPhotoModeStarted);
        dbg.Log("Camera initialization successfull");
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult res)
    {
        this.cameraReady = res.success;
    }

    private void TakePhoto()
    {
        if (!cameraReady)
        {
            dbg.Log("Could not take photo becuase camera is not ready!");
            return;
        }

        photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
    }

    private void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult res, PhotoCaptureFrame captureFrame)
    {
        // Copy the raw image data into our target texture
        captureFrame.UploadImageDataToTexture(targetTexture);

        // Create a gameobject that we can apply our texture to
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Renderer quadRenderer = quad.GetComponent<Renderer>() as Renderer;
        quadRenderer.material = new Material(Shader.Find("Unlit/Texture"));

        quad.transform.parent = this.transform;
        quad.transform.localPosition = new Vector3(0.0f, 0.0f, 3.0f);

        quadRenderer.material.SetTexture("_MainTex", targetTexture);
        dbg.Log("Captured image shown");
    }

    void LateUpdate()
    {
        if (!recording) 
            return;

#if ENABLE_WINMD_SUPPORT
        try
        {

            if (researchMode == null)
            {
                dbg.Log("Research mode is null");
            }

            if (!researchMode.PointCloudUpdated()) {
                return;
            }

            float[] points = researchMode.GetPointCloudBuffer();
            if (points == null)
            {
                dbg.Log("points is null");
            }

            dbg.Log(string.Format("{0} points in PointCloudBuffer", points.Length));
            PointCloud pointCloud = new PointCloud(points, .5);
            if (pointCloud == null)
            {
                dbg.Log("pointCloud is null");
            }
            pointCloud.RandomDownSample(2000);
            if (current_animation == null)
            {
                dbg.Log("current_animation is null");
            }
            current_animation.AddPointCloud(pointCloud);
        } catch(Exception e)
        {
            dbg.Log(e.ToString());
        }
#endif

        recording = false;
        photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
    }

    public void ToggleRecording()
    {
        dbg.Log("Toggled recording");
        recording = !recording;
        // Write captured point cloud animation to disk
        if (!recording)
        {
            try
            {
                dbg.Log("Started export");
                current_animation.ExportToPLY(DateTime.Now.ToString("dd-MM-yyyyTHH_mm"));
                dbg.Log("Finished export");
                // Allocate new PC collection for next animation, freeing memory
                // for animation that was just written
                current_animation = new PointCloudCollection();
            }
            catch (Exception e)
            {
                dbg.Log(e.ToString());
            }
        }
    }
    private void InitResearchMode()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode = new HL2ResearchMode();

        researchMode.InitializeDepthSensor();
        researchMode.InitializeSpatialCamerasFront();
        researchMode.SetReferenceCoordinateSystem(unityWorldOrigin);
        researchMode.SetPointCloudDepthOffset(0);

        researchMode.StartDepthSensorLoop(true);
        researchMode.StartSpatialCamerasFrontLoop();
#endif
    }
    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        // Shutdown our photo capture resource
        dbg.Log("Shutting down camera");
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }
}

