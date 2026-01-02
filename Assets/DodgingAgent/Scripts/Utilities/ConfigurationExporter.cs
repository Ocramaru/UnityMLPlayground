using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DodgingAgent.Scripts.Agents;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DodgingAgent.Scripts.Utilities
{
    public static class ConfigurationExporter
    {
        public const string DefaultDirectory = "Assets/DodgingAgent/config/agents";

        [Serializable]
        public class AgentConfiguration
        {
            public string exportTime;
            public string agentType;
            public string sceneName;
            public List<Parameter> agentParameters = new List<Parameter>();
            public List<ComponentConfig> sensors = new List<ComponentConfig>();
            public List<Parameter> behaviorParameters = new List<Parameter>();
        }

        [Serializable]
        public class ComponentConfig
        {
            public string componentType;
            public List<Parameter> parameters = new List<Parameter>();
        }

        [Serializable]
        public class Parameter
        {
            public string key;
            public string value;
        }

        public static AgentConfiguration CollectConfiguration(Agent agent)
        {
            var config = CollectAgentConfig(agent);
            config.exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return config;
        }

        public static void Save(Agent agent, string path)
        {
            if (!agent)
            {
                Debug.LogError("No agent provided for export");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            var config = CollectConfiguration(agent);
            File.WriteAllText(path, JsonUtility.ToJson(config, true));
            Debug.Log($"Agent config exported: {path}");
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        public static AgentConfiguration Load(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"Config not found: {path}");
                return null;
            }
            return JsonUtility.FromJson<AgentConfiguration>(File.ReadAllText(path));
        }

        private static AgentConfiguration CollectAgentConfig(Agent agent)
        {
            var config = new AgentConfiguration
            {
                agentType = agent.GetType().Name,
                sceneName = agent.gameObject.scene.name,
                agentParameters = ExtractComponentParameters(agent),
                behaviorParameters = CollectBehaviorParameters(agent),
                sensors = CollectSensorConfigs(agent.gameObject)
            };

            return config;
        }

        private static List<Parameter> CollectBehaviorParameters(Agent agent)
        {
            var behaviorParams = agent.GetComponent<BehaviorParameters>();
            if (!behaviorParams) return new List<Parameter>();

            return ExtractComponentParameters(behaviorParams);
        }

        private static List<ComponentConfig> CollectSensorConfigs(GameObject agentObject)
        {
            var configs = new List<ComponentConfig>();

            foreach (var sensor in agentObject.GetComponents<SensorComponent>())
            {
                var sensorConfig = new ComponentConfig
                {
                    componentType = sensor.GetType().Name,
                    parameters = ExtractComponentParameters(sensor)
                };
                configs.Add(sensorConfig);
            }

            return configs;
        }

        private static List<Parameter> ExtractComponentParameters(Component component)
        {
            var parameters = new List<Parameter>();
            var type = component.GetType();

            // Unity built-in properties to ignore
            var ignoredProperties = new HashSet<string>
            {
                "useGUILayout", "didStart", "didAwake", "runInEditMode", "enabled",
                "isActiveAndEnabled", "tag", "name", "hideFlags", "gameObject", "transform"
            };

            // Get all public fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (ShouldSerializeField(field.FieldType) && !ignoredProperties.Contains(field.Name))
                {
                    var value = field.GetValue(component);
                    parameters.Add(new Parameter
                    {
                        key = field.Name,
                        value = SerializeValue(value)
                    });
                }
            }

            // Get all public properties with getters
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.CanRead && ShouldSerializeField(prop.PropertyType) && !ignoredProperties.Contains(prop.Name))
                {
                    try
                    {
                        var value = prop.GetValue(component);
                        parameters.Add(new Parameter
                        {
                            key = prop.Name,
                            value = SerializeValue(value)
                        });
                    }
                    catch
                    {
                        // Skip properties that throw exceptions when accessed
                    }
                }
            }

            return parameters;
        }

        private static bool ShouldSerializeField(Type fieldType)
        {
            // Serialize primitive types, strings, and arrays of primitives
            return fieldType.IsPrimitive
                   || fieldType == typeof(string)
                   || fieldType == typeof(decimal)
                   || (fieldType.IsArray && (fieldType.GetElementType()?.IsPrimitive ?? false))
                   || fieldType.IsEnum;
        }

        private static string SerializeValue(object value)
        {
            if (value == null) return "null";

            if (value is Array array)
            {
                return $"[{string.Join(",", array.Cast<object>())}]";
            }

            return value.ToString();
        }
    }

#if UNITY_EDITOR
    public static class ConfigurationExporterMenu
    {
        [MenuItem("DodgingAgent/Export Agent Configuration")]
        public static void ExportConfig()
        {
            // Get selected GameObject
            var selected = Selection.activeGameObject;
            if (!selected)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a GameObject with an Agent component in the hierarchy.", "OK");
                return;
            }

            // Check if it has an Agent component
            var agent = selected.GetComponent<Agent>();
            if (!agent)
            {
                EditorUtility.DisplayDialog("Invalid Selection", "Selected GameObject does not have an Agent component.", "OK");
                return;
            }

            // Create default directory if it doesn't exist
            if (!Directory.Exists(ConfigurationExporter.DefaultDirectory))
            {
                Directory.CreateDirectory(ConfigurationExporter.DefaultDirectory);
                AssetDatabase.Refresh();
            }

            // Show save dialog
            var defaultFileName = $"{agent.GetType().Name}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            var path = EditorUtility.SaveFilePanel(
                "Export Agent Configuration",
                ConfigurationExporter.DefaultDirectory,
                defaultFileName,
                "json"
            );

            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("Export cancelled");
                return;
            }

            ConfigurationExporter.Save(agent, path);
        }

        [MenuItem("DodgingAgent/Export Agent Configuration", true)]
        public static bool ValidateExportConfig()
        {
            var selected = Selection.activeGameObject;
            return selected && selected.GetComponent<Agent>();
        }
    }
#endif
}