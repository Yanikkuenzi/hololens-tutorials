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

    private ArrayList clouds;

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

    public PointCloud GetLast()
    {
        return this.Get(clouds.Count - 1);
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

        return true;
    }

}
