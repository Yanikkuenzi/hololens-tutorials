using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Windows.WebCam;
using System.Numerics;
using UnityEngine.Experimental.Rendering;

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
        ArrayList colorFrames;
        ArrayList depthFrames;
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
        colorFrames = new ArrayList();
        depthFrames = new ArrayList();
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
                    System.Numerics.Matrix4x4 M = extrinsics.TryGetTransformTo(unityWorldOrigin) is System.Numerics.Matrix4x4 mat
                                        ? mat 
                                        : System.Numerics.Matrix4x4.Identity;

                    // Timestamp
                    long ticks = frame.SystemRelativeTime is TimeSpan s ? s.Ticks : -1;

                    // TODO: Save all of that together with the image to an object and export that to a text file

                    // Get camera intrinsics
                    var intrinsics = frame.VideoMediaFrame.CameraIntrinsics;

                    // Constrict matrix from it
                    System.Numerics.Matrix4x4 projMat = System.Numerics.Matrix4x4.Identity;
                    // Focal lengths
                    projMat.M11 = intrinsics.FocalLength.X;
                    projMat.M22 = intrinsics.FocalLength.Y;
                    // Principal point
                    projMat.M13 = intrinsics.PrincipalPoint.X;
                    projMat.M23 = intrinsics.PrincipalPoint.Y;

                    using (var frameBitmap = frame.VideoMediaFrame.SoftwareBitmap)
                    {
                        if (frameBitmap == null)
                        {
                            dbg.Log("frameBitmap is null!");
                        }
                        // Copies bitmap to point cloud
                        ColorFrame colorFrame = new ColorFrame(ticks, SoftwareBitmap.Copy(frameBitmap));
                        colorFrame.extrinsics = M is System.Numerics.Matrix4x4 ext ? ext : System.Numerics.Matrix4x4.Identity;
                        colorFrame.intrinsics = projMat;
                        colorFrames.Add(colorFrame);
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

                //byte[] depthFrame = new byte[512 * 512];
                //Array.Copy(researchMode.GetDepthMapTextureBuffer(out timeStamp), depthFrame, 512 * 512);
                ushort[] depthFrame = new ushort[512 * 512];
                Array.Copy(researchMode.GetDepthMapBuffer(out timeStamp), depthFrame, 512 * 512);
                dbg.Log($"Got new depthmaptexture with timestamp {timeStamp}!");
                DepthFrame frame = new DepthFrame(timeStamp, depthFrame);
                frame.xy = researchMode.GetLUTEntries(samplePoints);
                frame.uv = samplePoints;
                frame.extrinsics = System.Numerics.Matrix4x4.Identity;
                float[] M = researchMode.GetDepthToWorld();

                System.Numerics.Matrix4x4 ext = new System.Numerics.Matrix4x4();

                // This is ridiculous but apparently indexing a matrix by row/col is not supported :(
                ext.M11 = M[0];
                ext.M12 = M[1];
                ext.M13 = M[2];
                ext.M14 = M[3];
                ext.M21 = M[4];
                ext.M22 = M[5];
                ext.M23 = M[6];
                ext.M24 = M[7];
                ext.M31 = M[8];
                ext.M32 = M[9];
                ext.M33 = M[10];
                ext.M34 = M[11];
                ext.M41 = M[12];
                ext.M42 = M[13];
                ext.M43 = M[14];
                ext.M44 = M[15];

                frame.extrinsics = ext;
                depthFrames.Add(frame);
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
                // Stop polling depth sensor
                depthPollingThread.Join();
                // Stop video stream
                await videoFrameReader.StopAsync();
                try 
                {
                    Windows.Storage.StorageFolder storageFolder = KnownFolders.Objects3D;
                    string rgbPath = $"{storageFolder.Path}/rgb";
                    string depthPath = $"{storageFolder.Path}/depth";
                    if (!Directory.Exists(rgbPath)) 
                    {
                        Directory.CreateDirectory(rgbPath);
                    }
                    if (!Directory.Exists(depthPath)) 
                    {
                        Directory.CreateDirectory(depthPath);
                    }

                    for (int i = 0; i < colorFrames.Count; ++i)
                    {
                        var frame = (ColorFrame)colorFrames[i];
                        SoftwareBitmap bmp = SoftwareBitmap.Convert((SoftwareBitmap)(frame.bitmap), 
                                    BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight);

                        Windows.Storage.StorageFile file =
                            await (await storageFolder.GetFolderAsync("rgb")).CreateFileAsync($"{i:D6}.png", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                        SaveSoftwareBitmapToFile(bmp, file);

                        using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/rgb/meta_{i:D6}.txt"))
                        {
                            // Write time stamp to correlate rgb and depth images
                            writer.WriteLine($"{frame.timeStamp}");

                            // Write pose
                            writer.WriteLine($"{frame.extrinsics.M11}, {frame.extrinsics.M12}, {frame.extrinsics.M13}, {frame.extrinsics.M14}");
                            writer.WriteLine($"{frame.extrinsics.M21}, {frame.extrinsics.M22}, {frame.extrinsics.M23}, {frame.extrinsics.M24}");
                            writer.WriteLine($"{frame.extrinsics.M31}, {frame.extrinsics.M32}, {frame.extrinsics.M33}, {frame.extrinsics.M34}");
                            writer.WriteLine($"{frame.extrinsics.M41}, {frame.extrinsics.M42}, {frame.extrinsics.M43}, {frame.extrinsics.M44}");

                            // Write intrinsics
                            writer.WriteLine($"{frame.intrinsics.M11}, {frame.intrinsics.M12}, {frame.intrinsics.M13}, {frame.intrinsics.M14}");
                            writer.WriteLine($"{frame.intrinsics.M21}, {frame.intrinsics.M22}, {frame.intrinsics.M23}, {frame.intrinsics.M24}");
                            writer.WriteLine($"{frame.intrinsics.M31}, {frame.intrinsics.M32}, {frame.intrinsics.M33}, {frame.intrinsics.M34}");
                            writer.WriteLine($"{frame.intrinsics.M41}, {frame.intrinsics.M42}, {frame.intrinsics.M43}, {frame.intrinsics.M44}");
                        }
                        dbg.Log($"Processed rgb frame {i+1} of {colorFrames.Count}, number of depth images is {depthFrames.Count}");
                    }


                    for (int i = 0; i < depthFrames.Count; ++i)
                    {
                        ushort max = 0;
                        Texture2D depthTexture = new Texture2D(512, 512);//, TextureFormat.R16, false);
                        //ushort[] depths = (ushort[])depthFrames[i];
                        DepthFrame frame = (DepthFrame)depthFrames[i];
                        ushort[] depths = frame.data;

                        var pixels = new Color32[512*512];
                        // TODO: remove
                        depths[0] = 4090;
                        for(int row = 0; row < 512; ++row)
                        {
                            for(int col = 0; col < 512; ++col)
                            {
                                int idx = 512 * row + col;
                                ushort val = depths[idx];
                                if (val > 4090) val = 0;
                                if (val > max) max = val;

                                pixels[idx] = new Color32((byte)(val >> 8), (byte)(val & 0xFF), 0, 0xFF);
                                //depthTexture.SetPixel(512 - row, 512 - col,
                                //            new Color32((byte)(val / 256), (byte)(val % 256), 0, 255));
                            }
                        }
                        depthTexture.SetPixels32(pixels);
                        depthTexture.Apply();
                        byte[] bytes = ImageConversion.EncodeToPNG(depthTexture);
                        File.WriteAllBytes($"{storageFolder.Path}/depth/{i:D6}.png", bytes);

                        using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/depth/meta_{i:D6}.txt"))
                        {
                            // Write time stamp to correlate rgb and depth images
                            writer.WriteLine($"{frame.timeStamp}");

                            // Write pose
                            writer.WriteLine($"{frame.extrinsics.M11}, {frame.extrinsics.M12}, {frame.extrinsics.M13}, {frame.extrinsics.M14}");
                            writer.WriteLine($"{frame.extrinsics.M21}, {frame.extrinsics.M22}, {frame.extrinsics.M23}, {frame.extrinsics.M24}");
                            writer.WriteLine($"{frame.extrinsics.M31}, {frame.extrinsics.M32}, {frame.extrinsics.M33}, {frame.extrinsics.M34}");
                            writer.WriteLine($"{frame.extrinsics.M41}, {frame.extrinsics.M42}, {frame.extrinsics.M43}, {frame.extrinsics.M44}");

                            // Write uv <-> xy correspondence to calculate depth intrinsics
                            for(int j = 0; j < frame.xy.Length; j += 2) 
                            {
                                writer.WriteLine($"{frame.xy[j]}, {frame.xy[j+1]}, {frame.uv[j]}, {frame.uv[j+1]}");
                            }
                        }

                        dbg.Log($"Processed depth frame {i+1}, max depth = {max}; new!");
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
#if ENABLE_WINMD_SUPPORT

            // Start video
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
            // Start depth capture
            depthPollingThread = new Thread(new ThreadStart(PollDepthSensor));
            depthPollingThread.Start();
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
            //researchMode.InitializeSpatialCamerasFront();
            researchMode.SetReferenceCoordinateSystem(unityWorldOrigin);
            researchMode.SetPointCloudDepthOffset(0);

            researchMode.StartDepthSensorLoop(false);
            //researchMode.StartSpatialCamerasFrontLoop();
            dbg.Log("Initialized research mode");
        } catch (Exception e) {
            dbg.Log(e.ToString());
        }
#endif
    }

}