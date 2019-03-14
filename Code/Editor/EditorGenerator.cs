using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using UnityEngine;
using UnityEditor;

namespace Beans.Unity.Editor.EditorGenerator
{
	public class EditorGenerator
	{
		private string path = "Assets";

		private MonoScript script;
		private CodeCompileUnit unitCode;

		public static bool IsValidMonoScript (MonoScript script)
		{
			if (script == null)
				return false;

			var scriptClass = script.GetClass ();
			return scriptClass.IsSubclassOf (typeof (MonoBehaviour)) || scriptClass.IsSubclassOf (typeof (ScriptableObject));
		}

		public void Create (MonoScript script)
		{
			this.script = script ?? throw new ArgumentNullException ("Script cannot be null.");

			var scriptClass = script.GetClass ();
			if (!IsValidMonoScript (script))
				throw new ArgumentException ("Script must derive from MonoBehaviour or ScriptableObject.");

			unitCode = new CodeCompileUnit ();

			var namespaceCode = CreateNamespaceCode (script);

			namespaceCode.Types.Add (CreateClassCode (script));
			unitCode.Namespaces.Add (namespaceCode);
		}

		private CodeNamespace CreateNamespaceCode (MonoScript script)
		{
			var namespaceCode = new CodeNamespace ();

			// Set name
			var name = script.GetClass ().Namespace;
			if (!string.IsNullOrEmpty (name))
				namespaceCode.Name = $"{name}Editor";

			namespaceCode.Imports.Add (new CodeNamespaceImport ("UnityEditor"));

			return namespaceCode;
		}

		private CodeTypeDeclaration CreateClassCode (MonoScript script)
		{
			var classCode = new CodeTypeDeclaration ($"{script.name}Editor");

			// Make class inherit from Editor
			classCode.BaseTypes.Add ("Editor");

			// Make unit a public class
			classCode.IsClass = true;
			classCode.TypeAttributes = TypeAttributes.Public;

			// Create all the code for the class
			CreateCustomEditorAttribute (classCode);
			CreateFields (classCode, script);
			CreateMethods (classCode);

			return classCode;
		}

		private void CreateCustomEditorAttribute (CodeTypeDeclaration classCode)
		{
			var customEditorAttributeCode = new CodeAttributeDeclaration (new CodeTypeReference (typeof (CustomEditor)));
			customEditorAttributeCode.Arguments.Add (new CodeAttributeArgument (new CodeTypeOfExpression (script.GetClass ())));

			classCode.CustomAttributes.Add (customEditorAttributeCode);
		}

		private void CreateFields (CodeTypeDeclaration classCode, MonoScript script)
		{
			var allFields = script.GetClass ().GetFields (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var serializedFields = GetSerializedFields (allFields);

			foreach (var field in serializedFields)
			{
				var newFieldCode = new CodeMemberField ();

				newFieldCode.Type = new CodeTypeReference (typeof (GUIContent));
				newFieldCode.Name = $"{field.Name}Content";

				classCode.Members.Add (newFieldCode);
			}

			IEnumerable<FieldInfo> GetSerializedFields (FieldInfo[] fields)
			{
				for (int i = 0; i < fields.Length; i++)
				{
					var field = fields[i];

					var hasSerializeAttribute = false;
					var hasNonSerializedAttribute = false;

					var attributes = field.GetCustomAttributes ();
					foreach (var a in attributes)
					{
						if (a is SerializeField)
							hasSerializeAttribute = true;
						if (a is NonSerializedAttribute)
							hasNonSerializedAttribute = true;
					}

					if ((!hasNonSerializedAttribute && field.IsPublic) || hasSerializeAttribute)
						yield return field;
				}

				yield break;
			}
		}

		private void CreateMethods (CodeTypeDeclaration classCode)
		{
			var onEnableCode = new CodeMemberMethod ()
			{
				Name = "OnEnable",
				Attributes = MemberAttributes.Private
			};

			var inspectorGUICode = new CodeMemberMethod ()
			{
				Name = "OnInspectorGUI",
				Attributes = MemberAttributes.Public | MemberAttributes.Override
			};

			classCode.Members.Add (onEnableCode);
			classCode.Members.Add (inspectorGUICode);
		}

		public MonoScript Save ()
		{
			path = EditorUtility.SaveFilePanelInProject ("Save script", $"{script.name}Editor", "cs", "", path);

			if (string.IsNullOrEmpty (path))
				return null;

			var oldScript = (MonoScript)AssetDatabase.LoadAssetAtPath (path, typeof (MonoScript));
			if (oldScript == null)
				AssetDatabase.DeleteAsset (path);

			var provider = CodeDomProvider.CreateProvider ("CSharp");
			var options = new CodeGeneratorOptions ()
			{
				BracingStyle = "C"
			};

			using (var writer = new StreamWriter (path))
				provider.GenerateCodeFromCompileUnit (unitCode, writer, options);

			return (MonoScript)AssetDatabase.LoadAssetAtPath (path, typeof (MonoScript));
		}
	}
}