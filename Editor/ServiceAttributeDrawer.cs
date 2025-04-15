using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GAOS.ServiceLocator.Editor
{
    [CustomPropertyDrawer(typeof(ServiceAttribute))]
    public class ServiceAttributeDrawer : PropertyDrawer
    {
        private const float LINE_HEIGHT = 20f;
        private const float SPACING = 2f;
        private static readonly Color WARNING_COLOR = new Color(1f, 0.92f, 0.016f, 1f);
        private static readonly Color ERROR_COLOR = new Color(1f, 0.3f, 0.3f, 1f);

        private bool _showAdvanced = false;
        private Type[] _availableInterfaces;
        private string[] _interfaceNames;
        private int _selectedInterfaceIndex;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = LINE_HEIGHT; // Basic height for the main property
            if (_showAdvanced)
            {
                height += (LINE_HEIGHT + SPACING) * 3; // Interface, Lifetime, Context
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var currentRect = new Rect(position.x, position.y, position.width, LINE_HEIGHT);

            // Service Name field
            var nameRect = new Rect(currentRect.x, currentRect.y, currentRect.width - 100, currentRect.height);
            var advancedButtonRect = new Rect(nameRect.xMax + 5, currentRect.y, 95, currentRect.height);

            var serviceName = property.FindPropertyRelative("Name");
            EditorGUI.PropertyField(nameRect, serviceName, new GUIContent("Service Name"));

            // Advanced settings toggle
            _showAdvanced = GUI.Toggle(advancedButtonRect, _showAdvanced, "Advanced", EditorStyles.miniButton);

            if (_showAdvanced)
            {
                // Interface selection
                currentRect.y += LINE_HEIGHT + SPACING;
                DrawInterfaceSelection(currentRect, property);

                // Lifetime selection
                currentRect.y += LINE_HEIGHT + SPACING;
                var lifetime = property.FindPropertyRelative("Lifetime");
                EditorGUI.PropertyField(currentRect, lifetime);

                // Context selection
                currentRect.y += LINE_HEIGHT + SPACING;
                var context = property.FindPropertyRelative("Context");
                EditorGUI.PropertyField(currentRect, context);
            }

            EditorGUI.EndProperty();
        }

        private void DrawInterfaceSelection(Rect position, SerializedProperty property)
        {
            var interfaceType = property.FindPropertyRelative("ServiceInterface");
            
            // Cache available interfaces if not already cached
            if (_availableInterfaces == null)
            {
                _availableInterfaces = GetImplementedInterfaces(fieldInfo.DeclaringType);
                _interfaceNames = _availableInterfaces.Select(t => t.Name).ToArray();
                
                // Find current selection
                var currentType = interfaceType.GetValue<Type>();
                _selectedInterfaceIndex = Array.IndexOf(_availableInterfaces, currentType);
                if (_selectedInterfaceIndex == -1) _selectedInterfaceIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            _selectedInterfaceIndex = EditorGUI.Popup(position, "Service Interface", _selectedInterfaceIndex, _interfaceNames);
            if (EditorGUI.EndChangeCheck() && _selectedInterfaceIndex >= 0)
            {
                interfaceType.SetValue(_availableInterfaces[_selectedInterfaceIndex]);
            }

            // Draw warning if no interface is selected
            if (_selectedInterfaceIndex < 0 || _availableInterfaces.Length == 0)
            {
                var warningRect = new Rect(position.x, position.y + LINE_HEIGHT, position.width, LINE_HEIGHT);
                var oldColor = GUI.color;
                GUI.color = WARNING_COLOR;
                EditorGUI.HelpBox(warningRect, "No interface selected. Service may not work correctly.", MessageType.Warning);
                GUI.color = oldColor;
            }
        }

        private Type[] GetImplementedInterfaces(Type type)
        {
            if (type == null) return Array.Empty<Type>();

            var interfaces = type.GetInterfaces();
            var list = new List<Type> { type }; // Include the type itself as an option
            list.AddRange(interfaces);
            return list.ToArray();
        }
    }

    // Helper extension methods
    internal static class SerializedPropertyExtensions
    {
        public static T GetValue<T>(this SerializedProperty property)
        {
            object obj = property.managedReferenceValue;
            if (obj == null) return default;
            return (T)obj;
        }

        public static void SetValue<T>(this SerializedProperty property, T value)
        {
            property.managedReferenceValue = value;
        }
    }
} 