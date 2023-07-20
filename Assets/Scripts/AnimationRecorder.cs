using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Windows.WebCam;
using System.Numerics;
using UnityEngine.Experimental.Rendering;
using MathNet.Spatial.Euclidean;
using MathNet.Numerics.LinearAlgebra;

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
        float[] mappings;
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
            //InitResearchMode();
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

            //if (researchMode == null)
            //{
            //    dbg.Log("Research mode is null");
            //    try 
            //    {
            //        InitResearchMode();
            //    } 
            //    catch (Exception e)
            //    {
            //        dbg.Log("Cannot init researchmode! -- " + e.ToString());
            //    }
            //}

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
        //dbg.Log("Frame arrived");
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
                        CaptureJointPositions(colorFrame);
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

    protected void CaptureJointPositions(ColorFrame cf)
    {
        // Get hand positions for segmentation
        MixedRealityPose pose;

        foreach (var jointName in Enum.GetNames(typeof(TrackedHandJoint)))
        {
            TrackedHandJoint joint = (TrackedHandJoint) Enum.Parse(typeof(TrackedHandJoint), jointName);
            if (HandJointUtils.TryGetJointPose(joint, Handedness.Right, out pose))
            {
                cf.rightJoints.Add(pose.Position);
            }
            else
            {
                cf.rightJoints.Add(UnityEngine.Vector3.zero);
            }

            if (HandJointUtils.TryGetJointPose(joint, Handedness.Left, out pose))
            {
                cf.leftJoints.Add(pose.Position);
            }
            else
            {
                cf.leftJoints.Add(UnityEngine.Vector3.zero);
            }
        }

    }

#endif
    private void PollDepthSensor()
    {
//        long timeStamp = 0;
//        System.Random rand = new System.Random(0);
//        //float[] samplePoints = new float[20];
//        //for (int i = 0; i < 20; ++i)
//        //{
//        //    samplePoints[i] = (float)(rand.NextDouble() * 512);
//        //}
//        while (recording)
//        {
//#if ENABLE_WINMD_SUPPORT
//                    if (researchMode.PointCloudUpdated())
//                    {
//                        try 
//                        {
//                            // Append depth frame
//                            //ushort[] depthFrame = new ushort[512 * 512];
//                            //Array.Copy(researchMode.GetDepthMapBuffer(out timeStamp), depthFrame, 512 * 512);
//                            //dbg.Log($"Got new depthmaptexture with timestamp {timeStamp}!");
//                            //DepthFrame frame = new DepthFrame(timeStamp, depthFrame);

//                            //researchMode.GetDepthMapBuffer(out timeStamp);
//                            //DepthFrame frame = new DepthFrame(timeStamp, null);

//                            var coordinates = researchMode.GetPointCloudBuffer();
//                            if (coordinates == null) 
//                            {
//                                dbg.Log("coordinates are null");
//                                continue;
//                            }
//                            frame.pc = new PointCloud(coordinates, true);
//        ;
//                            // Get mapping for optimization
//                            //if (this.mappings == null)
//                            //    this.mappings = researchMode.GetMappings();

//                            frame.extrinsics = System.Numerics.Matrix4x4.Identity;
//                            float[] M = researchMode.GetCurrentDepthToWorld();
//                            System.Numerics.Matrix4x4 ext = new System.Numerics.Matrix4x4();

//                            // This is ridiculous but apparently indexing a matrix by row/col is not supported :(
//                            ext.M11 = M[0];
//                            ext.M12 = M[1];
//                            ext.M13 = M[2];
//                            ext.M14 = M[3];
//                            ext.M21 = M[4];
//                            ext.M22 = M[5];
//                            ext.M23 = M[6];
//                            ext.M24 = M[7];
//                            ext.M31 = M[8];
//                            ext.M32 = M[9];
//                            ext.M33 = M[10];
//                            ext.M34 = M[11];
//                            ext.M41 = M[12];
//                            ext.M42 = M[13];
//                            ext.M43 = M[14];
//                            ext.M44 = M[15];

//                            frame.extrinsics = ext;

//                            depthFrames.Add(frame);
//                        }
//                        catch (Exception e)
//                        {
//                            dbg.Log($"Caught exception while polling depth sensor: '{e.ToString()}'");
//                        }
//                    }
//#endif
//        }
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
                // Stop depth capture
                researchMode.StopDepthSensorLoop();
                //depthPollingThread.Join();
                // Stop video stream
                await videoFrameReader.StopAsync();
                var recordingEnd = DateTime.Now;
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

                        using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/rgb/joints_{i:D6}.txt"))
                        {
                            // Write pose
                            foreach (var joint in frame.leftJoints)
                            {
                                UnityEngine.Vector3 vecJoint = (UnityEngine.Vector3)joint;
                                writer.WriteLine($"{vecJoint.x} {vecJoint.y} {vecJoint.z}");
                            }

                            foreach (var joint in frame.rightJoints)
                            {
                                UnityEngine.Vector3 vecJoint = (UnityEngine.Vector3)joint;
                                writer.WriteLine($"{vecJoint.x} {vecJoint.y} {vecJoint.z}");
                            }

                        }

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

                        dbg.Log($"Processed rgb frame {i+1} of {colorFrames.Count}, number of depth images is {researchMode.GetPointCloudCount()}");
                    }


                    //for (int i = 0; i < depthFrames.Count; ++i)
                    //{
                    //    //Texture2D depthTexture = new Texture2D(512, 512);//, TextureFormat.R16, false);
                    //    //ushort[] depths = (ushort[])depthFrames[i];
                    //    DepthFrame frame = (DepthFrame)depthFrames[i];
                    //    //ushort[] depths = frame.data;

                    //    //var pixels = new Color32[512*512];
                    //    //for(int row = 0; row < 512; ++row)
                    //    //{
                    //    //    for(int col = 0; col < 512; ++col)
                    //    //    {
                    //    //        int idx = 512 * row + col;
                    //    //        ushort val = depths[idx];
                    //    //        if (val > 4090) val = 0;
                    //    //        if (val > max) max = val;

                    //    //        pixels[idx] = new Color32((byte)(val >> 8), (byte)(val & 0xFF), 0, 0xFF);
                    //    //        //depthTexture.SetPixel(512 - row, 512 - col,
                    //    //        //            new Color32((byte)(val / 256), (byte)(val % 256), 0, 255));
                    //    //    }
                    //    //}
                    //    //depthTexture.SetPixels32(pixels);
                    //    //depthTexture.Apply();
                    //    //byte[] bytes = ImageConversion.EncodeToPNG(depthTexture);
                    //    //File.WriteAllBytes($"{storageFolder.Path}/depth/{i:D6}.png", bytes);

                    //    using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/depth_old/meta_{i:D6}.txt"))
                    //    {
                    //        // Write time stamp to correlate rgb and depth images
                    //        writer.WriteLine($"{frame.timeStamp}");

                    //        // Write pose
                    //        writer.WriteLine($"{frame.extrinsics.M11}, {frame.extrinsics.M12}, {frame.extrinsics.M13}, {frame.extrinsics.M14}");
                    //        writer.WriteLine($"{frame.extrinsics.M21}, {frame.extrinsics.M22}, {frame.extrinsics.M23}, {frame.extrinsics.M24}");
                    //        writer.WriteLine($"{frame.extrinsics.M31}, {frame.extrinsics.M32}, {frame.extrinsics.M33}, {frame.extrinsics.M34}");
                    //        writer.WriteLine($"{frame.extrinsics.M41}, {frame.extrinsics.M42}, {frame.extrinsics.M43}, {frame.extrinsics.M44}");
                    //        // TODO: remove
                    //        writer.WriteLine($"Points: {frame.pc.Count}");

                    //    }

                    //    //frame.pc.Build();
                    //    frame.pc.ExportToPLY($"{storageFolder.Path}/depth_old/{i:D6}.ply");
                    //    dbg.Log($"Processed depth frame {i+1} old!");
                    //}


                    //using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/depth/mappings.txt"))
                    //{
                    //    for (int j = 0; j < 512 * 512; ++j) 
                    //    {
                    //        writer.WriteLine($"{this.mappings[3*j]} {this.mappings[3*j+1]} {this.mappings[3*j+2]}");
                    //    }
                    //}


                    //dbg.Log("Starting to optimize");
                    //List<Point2D> imagePoints = new List<Point2D>();
                    //List<Point3D> worldPoints = new List<Point3D>();
                    //// Convert data to right format and optimize
                    //for (int i = 0; i < 512; i++)
                    //{
                    //    for (int j = 0; j < 512; j++)
                    //    {
                    //        imagePoints.Add(new Point2D(j + 0.5, i + 0.5 ));
                    //        worldPoints.Add(
                    //            new Point3D(this.mappings[3*(i * 512 + j) + 0],
                    //                        this.mappings[3*(i * 512 + j) + 1],
                    //                        this.mappings[3*(i * 512 + j) + 2]));
                    //    }
                    //}

                    //// Initialize a starting camera matrix
                    //var initialCameraMatrix = Matrix<double>.Build.Dense(3, 3);
                    //var initialDistortion = Vector<double>.Build.Dense(2);
                    //initialCameraMatrix[0, 0] = 250; // fx
                    //initialCameraMatrix[1, 1] = 250; // fy
                    //initialCameraMatrix[0, 2] = 256; // cx
                    //initialCameraMatrix[1, 2] = 256; // cy
                    //initialCameraMatrix[2, 2] = 1;


                    //// Run optimization
                    //Calibration.CalibrateCameraIntrinsics(worldPoints,
                    //                                      imagePoints,
                    //                                      initialCameraMatrix,
                    //                                      initialDistortion,
                    //                                      out var computedCameraMatrix,
                    //                                      out var computedDistortionCoefficients,
                    //                                      false);


                    //// Write intrinsics to file
                    //using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/depth/cameraParams.txt"))
                    //{
                    //    writer.WriteLine($"{computedCameraMatrix[0, 0]} {computedCameraMatrix[1, 1]} {computedCameraMatrix[0, 2]} {computedCameraMatrix[1, 2]} {computedDistortionCoefficients[0]} {computedDistortionCoefficients[1]}");
                    //}

                    //dbg.Log($"Done optimizing, computed coefficients: {computedCameraMatrix[0, 0]} {computedCameraMatrix[1, 1]} {computedCameraMatrix[0, 2]} {computedCameraMatrix[1, 2]} {computedDistortionCoefficients[0]} {computedDistortionCoefficients[1]}");

                    // Construct point clouds and write them to files
                    for (uint i = 0; i < researchMode.GetPointCloudCount(); ++i) 
                    {
                        long timeStamp = 0;
                        float[] coordinates = researchMode.GetPointCloud(i, out timeStamp);
                        PointCloud pc = new PointCloud(coordinates, true);
                        float[] M = researchMode.GetDepthToWorld(i);
                        pc.ExportToPLY($"{storageFolder.Path}/depth/{i:D6}.ply");

                        using (StreamWriter writer = new StreamWriter($"{storageFolder.Path}/depth/meta_{i:D6}.txt"))
                        {
                            // Write time stamp to correlate rgb and depth images
                            writer.WriteLine($"{timeStamp}");

                            // Write pose
                            writer.WriteLine($"{M[0]}, {M[1]}, {M[2]}, {M[3]}");
                            writer.WriteLine($"{M[4]}, {M[5]}, {M[6]}, {M[7]}");
                            writer.WriteLine($"{M[8]}, {M[9]}, {M[10]}, {M[11]}");
                            writer.WriteLine($"{M[12]}, {M[13]}, {M[14]}, {M[15]}");
                        }

                    dbg.Log($"Processed point cloud {i+1}");

                    }

                    // Reset frames and mapping
                    colorFrames = new ArrayList();
                    depthFrames = new ArrayList();
                    //this.mappings = null;

                    double seconds = (recordingEnd - recordingStart).TotalSeconds;
                    dbg.Log($"Done processing! Depth frame rate = {researchMode.GetPointCloudCount() / seconds}, RGB frame rate = {colorFrames.Count / seconds}");
                }
                catch(Exception e)
                {
                    dbg.Log($"Error while saving images: '{e.ToString()}'");
                }
#endif
                //dbg.Log(string.Format("Started export of {0} point clouds, frame rate = {1}",
                //    current_animation.Count,
                //    frames / (DateTime.Now - recordingStart).TotalSeconds));
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
            recording = true;
            recordingStart = DateTime.Now;
#if ENABLE_WINMD_SUPPORT

            try 
            {
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

                // Start depth capture
                //researchMode.StartDepthSensorLoop(true);
                InitResearchMode();
            }
            catch(Exception e)
            {
                dbg.Log(e.ToString());
            }
#endif
            //depthPollingThread = new Thread(new ThreadStart(PollDepthSensor));
            //depthPollingThread.Start();
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

            researchMode.StartDepthSensorLoop(true);
            //researchMode.StartSpatialCamerasFrontLoop();
            dbg.Log("Initialized research mode");
        } catch (Exception e) {
            dbg.Log(e.ToString());
        }
#endif
    }

}