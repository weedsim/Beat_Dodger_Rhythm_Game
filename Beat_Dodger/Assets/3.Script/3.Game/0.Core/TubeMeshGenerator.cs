using UnityEngine;

namespace BeatDodger.Game
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TubeMeshGenerator : MonoBehaviour
    {
        private Mesh _mesh;
        private MeshFilter _meshFilter;

        public void Generate(Vector3[] path, float radius, int segments = 12)
        {
            if (path == null || path.Length < 2) return;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "CurvedLongNoteTube" };
                _meshFilter = GetComponent<MeshFilter>();
                _meshFilter.mesh = _mesh;
            }

            int ringCount = path.Length;
            int vertexCount = ringCount * (segments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[(ringCount - 1) * segments * 6];

            for (int r = 0; r < ringCount; r++)
            {
                Vector3 forward;
                if (r < ringCount - 1) forward = (path[r + 1] - path[r]).normalized;
                else forward = (path[r] - path[r - 1]).normalized;

                Vector3 up = Vector3.up;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;

                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * radius;
                    float y = Mathf.Sin(angle) * radius;

                    int vIdx = r * (segments + 1) + s;
                    vertices[vIdx] = path[r] + (right * x) + (up * y) - transform.position; // Local space
                    uvs[vIdx] = new Vector2((float)s / segments, (float)r / (ringCount - 1));

                    if (r < ringCount - 1 && s < segments)
                    {
                        int tIdx = (r * segments + s) * 6;
                        int curr = r * (segments + 1) + s;
                        int next = curr + segments + 1;

                        triangles[tIdx] = curr;
                        triangles[tIdx + 1] = next;
                        triangles[tIdx + 2] = curr + 1;

                        triangles[tIdx + 3] = curr + 1;
                        triangles[tIdx + 4] = next;
                        triangles[tIdx + 5] = next + 1;
                    }
                }
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.uv = uvs;
            _mesh.RecalculateNormals();
        }
    }
}
