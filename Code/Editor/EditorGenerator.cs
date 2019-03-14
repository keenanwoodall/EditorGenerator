using System;
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
		private MonoScript script;
		private CodeCompileUnit unitCode;

		private static string Path = "Assets";

		public static bool IsValidMonoScript (MonoScript script)
		{
			if (script == null)
				return false;

			var scriptClass = script.GetClass ();
			return scriptClass.IsSubclassOf (typeof (MonoBehaviour)) || scriptClass.IsSubclassOf (typeof (ScriptableObject));
		}

		public EditorGenerator (MonoScript script)
		{
			this.script = script ?? throw new ArgumentNullException ("Script cannot be null.");

			var scriptClass = script.GetClass ();
			if (!IsValidMonoScript (script))
				throw new ArgumentException ("Script must derive from MonoBehaviour or ScriptableObject.");

			unitCode = new CodeCompileUnit ();

			var namespaceCode = CreateNamespaceCode (script);
			var classCode = CreateClassCode (script);

			namespaceCode.Types.Add (classCode);
			unitCode.Namespaces.Add (namespaceCode);
		}

		private CodeNamespace CreateNamespaceCode (MonoScript script)
		{
			var namespaceCode = new CodeNamespace ();

			// Set name
			var name = script.GetClass ().Namespace;
			if (!string.IsNullOrEmpty (name))
			{
				namespaceCode.Name = $"{name}Editor";
				namespaceCode.Imports.Add (new CodeNamespaceImport (name));
			}

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

			CreateCustomEditorAttribute (classCode);
			CreateMethods (classCode);

			return classCode;
		}

		private void CreateCustomEditorAttribute (CodeTypeDeclaration classCode)
		{
			var customEditorAttributeCode = new CodeAttributeDeclaration (new CodeTypeReference (typeof (CustomEditor)));
			customEditorAttributeCode.Arguments.Add (new CodeAttributeArgument (new CodeTypeOfExpression (script.GetClass ())));

			classCode.CustomAttributes.Add (customEditorAttributeCode);
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

		public MonoScript Generate ()
		{
			Path = EditorUtility.SaveFilePanelInProject ("Save script", $"{script.name}Editor", "cs", "", Path);

			if (string.IsNullOrEmpty (Path))
				return null;

			var oldScript = (MonoScript)AssetDatabase.LoadAssetAtPath (Path, typeof (MonoScript));
			if (oldScript == null)
				AssetDatabase.DeleteAsset (Path);

			var provider = CodeDomProvider.CreateProvider ("CSharp");
			var options = new CodeGeneratorOptions ()
			{
				BracingStyle = "C"
			};

			using (var writer = new StreamWriter (Path))
				provider.GenerateCodeFromCompileUnit (unitCode, writer, options);

			return (MonoScript)AssetDatabase.LoadAssetAtPath (Path, typeof (MonoScript));
		}
	}
}