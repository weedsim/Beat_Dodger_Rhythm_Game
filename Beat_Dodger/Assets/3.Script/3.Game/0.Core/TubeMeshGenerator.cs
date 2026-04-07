using UnityEngine;

namespace BeatDodger.Game
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TubeMeshGenerator : MonoBehaviour
    {
        private Mesh _mesh;
        private MeshFilter _meshFilter;

        public void Generate(Vector3[] path, float baseRadius, AnimationCurve radiusCurve = null, int segments = 12)
        {
            if (path == null || path.Length < 2) return;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "CurvedLongNoteTube" };
                _meshFilter = GetComponent<MeshFilter>();
                _meshFilter.mesh = _mesh;
            }

            int ringCount = path.Length;
            // Vertices: Rings * (segments + 1) + 2 end cap centers
            int vertexCount = ringCount * (segments + 1) + 2; 
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            
            // Triangles: Body + 2 end caps
            int bodyTriCount = (ringCount - 1) * segments * 6;
            int capTriCount = segments * 3 * 2;
            int[] triangles = new int[bodyTriCount + capTriCount];

            for (int r = 0; r < ringCount; r++)
            {
                float tNormalized = (float)r / (ringCount - 1);
                float currentRadius = baseRadius;
                if (radiusCurve != null)
                {
                    currentRadius *= radiusCurve.Evaluate(tNormalized);
                }

                Vector3 forward;
                if (r == 0) forward = (path[1] - path[0]).normalized;
                else if (r == ringCount - 1) forward = (path[r] - path[r - 1]).normalized;
                else forward = (path[r + 1] - path[r - 1]).normalized;

                if (forward.sqrMagnitude < 0.001f) forward = transform.forward;

                Vector3 up = Vector3.up;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(Vector3.right, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;

                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * currentRadius;
                    float y = Mathf.Sin(angle) * currentRadius;

                    int vIdx = r * (segments + 1) + s;
                    vertices[vIdx] = path[r] + (right * x) + (up * y) - transform.position;
                    uvs[vIdx] = new Vector2((float)s / segments, tNormalized);

                    if (r < ringCount - 1 && s < segments)
                    {
                        int tIdx = (r * segments + s) * 6;
                        int curr = r * (segments + 1) + s;
                        int next = curr + segments + 1;

                        // Correcting winding order to Clockwise
                        triangles[tIdx] = curr;
                        triangles[tIdx + 1] = curr + 1;
                        triangles[tIdx + 2] = next;

                        triangles[tIdx + 3] = curr + 1;
                        triangles[tIdx + 4] = next + 1;
                        triangles[tIdx + 5] = next;
                    }
                }
            }

            // End Caps
            int startCapIdx = vertexCount - 2;
            int endCapIdx = vertexCount - 1;
            vertices[startCapIdx] = path[0] - transform.position;
            vertices[endCapIdx] = path[ringCount - 1] - transform.position;
            uvs[startCapIdx] = new Vector2(0.5f, 0f);
            uvs[endCapIdx] = new Vector2(0.5f, 1f);

            int triOffset = bodyTriCount;
            for (int s = 0; s < segments; s++)
            {
                // Start Cap (Looking from back: reverse)
                triangles[triOffset + s * 3] = startCapIdx;
                triangles[triOffset + s * 3 + 1] = s;
                triangles[triOffset + s * 3 + 2] = s + 1;

                // End Cap (Looking from front: normal)
                int endRingStart = (ringCount - 1) * (segments + 1);
                triangles[triOffset + capTriCount / 2 + s * 3] = endCapIdx;
                triangles[triOffset + capTriCount / 2 + s * 3 + 1] = endRingStart + s + 1;
                triangles[triOffset + capTriCount / 2 + s * 3 + 2] = endRingStart + s;
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.uv = uvs;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }
}
