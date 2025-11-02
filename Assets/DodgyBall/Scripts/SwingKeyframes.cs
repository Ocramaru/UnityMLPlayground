using System;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class SwingKeyframe
{
    public float angle;
    public Quaternion relativeStart;
    public Quaternion relativeEnd;
    public Vector3 localSwingAxis;
    public bool isUpwardSwing;
}

[Serializable]
public class SwingKeyframeSet
{
    public SwingKeyframe[] keyframes;
    public Vector3 planeNormal;
    
    // Binary serialization helpers
    public byte[] ToBytes()
    {
        using (var ms = new System.IO.MemoryStream())
        using (var writer = new System.IO.BinaryWriter(ms))
        {
            // Write plane normal
            writer.Write(planeNormal.x);
            writer.Write(planeNormal.y);
            writer.Write(planeNormal.z);
            
            // Write keyframe count
            writer.Write(keyframes.Length);
            
            // Write each keyframe
            foreach (var kf in keyframes)
            {
                writer.Write(kf.angle);
                
                // Start rotation
                writer.Write(kf.relativeStart.x);
                writer.Write(kf.relativeStart.y);
                writer.Write(kf.relativeStart.z);
                writer.Write(kf.relativeStart.w);
                
                // End rotation
                writer.Write(kf.relativeEnd.x);
                writer.Write(kf.relativeEnd.y);
                writer.Write(kf.relativeEnd.z);
                writer.Write(kf.relativeEnd.w);
                
                // Swing axis
                writer.Write(kf.localSwingAxis.x);
                writer.Write(kf.localSwingAxis.y);
                writer.Write(kf.localSwingAxis.z);
                writer.Write(kf.isUpwardSwing);
            }
            
            return ms.ToArray();
        }
    }
    
    public static SwingKeyframeSet FromBytes(byte[] data)
    {
        using (var ms = new System.IO.MemoryStream(data))
        using (var reader = new System.IO.BinaryReader(ms))
        {
            var set = new SwingKeyframeSet();
            
            // Read plane normal
            set.planeNormal = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            );
            
            // Read keyframes
            int count = reader.ReadInt32();
            set.keyframes = new SwingKeyframe[count];
            
            for (int i = 0; i < count; i++)
            {
                var kf = new SwingKeyframe();
                kf.angle = reader.ReadSingle();
                
                // Start rotation
                kf.relativeStart = new Quaternion(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
                
                // End rotation
                kf.relativeEnd = new Quaternion(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
                
                // Swing axis
                kf.localSwingAxis = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
                kf.isUpwardSwing = reader.ReadBoolean();
                
                set.keyframes[i] = kf;
            }
            
            return set;
        }
    }
    
    public static void Save(string path, SwingKeyframeSet set)
    {
        byte[] data = set.ToBytes();
        System.IO.File.WriteAllBytes(path, data);
    }
    
    public static SwingKeyframeSet Load(string path)
    {
        byte[] data = System.IO.File.ReadAllBytes(path);
        return FromBytes(data);
    }

    // --- Singleton Cache for global access ---
    private static SwingKeyframeSet _instance;
    public static SwingKeyframeSet Instance => _instance;
    public static bool IsLoaded => _instance != null;
    
    /// <summary>Loads a set from disk and stores it as the singleton instance.</summary>
    public static void LoadSingleton(string path)
    {
        _instance = Load(path);
    }
    
    /// <summary>Loads the singleton from the default path if no path is provided.</summary>
    public static void LoadSingleton()
    {
        const string defaultPath = "Assets/DodgyBall/data/swing_keyframes.bin";
        _instance = Load(defaultPath);
        Debug.Log($"SwingKeyframeSet loaded from default path: {defaultPath}");
    }
    
    /// <summary>Clears the cached singleton instance.</summary>
    public static void UnloadSingleton()
    {
        _instance = null;
    }
    
    /// <summary>Gets a random keyframe from the singleton instance.</summary>
    public static SwingKeyframe GetRandomFromSingleton()
    {
        if (_instance == null || _instance.keyframes == null || _instance.keyframes.Length == 0)
        {
            Debug.LogWarning("SwingKeyframeSet.Instance is not loaded or empty.");
            return default;
        }
        return _instance.GetRandomSwing();
    }
    
    /// <summary>Total number of keyframes in this set.</summary>
    public int Count => keyframes?.Length ?? 0;
    
    /// <summary>Bounds-checked access to a keyframe by index.</summary>
    public SwingKeyframe GetByIndex(int i)
    {
        if (keyframes == null || i < 0 || i >= keyframes.Length)
        {
            Debug.LogWarning($"Keyframe index {i} out of range.");
            return default;
        }
        return keyframes[i];
    }

    public SwingKeyframe GetRandomSwing()
    {
        if (keyframes == null || keyframes.Length == 0)
        {
            Debug.LogWarning("SwingKeyframeSet.GetRandomSwing: no keyframes available.");
            return default;
        }
        return keyframes[Random.Range(0, keyframes.Length)];
    }
}