using UnityEngine;
using System.Collections;
using System.Linq;

namespace Tutorials.ResearchMode
{
    /// <summary>
    /// Copies vector3d to unity mesh
    /// Inspired by: https://github.com/petergu684/HoloLens2-ResearchMode-Unity
    /// </summary>
    public class ElemRenderer : MonoBehaviour
    {
        public Mesh mesh;

        // TODO: Might wanna change this to avoid weird triangular artifacts
        public void UpdateMesh(ArrayList arrVertices, int nPointsToRender, int nPointsRendered, ArrayList pointColors)
        {
            int nPoints;

            if (arrVertices == null)
                nPoints = 0;
            else
                nPoints = System.Math.Min(nPointsToRender, arrVertices.Count - nPointsRendered);
            nPoints = System.Math.Min(nPoints, 65535);

            Vector3[] points = (Vector3[])arrVertices.GetRange(nPointsRendered, nPoints).ToArray(typeof(Vector3));
            int[] indices = new int[nPoints];
            Color[] colors = (Color[])pointColors.GetRange(nPointsRendered, nPoints).ToArray(typeof(Color));

            for (int i = 0; i < nPoints; i++)
            {
                indices[i] = i;
            }

            if (mesh != null)
                Destroy(mesh);

            mesh = new Mesh();
            mesh.vertices = points;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            GetComponent<MeshFilter>().mesh = mesh;
        }
        
        public void UpdateMesh(Vector3[] arrVertices, int nPointsToRender, int nPointsRendered, Color[] pointColors)
        {
            int nPoints;

            if (arrVertices == null)
                nPoints = 0;
            else
                nPoints = System.Math.Min(nPointsToRender, arrVertices.Length - nPointsRendered);
            nPoints = System.Math.Min(nPoints, 65535);

            Vector3[] points = arrVertices.Skip(nPointsRendered).Take(nPoints).ToArray();
            int[] indices = new int[nPoints];
            Color[] colors = pointColors.Skip(nPointsRendered).Take(nPoints).ToArray();

            for (int i = 0; i < nPoints; i++)
            {
                indices[i] = i;
            }

            if (mesh != null)
                Destroy(mesh);
            mesh = new Mesh();
            mesh.vertices = points;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            GetComponent<MeshFilter>().mesh = mesh;
        }
    }
}