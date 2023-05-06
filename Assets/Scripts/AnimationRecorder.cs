using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Windows.WebCam;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Graphics.Imaging;
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
    //private VideoCapture videoCaptureObject = null;
    // TODO: remove
    int frames;
    int picture = 0;
    private Texture2D targetTexture = null;
    private CameraParameters cameraParameters;
    private DateTime recordingStart;

    private bool recording = false;
#if ENABLE_WINMD_SUPPORT
        private MediaCapture mediaCapture;
        private LowLagPhotoCapture photoCapture;
        HL2ResearchMode researchMode;
        bool isCapturing;
        Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
#endif

    // Start is called before the first frame update
    void Start()
    {
#if ENABLE_WINMD_SUPPORT
    Debug.Log("WINMD SUPPORT enabled");
#else
    Debug.Log("WINMD SUPPORT NOT enabled!!!!");
#endif 

        if (dbg == null)
        {
            dbg = logger.GetComponent<DebugOutput>();
        }
        dbg.Log("Starting AnimationRecorder");

#if ENABLE_WINMD_SUPPORT
        IntPtr WorldOriginPtr = UnityEngine.XR.WindowsMR.WindowsMREnvironment.OriginSpatialCoordinateSystem;
        unityWorldOrigin = Marshal.GetObjectForIUnknown(WorldOriginPtr) as Windows.Perception.Spatial.SpatialCoordinateSystem;

        // Initialize camera
        InitCamera();
        isCapturing = false;
#endif
        // Set up everything for capturing images, giving color to point cloud
        // See: https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Windows.WebCam.PhotoCapture.html

        current_animation = new PointCloudCollection();

        recording = false;

        try
        {
            InitResearchMode();
            //PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
            //dbg.Log("Camera setup and research mode initialization successful");
        }
        catch (Exception e)
        {
            dbg.Log("Caught exception: " + e.ToString());
        }

    }

    void Update()
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

            DateTime time = DateTime.Now;
            float[] points = researchMode.GetPointCloudBuffer();
            if (points == null)
            {
                dbg.Log("points is null");
                return;
            }

            //dbg.Log(string.Format("{0} points in PointCloudBuffer", points.Length));

            // Capture RGB image
            StartCoroutine(CapturePhotoCoroutine());
            //CapturePhoto();

            ++frames;

            // TODO: uncomment
            // Create point cloud and add to animation
            //PointCloud pointCloud = new PointCloud(points, .5, time);
            //// Get hand positions for segmentation
            //MixedRealityPose pose;
            //if (HandJointUtils.TryGetJointPose(TrackedHandJoint.Wrist, Handedness.Left, out pose))
            //{
            //    pointCloud.LeftHandPosition = pose.Position;
            //}
            //if (HandJointUtils.TryGetJointPose(TrackedHandJoint.Wrist, Handedness.Right, out pose))
            //{
            //    pointCloud.RightHandPosition  = pose.Position;
            //}
            //pointCloud.RandomDownSample(2000);
            //if (current_animation == null)
            //{
            //    dbg.Log("current_animation is null");
            //}
            //current_animation.AddPointCloud(pointCloud);
            //dbg.Log("Added pointcloud to animation");

        } catch(Exception e)
        {
            dbg.Log(e.ToString());
        }
#endif
    }

#if ENABLE_WINMD_SUPPORT
    async Task InitCamera()
    {
        try
        {
            mediaCapture = new MediaCapture();
            await mediaCapture.InitializeAsync();
            mediaCapture.Failed += (MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs) =>
            {
                dbg.Log($"MediaCapture initialization failed: {errorEventArgs.Message}");
            };

            photoCapture = await mediaCapture.PrepareLowLagPhotoCaptureAsync(ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
            dbg.Log("PhotoCapture initialization done!");
        }
        catch (Exception e)
        {
            dbg.Log(e.ToString());
        }
    }

    IEnumerator CapturePhotoCoroutine()
    {
        // Wait until previous photo is taken
        if (isCapturing) yield break;
        isCapturing = true;
        var capturePhotoTask = CapturePhoto();
        while (!capturePhotoTask.IsCompleted)
        {
            yield return null;
        }
        isCapturing = false;
    }

    async Task CapturePhoto()
    {
        dbg.Log("In CapturePhoto()");
        try
        {
            //var photoCapture = await mediaCapture.PrepareLowLagPhotoCaptureAsync(ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
            if (mediaCapture == null) {
                dbg.Log("MediaCapture is null!");
                return;
            }
            if (photoCapture == null) {
                dbg.Log("PhotoCapture is null!");
                return;
            }
            var capturedPhoto = await photoCapture.CaptureAsync();
            if (capturedPhoto == null) {
                dbg.Log("capturedPhoto is null!");
                return;
            }

            // TODO: remove?
            if (capturedPhoto.Frame == null) {
                dbg.Log("Frame is null!");
                return;
            }
            //dbg.Log($"Frame has dimensions: {capturedPhoto.Frame.Width} x {capturedPhoto.Frame.Height}, can be read = {capturedPhoto.Frame.CanRead}");

            //var myPictures = await Windows.Storage.StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
            //StorageFile file = await myPictures.SaveFolder.CreateFileAsync($"photo_{picture++}.jpg", CreationCollisionOption.GenerateUniqueName);


            var softwareBitmap = capturedPhoto.Frame.SoftwareBitmap;
            if (softwareBitmap == null) {
                dbg.Log("softwareBitmap is null!");
                return;
            }

            //dbg.Log($"Frame is {softwareBitmap.PixelWidth} x {softwareBitmap.PixelHeight} pixels");
            //SaveSoftwareBitmapToFile(softwareBitmap, file);
            //dbg.Log($"Saved image {picture} to file {file.Name}, Current frame rate: {picture / (DateTime.Now - recordingStart).TotalSeconds}, StreamState = {mediaCapture.CameraStreamState}");
            dbg.Log($"Saved image {picture++} to file,  Current frame rate: {picture / (DateTime.Now - recordingStart).TotalSeconds}, StreamState = {mediaCapture.CameraStreamState}");
            //photoCapture.FinishAsync();
        }
        catch (Exception e)
        {
            dbg.Log($"Caught exception in CapturePhoto: '{e.ToString()}' with camera stream state {mediaCapture.CameraStreamState}");
        }
    }

    private async void SaveSoftwareBitmapToFile(SoftwareBitmap softwareBitmap, StorageFile outputFile)
    {
        using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            // Create an encoder with the desired format
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

            // Set the software bitmap
            encoder.SetSoftwareBitmap(softwareBitmap);

            encoder.IsThumbnailGenerated = true;

            try
            {
                await encoder.FlushAsync();
            }
            catch (Exception err)
            {
                const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
                switch (err.HResult)
                {
                    case WINCODEC_ERR_UNSUPPORTEDOPERATION: 
                        // If the encoder does not support writing a thumbnail, then try again
                        // but disable thumbnail generation.
                        encoder.IsThumbnailGenerated = false;
                        break;
                    default:
                        throw;
                }
            }

            if (encoder.IsThumbnailGenerated == false)
            {
                await encoder.FlushAsync();
            }


        }
    }
#endif

    public void ToggleRecording()
    {
        recording = !recording;
        // Write captured point cloud animation to disk
        if (!recording)
        {
            try
            {
                dbg.Log(string.Format("Started export of {0} point clouds, frame rate = {1}",
                    current_animation.Count,
                    frames / (DateTime.Now - recordingStart).TotalSeconds));
                // TODO: uncomment
                //current_animation.ExportToPLY(DateTime.Now.ToString("dd-MM-yyyyTHH_mm"));
                // Allocate new PC collection for next animation, freeing memory
                // for animation that was just written
                current_animation = new PointCloudCollection();
            }
            catch (Exception e)
            {
                dbg.Log(e.ToString());
            }
        }
        else
        {
            recordingStart = DateTime.Now;
            frames = 0;
        }
        // TODO: fix this
//        else // Start video recording
//        {
//            string filename = "recording.mp4";
//#if ENABLE_WINMD_SUPPORT
//            StorageFolder objects_3d = KnownFolders.Objects3D;
//            string filepath = objects_3d.Path + "/" + filename;
//#else
//            string filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);
//#endif
//            filepath = filepath.Replace("/", @"\");
//            dbg.Log("Storing video to " + filepath);
//            // recoring is set to true in the OnStartRecordingVideo function
//            videoCaptureObject.StartRecordingAsync(filepath, OnStartedRecordingVideo);
//            dbg.Log("Started recording at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff"));
//        }
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

    private void OnApplicationQuit()
    {
        dbg.Log("Called OnApplicationQuit");

        // Stop the photo mode and dispose of the photo capture object
        if (photoCaptureObject == null) 
        {
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
#if ENABLE_WINMD_SUPPORT
        photoCapture.FinishAsync();
#endif
        //if (videoCaptureObject != null)
        //{
        //    videoCaptureObject.StopVideoModeAsync(OnStoppedVideoCaptureMode);
        //}
    }

    void OnDestroy()
    {
        dbg.Log("Called OnDestroy");

        // Stop the photo mode and dispose of the photo capture object
        if (photoCaptureObject == null) 
        {
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
#if ENABLE_WINMD_SUPPORT
        photoCapture.FinishAsync();
#endif
        //if (videoCaptureObject != null)
        //{
        //    videoCaptureObject.StopVideoModeAsync(OnStoppedVideoCaptureMode);
        //}

    }



    // TODO: remove

    // PhotoCapture methods
    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;

        // Choose highest available resolution
        Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();

        CameraParameters c = new CameraParameters();
        c.cameraResolutionWidth = cameraResolution.width;
        c.cameraResolutionHeight = cameraResolution.height;
        c.pixelFormat = CapturePixelFormat.BGRA32;

        captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if (!result.success)
        {
            dbg.Log("PhotoMode started");
        }
        else
        {
            Debug.Log(result);
            dbg.Log("Unable to start photo mode!1!!!1!");
        }
    }

    void OnPhotoCaptured(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        if (result.success)
        {
            // Create our Texture2D for use and set the correct resolution
            //Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
            //Texture2D targetTexture = new Texture2D(cameraResolution.width, cameraResolution.height);
            // Copy the raw image data into our target texture
            //photoCaptureFrame.UploadImageDataToTexture(targetTexture);
            //photoCaptureFrame.
            ++frames;
        }
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }

    void CreateFrameObject(Texture2D frameTexture)
    {
        // Create a new Quad GameObject
        GameObject frameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);

        // Assign the frame texture to the Quad's material
        frameObject.GetComponent<Renderer>().material.mainTexture = frameTexture;

        // Set the position and scale of the Quad
        frameObject.transform.position = new Vector3(0, 0, 5);
        frameObject.transform.localScale = new Vector3(2, 2, 1);
    }

    //void InitVideoCapture()
    //{
    //    try
    //    {
    //        VideoCapture.CreateAsync(false, OnVideoCaptureCreated);
    //    }
    //    catch (Exception e)
    //    {
    //        dbg.Log(e.Message);
    //    }
    //}

    //void OnVideoCaptureCreated(VideoCapture videoCapture)
    //{
    //    Resolution cameraResolution = VideoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
    //    dbg.Log("Using resolution: " + cameraResolution);

    //    float cameraFramerate = VideoCapture.GetSupportedFrameRatesForResolution(cameraResolution).OrderByDescending((fps) => fps).First();
    //    dbg.Log("Using framerate: " + cameraFramerate);

    //    dbg.Log("OnVideoCaptureCreated");
    //    if (videoCapture != null)
    //    {
    //        videoCaptureObject = videoCapture;
    //        dbg.Log("Created VideoCapture Instance!");

    //        CameraParameters cameraParameters = new CameraParameters();
    //        cameraParameters.hologramOpacity = 0.0f;
    //        cameraParameters.frameRate = cameraFramerate;
    //        cameraParameters.cameraResolutionWidth = cameraResolution.width;
    //        cameraParameters.cameraResolutionHeight = cameraResolution.height;
    //        cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;

    //        videoCaptureObject.StartVideoModeAsync(cameraParameters,
    //            VideoCapture.AudioState.None,
    //            OnStartedVideoCaptureMode);
    //    }
    //    else
    //    {
    //        dbg.Log("ERROR: Failed to create VideoCapture Instance!");
    //    }
    //}

    //void OnStartedVideoCaptureMode(VideoCapture.VideoCaptureResult result)
    //{
    //    dbg.Log("Started Video Capture Mode!");
    //}

    //void OnStoppedVideoCaptureMode(VideoCapture.VideoCaptureResult result)
    //{
    //    dbg.Log("Stopped Video Capture Mode!");
    //}

    //void OnStartedRecordingVideo(VideoCapture.VideoCaptureResult result)
    //{
    //    videoStart = DateTime.Now;
    //    recording = true;
    //    dbg.Log("Started Recording Video at " + videoStart.ToString());
    //}

    //void OnStoppedRecordingVideo(VideoCapture.VideoCaptureResult result)
    //{
    //    dbg.Log("Stopped Recording Video!");
    //}

}

