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
using StreamBuffer = Windows.Storage.Streams.Buffer;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;
using System.Runtime.InteropServices.WindowsRuntime;
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

            float[] points = researchMode.GetPointCloudBuffer();
            if (points == null)
            {
                dbg.Log("points is null");
                return;
            }

            //dbg.Log(string.Format("{0} points in PointCloudBuffer", points.Length));

            ++frames;

            // TODO: uncomment
            //Create point cloud and add to animation
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
            foreach(var src in frameSourceGroups)
            {
                foreach(var info in src.SourceInfos)
                {
                    dbg.Log($"There is a source with kind {info.SourceKind}");
                }
            }
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

            //MediaCapture test = new MediaCapture();
            //var test_settings = new MediaCaptureInitializationSettings();
            //await test.InitializeAsync(test_settings);
            //var colorTest = test.FrameSources[MediaFrameSourceKind.Color];
            //var depthTest = test.FrameSources[MediaFrameSourceKind.Depth];

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
        if (current_animation.Count == 0) {
            dbg.Log("No point cloud has been captured yet");
            return;
        }

        // Current point cloud already has an image associated with it
        if (current_animation.GetLast().imageIdx != -1) {
            dbg.Log("Point cloud already got image");
            return;
        }

        using (var frame = sender.TryAcquireLatestFrame())
        {
            if (frame != null) 
            {
                // TODO: see https://github.com/microsoft/psi/blob/cb2651f8e591c63d4a1fc8a16ad08ec7196338eb/Sources/MixedReality/Microsoft.Psi.MixedReality.UniversalWindows/MediaCapture/PhotoVideoCamera.cs#L529
                // Compute pose

                // Get camera intrinsics
                var intrinsics = frame.VideoMediaFrame.CameraIntrinsics;

                // TODO: maybe use CameraIntrinsics instead of matrix
                // Constrict matrix from it
                Matrix4x4 projMat = new Matrix4x4();
                projMat[2,2] = 1;
                // Focal lengths
                projMat[0,0] = intrinsics.FocalLength.X;
                projMat[1,1] = intrinsics.FocalLength.Y;
                // Principal point
                projMat[0,2] = intrinsics.PrincipalPoint.X;
                projMat[1,2] = intrinsics.PrincipalPoint.Y;
                current_animation.GetLast().cameraMatrix = projMat;

                using (var frameBitmap = frame.VideoMediaFrame.SoftwareBitmap)
                {
                    if (frameBitmap == null)
                    {
                        dbg.Log("frameBitmap is null!");
                    }
                    dbg.Log("Added bitmap to array");
                    // Copies bitmap to point cloud
                    bitmaps.Add(SoftwareBitmap.Copy(frameBitmap));
                    current_animation.GetLast().imageIdx = bitmaps.Count - 1;
                }

            } else {
                dbg.Log("frame is null");
            }
        }

    }
#endif


    public async void ToggleRecording()
    {
        // Write captured point cloud animation to disk
        if (recording)
        {
            try
            {
                recording = false;
                // Stop video stream
#if ENABLE_WINMD_SUPPORT
                await videoFrameReader.StopAsync();
                int nColorless = 0;
                for (int i = 0; i < current_animation.Count; ++i)
                {
                    PointCloud cur = current_animation.Get(i);
                    if (cur.imageIdx == -1)
                    {
                        dbg.Log("Found point cloud without color information!");
                        ++nColorless;
                        continue;
                    }

                    SoftwareBitmap bmp = SoftwareBitmap.Convert((SoftwareBitmap)bitmaps[cur.imageIdx], 
                                BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight);

                    Windows.Storage.StorageFolder storageFolder = KnownFolders.Objects3D;
                    Windows.Storage.StorageFile file =
                        await storageFolder.CreateFileAsync($"image_{i}.jpg", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                    SaveSoftwareBitmapToFile(bmp, file);

                    IBuffer buf = new StreamBuffer((uint) (bmp.PixelWidth * bmp.PixelHeight * 4));
                    dbg.Log($"Before copy: {bmp.PixelWidth}x{bmp.PixelHeight} ==> {buf.Length}");
                    bmp.CopyToBuffer(buf);
                    dbg.Log($"After copy: {bmp.PixelWidth}x{bmp.PixelHeight} ==> {buf.Length}");
                    byte[] bytes = new byte[buf.Length];
                    dbg.Log($"Length of bytes array: {bytes.Length}");
                    Texture2D tex = new Texture2D(bmp.PixelWidth, bmp.PixelHeight);
                    WindowsRuntimeBufferExtensions.CopyTo(buf, bytes);
                    //tex.LoadRawTextureData(bytes);
                    // Kind of hacky...
                    Color[] colors = new Color[bmp.PixelWidth * bmp.PixelHeight];
                    for (int j = 0; j < bmp.PixelWidth * bmp.PixelHeight; ++j)
                    {
                        colors[i] = new Color(bytes[4*i] / 255f, 
                                              bytes[4*i+1] / 255f, 
                                              bytes[4*i+2] / 255f, 
                                              bytes[4*i+3] / 255f);
                    }

                    tex.Apply();
                    cur.ColorFromImage(tex);
                }
                dbg.Log($"{nColorless} clouds without color!!!1!!!1!");
#endif
                dbg.Log(string.Format("Started export of {0} point clouds, frame rate = {1}",
                    current_animation.Count,
                    frames / (DateTime.Now - recordingStart).TotalSeconds));
                current_animation.ExportToPLY(DateTime.Now.ToString("dd-MM-yyyyTHH_mm"));
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
            recording = true;
            frames = 0;
            picture = 0;
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

}
