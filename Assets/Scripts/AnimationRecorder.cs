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
using Windows.Media.Capture.Frames;
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
        private MediaFrameReader videoFrameReader;
        HL2ResearchMode researchMode;
        bool isCapturing;
        Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
        ArrayList bitmaps;
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
        bitmaps = new ArrayList();
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
            //StartCoroutine(CapturePhotoCoroutine());
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
            current_animation.AddPointCloud(pointCloud);
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
            var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
            var selectedGroupObjects = frameSourceGroups.Select(group =>
               new
               {
                   sourceGroup = group,
                   colorSourceInfo = group.SourceInfos.FirstOrDefault((sourceInfo) =>
                   {
                       return sourceInfo.MediaStreamType == MediaStreamType.VideoPreview
                       && sourceInfo.SourceKind == MediaFrameSourceKind.Color;
                   })

               }).Where(t => t.colorSourceInfo != null)
               .FirstOrDefault();

            MediaFrameSourceGroup selectedGroup = selectedGroupObjects?.sourceGroup;
            MediaFrameSourceInfo colorSourceInfo = selectedGroupObjects?.colorSourceInfo;

            if (selectedGroup == null)
            {
                dbg.Log("Error: selectedGroup is null");
                return;
            }

            mediaCapture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                SourceGroup = selectedGroup,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video
            };
            await mediaCapture.InitializeAsync(settings);

            var colorFrameSource = mediaCapture.FrameSources[colorSourceInfo.Id];
            var preferredFormat = colorFrameSource.SupportedFormats.Where(format =>
            {
                return format.VideoFormat.Width < 1080
                //&& format.Subtype == MediaEncodingSubtypes.Argb32
                && (int)Math.Round((double)format.FrameRate.Numerator / format.FrameRate.Denominator) == 30;

            }).FirstOrDefault();

            if (preferredFormat == null)
            {
                // Our desired format is not supported
                dbg.Log("Error: could not find format");
                return;
            }

            dbg.Log($"Using format with Subtype {preferredFormat.Subtype}");

            await colorFrameSource.SetFormatAsync(preferredFormat);

            mediaCapture.Failed += (MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs) =>
            {
                dbg.Log($"MediaCapture initialization failed: {errorEventArgs.Message}");
            };

            videoFrameReader = await mediaCapture.CreateFrameReaderAsync(colorFrameSource);
            videoFrameReader.FrameArrived += FrameArrived;
        }
        catch (Exception e)
        {
            dbg.Log(e.ToString());
        }
    }

    async void SaveSoftwareBitmapToFile(SoftwareBitmap softwareBitmap, StorageFile outputFile)
    {
        dbg.Log($"Software bitmap has dimension {softwareBitmap.PixelWidth} x {softwareBitmap.PixelHeight}, format = {softwareBitmap.BitmapPixelFormat}");
        using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            // Create an encoder with the desired format
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            dbg.Log("Created encoder");

            // Set the software bitmap
            try 
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                dbg.Log("Converted bitmap");
                encoder.SetSoftwareBitmap(softwareBitmap);
                dbg.Log("Set Bitmap");
            }
            catch (Exception e)
            {
                dbg.Log($"Caught exception trying to convert and set bitmap: '{e.ToString()}'");
            }

            encoder.IsThumbnailGenerated = true;

            try
            {
                await encoder.FlushAsync();
                dbg.Log("Flushed to encoder");
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
                try
                {
                    await encoder.FlushAsync();
                    dbg.Log("Flushed to encoder for real");
                }
                catch(Exception e)
                {
                    dbg.Log($"Could not flush to image: '{e.ToString()}'");
                }

            }

        }
    }

    async void FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        // Don't do anything if no point cloud was captured
        if (current_animation.Count == 0) 
            return;

        // Current point cloud already has an image associated with it
        if (current_animation.GetLast().Bitmap != null)
            return;

        using (var frame = sender.TryAcquireLatestFrame())
        {
            if (frame != null) 
            {
                // TODO: see https://github.com/microsoft/psi/blob/cb2651f8e591c63d4a1fc8a16ad08ec7196338eb/Sources/MixedReality/Microsoft.Psi.MixedReality.UniversalWindows/MediaCapture/PhotoVideoCamera.cs#L529
                // Compute pose

                // TODO: do something with it
                // Get camera intrinsics
                var intrinsics = frame.VideoMediaFrame.CameraIntrinsics;

                using (var frameBitmap = frame.VideoMediaFrame.SoftwareBitmap)
                {
                    if (frameBitmap == null)
                    {
                        dbg.Log("frameBitmap is null!");
                    }
                    // Copies bitmap to point cloud
                    current_animation.GetLast().Bitmap = frameBitmap;
                }

            }
        }

    }
#endif


    public async void ToggleRecording()
    {
        recording = !recording;
        // Write captured point cloud animation to disk
        if (!recording)
        {
            try
            {
                // Stop video stream
#if ENABLE_WINMD_SUPPORT
                await videoFrameReader.StopAsync();
                var myPictures = await Windows.Storage.StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
                dbg.Log($"Starting export of {bitmaps.Count} images");
                for (int i = 0; i < bitmaps.Count; ++i)
                {
                    StorageFile file = await myPictures.SaveFolder.CreateFileAsync($"photo_{i}.jpg", CreationCollisionOption.GenerateUniqueName);
                    SaveSoftwareBitmapToFile((SoftwareBitmap)bitmaps[i], file);
                }
#endif
                dbg.Log(string.Format("Started export of {0} point clouds, frame rate = {1}",
                    current_animation.Count,
                    frames / (DateTime.Now - recordingStart).TotalSeconds));
                // TODO: uncomment
                //current_animation.ExportToPLY(DateTime.Now.ToString("dd-MM-yyyyTHH_mm"));
                // Allocate new PC collection for next animation, freeing memory
                // for animation that was just written

                current_animation = new PointCloudCollection();
                dbg.Log($"Photo frame rate = {picture / (DateTime.Now - recordingStart).TotalSeconds}");
            }
            catch (Exception e)
            {
                dbg.Log(e.ToString());
            }
        }
        else
        {
            recordingStart = DateTime.Now;
#if ENABLE_WINMD_SUPPORT
            var status = await videoFrameReader.StartAsync();
            if (status == MediaFrameReaderStartStatus.Success)
            {
                dbg.Log("Successfully started mediaframereader!");
            }
            else
            {
                dbg.Log($"Failed to start mediaframereader!, status = {status}");
            }
#endif
            frames = 0;
            picture = 0;
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

