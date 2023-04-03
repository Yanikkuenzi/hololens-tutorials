using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEngine.Events;
using System.Runtime.Remoting.Messaging;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.CodeDom;
#if WINDOWS_UWP
using Windows.Security.ExchangeActiveSyncProvisioning;
#endif

namespace Tutorials
{

    /// <summary>
    /// Static class that provides logic for handling files and the current state of the loaded animations in the editor through its field AnimationListInstance, which exists only once in this context and can be accessed by any other class in this namespace.
    /// </summary>
    public static class FileHandler
    {
        private static string RECORDINGS_DIRECTORY = "Recordings";
        private static string ANIMATIONFILE_PREFIX = "HandAnimation";
        private static string DATAFILE_NAME = "datafile.xml";


        /// <summary>
        /// Event that will called if the upload fails, for example because there is no corresponding blob file stored locally (yet)
        /// </summary>
        public static Action OnUploadToCloudFailed = () => { };

        private static AnimationList animationList;

        /// <summary>
        /// The publicly available instance of the AnimationList that represents the loaded animations in the editor. 
        /// (Singleton-like pattern, i.e. there is only one AnimationList per runtime environment.
        /// </summary>
        public static AnimationList AnimationListInstance
        {
            get
            {
                if (animationList == null)
                {
                    try
                    {
                        LoadAnimationList();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + "AnimationList could not be loaded.");
                        return null;
                    }
                }

                return animationList;
            }

            set => animationList = value;
        }

        /// <summary>
        /// Creates the directory for the recorded data in the persistent data path, if it doesn't already exist.
        /// </summary>
        public static void CreateDirectory()
        {
            string path = Path.Combine(Application.persistentDataPath, RECORDINGS_DIRECTORY);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Returns a new file name for a blob file. When called on the Hololens (WINDOWS_UWP), the name includes the device Id and the time at which the method was called, which makes the name unique.
        /// The name also contains the file type suffix ".bin".
        /// </summary>
        /// <returns>The new file name</returns>
        public static string GetBlobFileName()
        {
#if WINDOWS_UWP
            var info = new EasClientDeviceInformation();
            return String.Format("{0}.{1}.{2}.{3}", ANIMATIONFILE_PREFIX, info.Id, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), InputAnimationSerializationUtils.Extension);
#elif UNITY_EDITOR
            return String.Format("{0}.{1}.{2}.{3}", ANIMATIONFILE_PREFIX, "unity_editor", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), InputAnimationSerializationUtils.Extension);
#else
            return String.Format("{0}.{1}.{2}.{3}", ANIMATIONFILE_PREFIX, "unknown_platform", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), InputAnimationSerializationUtils.Extension);
#endif

        }

        /// <summary>
        /// Returns the file path of a file in the recording directory when its name is passed as a parameter
        /// </summary>
        /// <param name="fileName">Name of the file</param>
        /// <returns>The complete file system path to the file</returns>
        public static string GetFilePath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, RECORDINGS_DIRECTORY, fileName);
        }

        /// <summary>
        /// Checks if blob file exists locally.
        /// </summary>
        /// <param name="blobFileName">Name of the blob file.</param>
        /// <returns>Result whether or not the blob file exists on the device (in the recordings directory)</returns>
        public static bool CheckIfBlobFileExistsLocally(string blobFileName)
        {
            if (blobFileName == null) return false;
            return File.Exists(Path.Combine(Application.persistentDataPath, RECORDINGS_DIRECTORY, blobFileName));
        }

        /// <summary>
        /// Loads the animation from a local blob file if it exists. 
        /// </summary>
        /// <param name="blobFileName">Name of the BLOB file.</param>
        /// <returns>If file was successfully retrieved, the animation is returned. Otherwise, null is returned</returns>
        public static InputAnimation LoadAnimationFromLocalBlobFile(string blobFileName)
        {
            if (!CheckIfBlobFileExistsLocally(blobFileName)) return null;

            if (blobFileName != null && blobFileName.Length > 0)
            {
                string combinedPath = Path.Combine(Application.persistentDataPath, RECORDINGS_DIRECTORY, blobFileName);

                try
                {
                    using (FileStream fs = new FileStream(combinedPath, FileMode.Open))
                    {
                        InputAnimation inputAnimation = new InputAnimation();
                        inputAnimation = InputAnimation.FromStream(fs);

                        return inputAnimation;
                    }
                }
                catch (IOException ex)
                {
                    Debug.LogError(ex.Message);
                }
            }
            return null;
        }

        /// <summary>
        /// Stores the blob file locally, i.e. in the recordings directory on the device
        /// Note: This method may take a substantial amound of time for longer animations. It runs asynchronously, which means the program will not be interrupted. 
        /// </summary>
        /// <param name="inputAnimation">The input animation to be saved</param>
        /// <param name="blobFileName">Name of the blob file.</param>
        public static async void StoreBlobFileLocally(InputAnimation inputAnimation, string blobFileName)
        {
            if (inputAnimation == null)
            {
                Debug.Log("InputAnimation is null");
                return;
            }

            if (CheckIfBlobFileExistsLocally(blobFileName)) return;

            try
            {
                byte[] blobFileBinary = await inputAnimation.ToBinary();

                File.WriteAllBytes(GetFilePath(blobFileName), blobFileBinary);

                Debug.Log($"Recorded input animation exported to {GetFilePath(blobFileName)}");

                AnimationListInstance.AnimationStoredLocally.Invoke();
            }
            catch (IOException ex)
            {
                Debug.LogWarning(ex.Message);
            }
        }



        


        /// <summary>
        /// Saves the animation list instance locally (in the persistant data path in the datafile.xml)
        /// Note: This is done if no animation list is found (i.e. datafile.xml doesn't exist), or on destroy, or any time when the list is changed (i.e. animations are added, overwritten or removed)
        /// </summary>
        /// <returns></returns>
        public static bool SaveAnimationList()
        {
            if (AnimationListInstance == null)
            {
                Debug.Log("animationList is null");
                return false;
            }

            AnimationListInstance.CopyLinkedListToArray();

            string path = Path.Combine(Application.persistentDataPath, RECORDINGS_DIRECTORY, DATAFILE_NAME);

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AnimationList));
                TextWriter writer = new StreamWriter(File.Create(path));
                serializer.Serialize(writer, AnimationListInstance);
                writer.Close();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Loads the animation list instance at launch. This is called when the animation instance is called but the property animationList is still null, which happens only at the time when the program is launched.
        /// </summary>
        public static void LoadAnimationList()
        {
            string path = Path.Combine(Application.persistentDataPath, RECORDINGS_DIRECTORY, DATAFILE_NAME);

            // if datafile.xml doesn't exist yet, a new (empty) animation list will be instatiated and save to the recordings directory
            if (!File.Exists(path))
            {
                if (animationList == null)
                {
                    AnimationListInstance = new AnimationList();
                }
                SaveAnimationList();

                AnimationListInstance.CurrentAnimationChanged.Invoke();

                return;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(AnimationList));
            FileStream fs = new FileStream(path, FileMode.Open);
            AnimationListInstance = (AnimationList)serializer.Deserialize(fs);
            fs.Close();

            AnimationListInstance.CopyArrayToLinkedList();

            AnimationListInstance.CurrentAnimationChanged.Invoke();
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static void LoadPointsFromPLY(string path, out ArrayList points, out ArrayList colors)
        {
            points = null;
            colors = null;

            // After this prefix, the number of vertices is specified
            const string length_prefix = "element vertex ";
            int idx = length_prefix.Length;

            // Points to the current position in the points and colors arrays that are being filled
            int curr = 0;
            try
            {
                // Is true iff the line containing 'end_header' has been consumed
                bool read_header = false;
                foreach(string line in System.IO.File.ReadLines(path))
                {
                    // Actually read points into the vector
                    if (read_header)
                    {
                        // There are more vertices in the file than specified in the header
                        if(curr >= points.Capacity)
                        {
                            Debug.Log("There are more vertices than specified in the header");
                        }
                        try
                        {
                            (Vector3? coordinates, Color color) = ParseLine(line.Trim());
                            if (coordinates == null)
                            {
                                Debug.Log("Skipped malformed line: " + line);
                                continue;
                            }
                            points.Add(coordinates);
                            colors.Add(color);
                            curr++;
                        }
                        catch (FormatException)
                        {
                            Debug.Log("All point coordinates must be floats!");
                        }
                    }
                    else
                    {
                        // Header ended
                        if (line.Trim().Equals("end_header"))
                        {
                            read_header = true;
                            continue;
                        }
                        // Line contains number of vertices
                        if (line.Length > idx && line.Substring(0, idx).Equals(length_prefix))
                        {
                            try
                            {
                                int n = Int32.Parse(line.Trim().Substring(idx));
                                points = new ArrayList(n);
                                colors = new ArrayList(n);
                            }
                            catch (FormatException)
                            {
                                Debug.Log("Number of vertices must be an integer");
                            }
                        }
                    }
                }
            }
            catch (IOException ex) // Could not open file, probably does not exist or don't have permission
            {
                Debug.LogError(ex.Message);
            }
        }

        private static (Vector3?, Color) ParseLine(string line)
        {
            Color default_color = Color.green;
            string[] elements = line.Split(' ');
            // Cannot parse malformed line
            if (elements.Length < 3) return (null, default_color);
            // x,y and z coordinates value of the point are the first three elements, seperated by space
            // the next three space-seperated elements are the r,g and b value
            float x = float.Parse(elements[0]);
            float y = float.Parse(elements[1]);
            float z = float.Parse(elements[2]);

            Color color = default_color;
            // use default color if RGB was not specified
            if (elements.Length >= 6)
            {
                float r = float.Parse(elements[3]) / 255f;
                float g = float.Parse(elements[4]) / 255f;
                float b = float.Parse(elements[5]) / 255f; 
                color = new Color(r, g, b);
            }



            return (new Vector3(x, y, z), color);
        }


    }


}
