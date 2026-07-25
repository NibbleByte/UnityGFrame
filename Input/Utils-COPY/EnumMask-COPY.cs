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

			var enumType = fieldInfo.FieldType;

			EditorGUI.BeginChangeCheck();
			// MaskField() ignores the enum values and just enumerates them in the order they are defined.
			// You can't have 1 << 2 and then jump directly to 1 << 8. This is why we use EnumFlagsField()
			//int value = EditorGUI.MaskField(position, label, property.enumValueFlag, property.enumNames);

			Enum value = EditorGUI.EnumFlagsField(position, label, (Enum)Enum.ToObject(enumType, property.enumValueFlag));

			if (EditorGUI.EndChangeCheck()) {
				property.enumValueFlag = (int)(object)value;
			}

			EditorGUI.EndProperty();
		}
	}
#endif

}
