using System.Collections;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.Video;
using System.Security.Cryptography;
using System.Net.Http.Headers;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif

public class PointCloudCollection : MonoBehaviour
{

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private Texture2D frameTexture;
    private ArrayList clouds;
    private DateTime videoStart;

    // Id of next point cloud that must be colored
    private int colorIndex = 0;

    public int Count
    {
        get => clouds.Count;
    }

    public PointCloudCollection()
    {
        clouds = new ArrayList();
    }

    private void Start()
    {
        frameTexture = new Texture2D(1, 1);
    }

    /// <summary>
    /// Appends point cloud collection with new point cloud.
    /// </summary>
    /// <param name="pointCloud">The coordinates of the point captured</param>
    public void AddPointCloud(PointCloud pc)
    {
        clouds.Add(pc);
    }

    /// <summary>
    /// Writes the stored animation consisting of point clouds to the specified directory in the
    /// 3DObjects folder on the device. The point clouds are stored individually in the ply format.
    /// </summary>
    /// <param name="directory">Name of the target directory</param>
    public void ExportToPLY(string directory)
    {

#if ENABLE_WINMD_SUPPORT
        StorageFolder objects_3d = KnownFolders.Objects3D;
        // Prepend storage location and create output directory
        directory = objects_3d.Path + "/" + directory;
#endif
        Directory.CreateDirectory(directory);
        for (int i = 0; i < clouds.Count; ++i)
        {
            ((PointCloud)clouds[i]).ExportToPLY(directory + "/" + string.Format("{0:D6}.ply", i));
        }
    }

    public PointCloud Get(int index)
    {
        if (index < 0 || index >= clouds.Count)
        {
            throw new ArgumentOutOfRangeException(string.Format("Index {0} is out of range for collection of size {1}", index, clouds.Count));
        }
        return (PointCloud)clouds[index];
    }

    public bool LoadFromPLY(string directory)
    {
        string[] filenames = null;
        try
        {
#if WINDOWS_UWP
            StorageFolder o3d = KnownFolders.Objects3D;
            string dir = o3d.Path + "/" + directory;
            filenames = Directory.GetFiles(dir, "*.ply");

#else
            filenames = Directory.GetFiles("Assets/Resources/PointClouds/" + directory, "*.ply");
#endif
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }

        //int n = Math.Min(filenames.Length, 1);
        int n = filenames.Length;
        Debug.Log(string.Format("Loading {0} ply files", n));

        // Initialize enough space for all the point clouds
        this.clouds = new ArrayList(n);

        // Make sure we get the correct order
        Array.Sort(filenames);

        // Load all the point clouds belonging to the animation
        for (int i = 0; i < n; i++)
        {
            this.clouds.Add(new PointCloud(filenames[i]));
        }

        var delta_t = (((PointCloud)clouds[clouds.Count - 1]).TimeStamp -
            ((PointCloud)clouds[0]).TimeStamp);
        Debug.Log("Time between first and last pointcloud is " + delta_t.TotalSeconds + "s, first @ "  + ((PointCloud)clouds[0]).TimeStamp.ToString("yyyy-MM-dd HH:mm:ss,fff"));

        return true;
    }

    public void ColorFromVideo(string path)
    {
        GameObject playerObject = new GameObject("VideoPlayer");
        videoPlayer = playerObject.AddComponent<VideoPlayer>();
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.url = path;
        videoPlayer.sendFrameReadyEvents = true;
        videoPlayer.frameReady += OnFrameReady;
        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.Prepare();
        videoStart = DateTime.ParseExact("2023-05-01 09:21:40,362", "yyyy-MM-dd HH:mm:ss,fff", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void OnFrameReady(VideoPlayer source, long idx)
    {
        if (colorIndex >= clouds.Count)
            return;

        // Calculate if ready frame is the one needed to calculate 
        // color for next cloud
        double delta_t = (((PointCloud)clouds[colorIndex]).TimeStamp - videoStart).TotalSeconds;
        Debug.Log("delta_t = " + delta_t + " waiting for frame " + (int) (delta_t * 30) + " current frame is " + idx);

        // Not the right frame for the current cloud
        if (idx < (int)(delta_t * 30.0))
            return;

        Debug.Log("Got frame for cloud " + colorIndex);
        RenderTexture renderTexture = source.texture as RenderTexture;

        if (frameTexture == null)
        {
            frameTexture = new Texture2D(renderTexture.width, renderTexture.height);
        }
        if (frameTexture.width != renderTexture.width || frameTexture.height != renderTexture.height)
        {
            frameTexture.Resize(renderTexture.width, renderTexture.height);
        }
        RenderTexture.active = renderTexture;
        frameTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        frameTexture.Apply();
        RenderTexture.active = null;

        ((PointCloud)clouds[colorIndex++]).ColorFromImage(frameTexture, new Matrix4x4());
        CreateFrameObject(frameTexture);

    }


    private void OnPrepareCompleted(VideoPlayer videoPlayer)
    {
        Debug.Log("Video starts at " + videoStart.ToString("yyyy-MM-dd HH:mm:ss,fff") + " and is " + string.Format("{0:0.000}", videoPlayer.length) + "s long");
        //try
        //{
        //    //videoPlayer.frame = 300;
        //    videoPlayer.Play();
        //    var oldTex = RenderTexture.active;
        //    RenderTexture renderTexture = videoPlayer.texture as RenderTexture;
        //    Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height,
        //        TextureFormat.RGB24, false);
        //    RenderTexture.active = renderTexture;

        //    texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        //    texture.Apply();
        //    RenderTexture.active = null;
        //    byte[] bytes = texture.EncodeToPNG();
        //    File.WriteAllBytes("Assets/Resources/image.png", bytes);
        //    CreateFrameObject(texture);

        //    RenderTexture.active = oldTex;
        //}
        //catch(Exception e)
        //{
        //    Debug.LogException(e);
        //}
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

}
