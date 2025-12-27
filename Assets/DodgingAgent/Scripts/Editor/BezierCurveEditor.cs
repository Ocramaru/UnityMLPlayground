using System;
using UnityEngine;
using UnityEditor;
using DodgyBall.Scripts.Utilities;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts.Editor
{
    public class BezierCurveEditor : EditorWindow
    {
        private BezierCurveLibrary library;
        
        // Generation Settings
        private int numSamples = 50;
        private int numCurvesToGenerate = 5;
        private float placementOffset = 0.2f;
        private int numControlPoints = 3;
        
        // Visualization Settings
        private bool showPoints = true;
        private bool showTangents = false;
        private float curveScale = 1f;

        private int tabIndex = 0;
        private string[] tabs = { "Generate", "Visualize" };
        private int selectedCurveIndex = 0;

        [MenuItem("Window/Custom/Bezier Curve Editor")]
        public static void ShowWindow()
        {
            GetWindow<BezierCurveEditor>("Bezier Curve Editor");
        }

        private void OnGUI()
        {
            GUILayout.Label("Bezier Curve Editor", EditorStyles.boldLabel);

            library = (BezierCurveLibrary)EditorGUILayout.ObjectField("Target Library", library, typeof(BezierCurveLibrary), false);

            EditorGUILayout.Space();

            tabIndex = GUILayout.Toolbar(tabIndex, tabs);

            EditorGUILayout.Space();

            if (tabIndex == 0)
            {
                DrawGenerateTab();
            }
            else
            {
                DrawVisualizeTab();
            }
        }

        private void DrawGenerateTab()
        {
            GUILayout.Label("Generation Settings", EditorStyles.boldLabel);

            numSamples = EditorGUILayout.IntField("Num Samples", numSamples);
            numCurvesToGenerate = EditorGUILayout.IntField("Num Curves", numCurvesToGenerate);
            placementOffset = EditorGUILayout.FloatField("Placement Offset", placementOffset);
            numControlPoints = EditorGUILayout.IntSlider("Control Points", numControlPoints, 3, 5);

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Curves"))
            {
                GenerateCurves();
            }
        }

        private void DrawVisualizeTab()
        {
            if (!library || library.curves == null || library.curves.Length == 0)
            {
                EditorGUILayout.HelpBox("No curves to visualize. Generate curves first.", MessageType.Info);
                return;
            }

            GUILayout.Label("Visualization", EditorStyles.boldLabel);
            
            showPoints = EditorGUILayout.Toggle("Show Points", showPoints);
            showTangents = EditorGUILayout.Toggle("Show Tangents", showTangents);
            curveScale = EditorGUILayout.FloatField("Scale", curveScale);
            selectedCurveIndex = EditorGUILayout.IntSlider("Curve Index", selectedCurveIndex, 0, library.curves.Length - 1);

            BezierCurve curve = library.curves[selectedCurveIndex];
            EditorGUILayout.LabelField($"Contact Point: {curve.contactPoint}");
            EditorGUILayout.LabelField($"Distance to Contact: {curve.distanceToContact:F3}");
            EditorGUILayout.LabelField($"Num Samples: {curve.sampledPoints?.Length ?? 0}");

            if (GUILayout.Button("Refresh Scene View"))
            {
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (tabIndex != 1 || !library || library.curves == null || library.curves.Length == 0)
                return;

            if (selectedCurveIndex < 0 || selectedCurveIndex >= library.curves.Length)
                return;

            BezierCurve curve = library.curves[selectedCurveIndex];

            if (curve.sampledPoints == null || curve.sampledPoints.Length == 0)
                return;
            
            Handles.color = Color.cyan;

            // Draw the curve itself
            for (int i = 0; i < curve.sampledPoints.Length - 1; i++)
            {
                Handles.DrawLine(curve.sampledPoints[i] * curveScale, curve.sampledPoints[i + 1] * curveScale, 1.5f);
            }

            // Then let's place some points
            if (showPoints)
            {
                Handles.color = Color.green; // start point
                Handles.SphereHandleCap(0, curve.sampledPoints[0] * curveScale, Quaternion.identity, 0.025f, EventType.Repaint);
                Handles.Label(curve.sampledPoints[0] * curveScale, "Start");

                Handles.color = Color.red; // end point
                Handles.SphereHandleCap(0, curve.sampledPoints[^1] * curveScale, Quaternion.identity, 0.025f, EventType.Repaint);
                Handles.Label(curve.sampledPoints[^1] * curveScale, "End");

                Handles.color = Color.yellow; // contact point
                Handles.SphereHandleCap(0, curve.contactPoint * curveScale, Quaternion.identity, 0.05f, EventType.Repaint);
                Handles.Label(curve.contactPoint * curveScale, "Contact");
            }

            // Draw tangents at intervals
            if (showTangents)
            {
                Handles.color = Color.magenta;
                int tangentInterval = Mathf.Max(1, curve.sampledPoints.Length / 10);
                for (int i = 0; i < curve.sampledTangents.Length; i += tangentInterval)
                {
                    Vector3 tangentEnd = curve.sampledPoints[i] + curve.sampledTangents[i] * 0.1f;
                    Handles.DrawLine(curve.sampledPoints[i] * curveScale, tangentEnd * curveScale, 1.5f);
                }
            }
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private BezierCurve GenerateCurve()
        {
            Vector3[] controlPoints = new Vector3[numControlPoints];

            controlPoints[0] = Vector3.zero;
            controlPoints[numControlPoints - 1] = new Vector3(1f, 0f, 0f);

            for (int i = 1; i < numControlPoints - 1; i++)
            {
                Vector3 basePosition = Vector3.Lerp(controlPoints[0], controlPoints[numControlPoints - 1], (float)i / (numControlPoints - 1));

                Vector3 randomOffset = Random.insideUnitSphere * placementOffset;
                randomOffset.z = Mathf.Abs(randomOffset.z);
                controlPoints[i] = basePosition + randomOffset;
            }

            float contactAt = Random.Range(0.35f, 0.65f);
            Vector3 contactPoint = Bezier.Evaluate(contactAt, controlPoints);

            // Arc Lengths | Needed for contactTimeRatio
            float totalArcLength = Bezier.ArcLength(0f, 1f, 1e-5f, 30, controlPoints);
            float arcLengthToContact = Bezier.ArcLength(0f, contactAt, 1e-5f, 30, controlPoints);
            float contactTimeRatio = arcLengthToContact / totalArcLength;

            // Debug.Log($"Gen Curve: contactAt={contactAt:F3} | totalArc={totalArcLength:F6} | arcToContact={arcLengthToContact:F6} | ratio={contactTimeRatio:F6}");

            float distanceToContact = (contactPoint - controlPoints[0]).magnitude;

            Vector3[] sampledPoints = Bezier.SamplePoints(numSamples, controlPoints);
            Vector3[] sampledTangents = Bezier.SampleTangents(numSamples, controlPoints);

            float[] cumulativeArcLengths = new float[sampledPoints.Length];
            cumulativeArcLengths[0] = 0f;
            for (int i = 1; i < sampledPoints.Length; i++)
            {
                float segmentLength = Vector3.Distance(sampledPoints[i - 1], sampledPoints[i]);
                cumulativeArcLengths[i] = cumulativeArcLengths[i - 1] + segmentLength;
            }

            return new BezierCurve
            {
                sampledPoints = sampledPoints,
                sampledTangents = sampledTangents,
                cumulativeArcLengths = cumulativeArcLengths,
                contactPoint = contactPoint,
                distanceToContact = distanceToContact,
                totalArcLength = totalArcLength,
                arcLengthToContact = arcLengthToContact,
                contactTimeRatio = contactTimeRatio
            };
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void GenerateCurves()
        {
            if (!library)
            {
                Debug.LogError("No library selected");
                return;
            }

            library.curves ??= Array.Empty<BezierCurve>(); // create new curve if null

            int originalCount = library.curves.Length;
            Array.Resize(ref library.curves, originalCount + numCurvesToGenerate);

            for (int i = 0; i < numCurvesToGenerate; i++)
            {
                library.curves[originalCount + i] = GenerateCurve();
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"Generated {numCurvesToGenerate} curves. Total: {library.curves.Length}");
        }
    }
}