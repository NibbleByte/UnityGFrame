using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DevLocker.GFrame.Utils
{
	/// <summary>
	/// Display enum as a bit mask drop-down menu in the editor.
	/// HACK: THIS IS COPY-PASTE FROM THE DevLocker.Utils.
	/// </summary>
	internal class EnumMaskAttribute : PropertyAttribute
	{
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(EnumMaskAttribute))]
	internal class EnumMaskPropertyDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (property.propertyType != SerializedPropertyType.Enum) {
				EditorGUI.PropertyField(position, property, label, true);
				return;
			}

			EditorGUI.BeginProperty(position, label, property);

			EditorGUI.BeginChangeCheck();
			int value = EditorGUI.MaskField(position, label, property.enumValueFlag, property.enumNames);
			if (EditorGUI.EndChangeCheck()) {
				property.intValue = value;
			}

			EditorGUI.EndProperty();
		}
	}
#endif

}
