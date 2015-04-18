/*
 * Copyright (c) 2015 Thomas Hourdel
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 *    1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 
 *    2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 
 *    3. This notice may not be removed or altered from any source
 *    distribution.
 */

using UnityEngine;
using UnityEditor;
using Moments;

namespace MomentsEditor
{
	[CustomEditor(typeof(Recorder))]
	public sealed class RecorderEditor : Editor
	{
		SerializedProperty m_AutoAspect;
		SerializedProperty m_Width;
		SerializedProperty m_Height;
		SerializedProperty m_FramePerSecond;
		SerializedProperty m_Repeat;
		SerializedProperty m_Quality;
		SerializedProperty m_BufferSize;
		SerializedProperty m_WorkerPriority;

		void OnEnable()
		{
			m_AutoAspect = serializedObject.FindProperty("m_AutoAspect");
			m_Width = serializedObject.FindProperty("m_Width");
			m_Height = serializedObject.FindProperty("m_Height");
			m_FramePerSecond = serializedObject.FindProperty("m_FramePerSecond");
			m_Repeat = serializedObject.FindProperty("m_Repeat");
			m_Quality = serializedObject.FindProperty("m_Quality");
			m_BufferSize = serializedObject.FindProperty("m_BufferSize");
			m_WorkerPriority = serializedObject.FindProperty("WorkerPriority");
		}

		public override void OnInspectorGUI()
		{
			Recorder recorder = (Recorder)target;

			EditorGUILayout.HelpBox("This inspector is only used to tweak default values for the component. To change values at runtime, use the Setup() method.", MessageType.Info);

			// Don't let the user tweak settings while playing as it may break everything
			if (Application.isEditor && Application.isPlaying)
				GUI.enabled = false;

			serializedObject.Update();

			// Hooray for propertie drawers !
			EditorGUILayout.PropertyField(m_AutoAspect, new GUIContent("Automatic Height", "Automatically compute height from the current aspect ratio."));
			EditorGUILayout.PropertyField(m_Width, new GUIContent("Width", "Output gif width in pixels."));

			if (!m_AutoAspect.boolValue)
				EditorGUILayout.PropertyField(m_Height, new GUIContent("Height", "Output gif height in pixels."));
			else
				EditorGUILayout.LabelField(new GUIContent("Height", "Output gif height in pixels."), new GUIContent(m_Height.intValue.ToString()));

			EditorGUILayout.PropertyField(m_WorkerPriority, new GUIContent("Worker Thread Priority", "Thread priority to use when processing frames to a gif file."));
			EditorGUILayout.PropertyField(m_Quality, new GUIContent("Compression Quality", "Lower values mean better quality but slightly longer processing time. 15 is generally a good middleground value."));
			EditorGUILayout.PropertyField(m_Repeat, new GUIContent("Repeat", "-1 to disable, 0 to loop indefinitely, >0 to loop a set number of time."));
			EditorGUILayout.PropertyField(m_FramePerSecond, new GUIContent("Frames Per Second", "The number of frames per second the gif will run at."));
			EditorGUILayout.PropertyField(m_BufferSize, new GUIContent("Record Time", "The amount of time (in seconds) to record to memory."));

			serializedObject.ApplyModifiedProperties();

			GUI.enabled = true;

			recorder.ComputeHeight();
			EditorGUILayout.LabelField("Estimated VRam Usage", recorder.EstimatedMemoryUse.ToString("F3") + " MB");
		}
	}
}
