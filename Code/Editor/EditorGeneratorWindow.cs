using UnityEngine;
using UnityEditor;

namespace Beans.Unity.Editor.EditorGenerator
{
	public class EditorGeneratorWindow : EditorWindow
	{
		private class Content
		{
			public const string InvalidScriptWarning = "Script must derive from MonoBehaviour or ScriptableObject.";

			public static readonly GUIContent Title = new GUIContent ("Editor Generator");
			public static readonly GUIContent Script = new GUIContent ("Script");
		}

		private class Styles
		{
			public static readonly Vector2 MinSize = new Vector2 (300, 200);

			public static readonly GUIStyle Title;
			public static readonly GUIStyle FilePath;

			static Styles ()
			{
				Title = new GUIStyle (EditorStyles.largeLabel)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold
				};

				FilePath = new GUIStyle (EditorStyles.label)
				{
					alignment = TextAnchor.MiddleRight
				};
			}
		}

		private MonoScript script;

		private EditorGenerator generator;

		[MenuItem ("Tools/Editor Generator")]
		[MenuItem ("Window/Editor Generator")]
		public static void GetWindow ()
		{
			var window = GetWindow<EditorGeneratorWindow> ();

			window.titleContent = Content.Title;
			window.minSize = Styles.MinSize;
		}

		private void OnEnable ()
		{
			generator = new EditorGenerator ();
		}

		private void OnGUI ()
		{
			EditorGUILayout.LabelField (Content.Title, Styles.Title);

			EditorGUILayout.Space ();

			script = (MonoScript)EditorGUILayout.ObjectField (Content.Script, script, typeof (MonoScript), false);

			var isValidScript = EditorGenerator.IsValidMonoScript (script);

			if (script!= null && !isValidScript)
				EditorGUILayout.HelpBox (Content.InvalidScriptWarning, MessageType.Error);

			using (new EditorGUI.DisabledGroupScope (!isValidScript))
			{
				if (GUILayout.Button ("Generate"))
				{
					generator.Create (script);
					EditorGUIUtility.PingObject (generator.Save ());

					Debug.Log ($"<b>Don't forget to remove GUI attributes from {script.name}.</b> If you don't, they'll be drawn by the custom editor <i>and</i> the attributes.");
				}
			}
		}

		private string OpenSelectFilePanel ()
		{
			return EditorUtility.OpenFilePanel ("Select script", "Assets", "cs");
		}
	}
}