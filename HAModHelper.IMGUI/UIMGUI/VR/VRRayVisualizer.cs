using UnityEngine;

namespace UImGui.VR
{
    public class VRRayVisualizer : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private int curveResolution = 20; 
        [SerializeField] private float curvature = 0.3f; 
        [SerializeField] private float curveHeight = 0.5f;

        private void Start()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            lineRenderer.positionCount = curveResolution;
            lineRenderer.useWorldSpace = true;
        }

        public void UpdateRay(Vector3 controllerPosition, Quaternion controllerRotation, Vector3 hitPoint)
        {
            Vector3 p0 = controllerPosition;
            Vector3 p3 = hitPoint;
            
            Vector3 controllerForward = controllerRotation * Vector3.forward;
            
            float distance = Vector3.Distance(p0, p3);
            
            Vector3 p1 = p0 + controllerForward * (distance * curvature);
            
            Vector3 toController = (p0 - p3).normalized;
            Vector3 up = Vector3.up;
            Vector3 p2 = p3 + toController * (distance * curvature) + up * (distance * curveHeight);
            
            DrawBezierCurve(p0, p1, p2, p3);
        }

        private void DrawBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            for (int i = 0; i < curveResolution; i++)
            {
                float t = i / (float)(curveResolution - 1);
                Vector3 point = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
                lineRenderer.SetPosition(i, point);
            }
        }

        private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 point = uuu * p0; // (1-t)^3 * P0
            point += 3 * uu * t * p1; // 3(1-t)^2 * t * P1
            point += 3 * u * tt * p2; // 3(1-t) * t^2 * P2
            point += ttt * p3; // t^3 * P3

            return point;
        }
    }
}