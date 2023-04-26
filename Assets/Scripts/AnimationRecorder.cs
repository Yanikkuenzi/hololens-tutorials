using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.WebCam;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
using Windows.Storage;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;
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

    private bool recording = false;
#if ENABLE_WINMD_SUPPORT
        HL2ResearchMode researchMode;
        Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
#endif

    // Start is called before the first frame update
    void Start()
    {
        if (dbg == null)
        {
            dbg = logger.GetComponent<DebugOutput>();
        }
        dbg.Log("Starting AnimationRecorder");

#if ENABLE_WINMD_SUPPORT
        IntPtr WorldOriginPtr = UnityEngine.XR.WindowsMR.WindowsMREnvironment.OriginSpatialCoordinateSystem;
        unityWorldOrigin = Marshal.GetObjectForIUnknown(WorldOriginPtr) as Windows.Perception.Spatial.SpatialCoordinateSystem;
#endif
        // Set up everything for capturing images, giving color to point cloud
        // See: https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Windows.WebCam.PhotoCapture.html

        current_animation = new PointCloudCollection();

        recording = false;

        try
        {
            InitResearchMode();
            dbg.Log("Trying to create PhotoCapture object");
            //PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
            dbg.Log("Camera setup and research mode initialization successful");
        }
        catch (Exception e)
        {
            dbg.Log("Caught exception: " + e.ToString());
        }

    }

    private void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        if (captureObject == null)
        {
            dbg.Log("Failed to create CaptureObject!");
            return;
        }
        dbg.Log("Initializing camera");

        try
        {
            this.photoCaptureObject = captureObject;
            Resolution res = PhotoCapture.SupportedResolutions.OrderByDescending(resolution => resolution.width * resolution.height).First();
            CameraParameters cameraParams = new CameraParameters(WebCamMode.PhotoMode);
            cameraParameters.hologramOpacity = 0f;
            cameraParameters.cameraResolutionWidth = res.width;
            cameraParameters.cameraResolutionHeight = res.height;
            cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;
            this.targetTexture = new Texture2D(res.width, res.height);

            photoCaptureObject.StartPhotoModeAsync(cameraParameters, OnPhotoModeStarted);
            dbg.Log("Initialization done");
        }
        catch (Exception e)
        {
            dbg.Log("Failed to initialize camera: " + e.ToString());
        }
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult res)
    {
        this.cameraReady = res.success;
        dbg.Log(cameraReady ? "Initialization successful" : "Initialization failed");
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

        //// Create a gameobject that we can apply our texture to
        //GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        //Renderer quadRenderer = quad.GetComponent<Renderer>() as Renderer;
        //quadRenderer.material = new Material(Shader.Find("Unlit/Texture"));

        //quad.transform.parent = this.transform;
        //quad.transform.localPosition = new Vector3(0.0f, 0.0f, 3.0f);

        //quadRenderer.material.SetTexture("_MainTex", targetTexture);
        //dbg.Log("Captured image shown");

        // Write to PNG
        try
        {
            dbg.Log("Trying to write PNG to file");
            byte[] bytes = targetTexture.EncodeToPNG();
            string directory = "Assets/Resources/";
#if ENABLE_WINMD_SUPPORT
            StorageFolder objects_3d = KnownFolders.Objects3D;
            directory = objects_3d.Path + "/";
#endif

            File.WriteAllBytes(directory + string.Format("{}.png", current_animation.Count), bytes);
        }
        catch (Exception e)
        {
            dbg.Log(e.ToString());
        }
        dbg.Log("Write successfull");

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
                try 
                {
                    InitResearchMode();
                } 
                catch (Exception e)
                {
                    dbg.Log("Cannot init researchmode! -- " + e.ToString());
                }
            }

            if (!researchMode.PointCloudUpdated()) {
                //dbg.Log("point cloud not updated, returning now");
                return;
            }

            float[] points = researchMode.GetPointCloudBuffer();
            if (points == null)
            {
                dbg.Log("points is null");
                return;
            }


            dbg.Log(string.Format("{0} points in PointCloudBuffer", points.Length));

            // Create point cloud and add to animation
            PointCloud pointCloud = new PointCloud(points, .5);
            // Get hand positions for segmentation
            MixedRealityPose pose;
            if (HandJointUtils.TryGetJointPose(TrackedHandJoint.Wrist, Handedness.Left, out pose))
            {
                pointCloud.LeftHandPosition = pose.Position;
            }
            if (HandJointUtils.TryGetJointPose(TrackedHandJoint.Wrist, Handedness.Right, out pose))
            {
                pointCloud.RightHandPosition  = pose.Position;
            }
            pointCloud.RandomDownSample(2000);
            if (current_animation == null)
            {
                dbg.Log("current_animation is null");
            }
            current_animation.AddPointCloud(pointCloud);
            dbg.Log("Added pointcloud to animation");

        } catch(Exception e)
        {
            dbg.Log(e.ToString());
        }
#endif

        dbg.Log("Trying to take picture");
        try
        {
            photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
            dbg.Log("Successfully took picture");
        }
        catch (Exception e)
        {
            dbg.Log("Caught exception: " + e.ToString());
        }
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
                dbg.Log(string.Format("Started export of {0} point clouds", current_animation.Count));
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
        try {
            researchMode = new HL2ResearchMode();

            researchMode.InitializeDepthSensor();
            researchMode.InitializeSpatialCamerasFront();
            researchMode.SetReferenceCoordinateSystem(unityWorldOrigin);
            researchMode.SetPointCloudDepthOffset(0);

            researchMode.StartDepthSensorLoop(true);
            researchMode.StartSpatialCamerasFrontLoop();
            dbg.Log("Initialized research mode");
        } catch (Exception e) {
            dbg.Log(e.ToString());
        }
#endif
    }
    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        // Shutdown our photo capture resource
        dbg.Log("Shutting down camera");
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
        cameraReady = false;
    }

    private void OnApplicationQuit()
    {
        dbg.Log("Called OnApplicationQuit");
        StartCoroutine(waiter());
        // Stop the photo mode and dispose of the photo capture object
        if (photoCaptureObject != null)
        {
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
    }

    void OnDestroy()
    {
        dbg.Log("Called OnDestroy");
        StartCoroutine(waiter());

        // Stop the photo mode and dispose of the photo capture object
        if (photoCaptureObject != null)
        {
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
    }

    void OnEnable()
    {
        dbg.Log("Enabled");
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        dbg.Log("Log: " + logString);
        if (type == LogType.Error || type == LogType.Exception)
        {
            // Handle the error or exception here
            dbg.Log("Unity Error: " + logString + "\n" + stackTrace);
        }
    }

    IEnumerator waiter()
    {
        yield return new WaitForSeconds(4);
    }

}

