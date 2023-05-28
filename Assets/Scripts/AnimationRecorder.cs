using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Windows.WebCam;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
using Windows.Storage;
using Windows.Storage.Streams;
using StreamBuffer = Windows.Storage.Streams.Buffer;
using Windows.Media.Capture;
using Windows.Perception.Spatial;
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
    // TODO: remove
    int frames;
    int picture = 0;
    private Texture2D targetTexture = null;
    private CameraParameters cameraParameters;
    private DateTime recordingStart;

    private bool recording = false;
    private Thread depthPollingThread = null;
#if ENABLE_WINMD_SUPPORT
        private MediaCapture mediaCapture;
        private LowLagPhotoCapture photoCapture;
        private MediaFrameReader videoFrameReader;
        HL2ResearchMode researchMode;
        bool isCapturing;
        Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
        ArrayList bitmaps;
        ArrayList depthFrames;
        ArrayList mappings;
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
        depthFrames = new ArrayList();
        mappings = new ArrayList();
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

            //if (!researchMode.PointCloudUpdated()) {
            //    //dbg.Log("point cloud not updated, returning now");
            //    return;
            //}

            //float[] points = researchMode.GetPointCloudBuffer();
            //if (points == null)
            //{
            //    dbg.Log("points is null");
            //    return;
            //}

            //dbg.Log(string.Format("{0} points in PointCloudBuffer", points.Length));

            //++frames;

            //// TODO: uncomment
            ////Create point cloud and add to animation
            //PointCloud pointCloud = new PointCloud(points, .5);
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
        using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            // Create an encoder with the desired format
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);

            // Set the software bitmap
            try 
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                encoder.SetSoftwareBitmap(softwareBitmap);
            }
            catch (Exception e)
            {
                dbg.Log($"Caught exception trying to convert and set bitmap: '{e.ToString()}'");
            }

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
                try
                {
                    await encoder.FlushAsync();
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
        dbg.Log("Frame arrived");
        try 
        {
            using (var frame = sender.TryAcquireLatestFrame())
            {
                if (frame != null) 
                {
                    // see https://github.com/microsoft/psi/blob/cb2651f8e591c63d4a1fc8a16ad08ec7196338eb/Sources/MixedReality/Microsoft.Psi.MixedReality.UniversalWindows/MediaCapture/PhotoVideoCamera.cs#L529

                    // Compute pose
                    SpatialCoordinateSystem extrinsics = frame.CoordinateSystem;

                    // Timestamp
                    System.TimeSpan? timestamp = frame.SystemRelativeTime;

                    // TODO: Save all of that together with the image to an object and export that to a text file

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
                    //current_animation.GetLast().cameraMatrix = projMat;

                    using (var frameBitmap = frame.VideoMediaFrame.SoftwareBitmap)
                    {
                        if (frameBitmap == null)
                        {
                            dbg.Log("frameBitmap is null!");
                        }
                        // Copies bitmap to point cloud
                        bitmaps.Add(SoftwareBitmap.Copy(frameBitmap));


                    }

                } else {
                    dbg.Log("frame is null");
                }
            }
        }
        catch(Exception e)
        {
            dbg.Log($"Caught exception in FrameArrived: '{e.ToString()}'");
        }

    }

    //protected void CaptureJointPositions(out ArrayList leftHandPoses, out ArrayList rightHandPoses)
    //{
        //// Get hand positions for segmentation
        //MixedRealityPose pose;

        //foreach (int joint in Enum.GetValues(typeof(TrackedHandJoint)))
        //{
        //    if (HandJointUtils.TryGetJointPose(joint, Handedness.Right, out pose))
        //    {
        //        rightHandPoses.Add(pose);
        //    }
        //    else
        //    {
        //        rightHandPoses.Add(null);
        //    }

        //    if (HandJointUtils.TryGetJointPose(joint, Handedness.Left, out pose))
        //    {
        //        leftHandPoses.Add(pose);
        //    }
        //    else
        //    {
        //        leftHandPoses.Add(null);
        //    }
        //}

    //}

#endif
    private void PollDepthSensor()
    {
        long timeStamp = 0;
        System.Random rand = new System.Random(0);
        float[] samplePoints = new float[20];
        for (int i = 0; i < 20; ++i)
        {
            samplePoints[i] = (float) (rand.NextDouble() * 512);
        }
        while (recording)
        {
#if ENABLE_WINMD_SUPPORT
            if (researchMode.DepthMapTextureUpdated())
            {
                // Append depth frame
                //ushort[] depthFrame = new ushort[512 * 512];
                //Array.Copy(researchMode.GetDepthMapBuffer(), depthFrame, 512 * 512);
                byte[] depthFrame = new byte[512 * 512];
                Array.Copy(researchMode.GetDepthMapTextureBuffer(out timeStamp), depthFrame, 512 * 512);
                dbg.Log($"Got new depthmaptexture with timestamp {timeStamp}!");
                float[] xy = researchMode.GetLUTEntries(samplePoints);
                mappings.Add((samplePoints, xy));
                depthFrames.Add(new Frame<byte>(timeStamp, depthFrame));
            }
#endif
        }
    }

    public async void ToggleRecording()
    {
        // Write captured point cloud animation to disk
        if (recording)
        {
            try
            {
                recording = false;
#if ENABLE_WINMD_SUPPORT
                // Stop video stream
                await videoFrameReader.StopAsync();
                // Stop polling depth sensor
                depthPollingThread.Join();
                try 
                {
                    Windows.Storage.StorageFolder storageFolder = KnownFolders.Objects3D;
                    for (int i = 0; i < bitmaps.Count; ++i)
                    {
                        SoftwareBitmap bmp = SoftwareBitmap.Convert((SoftwareBitmap)bitmaps[i], 
                                    BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight);

                        Windows.Storage.StorageFile file =
                            await (await storageFolder.GetFolderAsync("rgb")).CreateFileAsync($"{i:D6}.png", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                        SaveSoftwareBitmapToFile(bmp, file);

                        dbg.Log($"Processed rgb frame {i+1} of {bitmaps.Count}, number of depth images is {depthFrames.Count}");
                    }


                    for (int i = 0; i < depthFrames.Count; ++i)
                    {
                        Texture2D depthTexture = new Texture2D(512, 512);
                        //ushort[] depths = (ushort[])depthFrames[i];
                        Frame<byte> frame = (Frame<byte>)depthFrames[i];
                        byte[] depths = frame.data;

                        // TODO: fix this
                        for(int row = 0; row < 512; ++row)
                        {
                            for(int col = 0; col < 512; ++col)
                            {
                                
                                //ushort val = depths[512 * row + col];
                                //float intensity =  val > 4090 ? 0f : val / 4090f;
                                byte val = depths[512 * col + row];
                                float intensity =  val / 255f;
                                depthTexture.SetPixel(row, col, new Color(intensity, intensity, intensity));
                            }
                        }

                        byte[] bytes = ImageConversion.EncodeToPNG(depthTexture);
                        File.WriteAllBytes($"{storageFolder.Path}/depth/{i:D6}.png", bytes);

                        dbg.Log($"Processed depth frame {i+1}");
                    }
                    var map = (ValueTuple<float[], float[]>)(mappings[0]);
                    float[] uv = map.Item1;
                    float[] xy = map.Item2;

                    using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/mapping.txt"))
                    {
                        for(int i = 0; i < xy.Length; i += 2) 
                        {
                            writer.WriteLine($"({xy[i]}, {xy[i+1]}) maps to ({uv[i]}, {uv[i+1]})");
                        }
                    }
                }
                catch(Exception e)
                {
                    dbg.Log($"Error while saving images: '{e.ToString()}'");
                }
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
            recording = true;
            recordingStart = DateTime.Now;
            depthPollingThread = new Thread(new ThreadStart(PollDepthSensor));
            depthPollingThread.Start();
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