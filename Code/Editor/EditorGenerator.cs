using System;
using System.Reflection;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using UnityEditor;

namespace Beans.Unity.Editor.EditorGenerator
{
	public class EditorGenerator
	{
		private MonoScript script;
		private CodeCompileUnit unitCode;

		private static string Path = "Assets";

		public EditorGenerator (MonoScript script)
		{
			this.script = script ?? throw new NullReferenceException ("Script cannot be null.");

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
				namespaceCode.Name = $"{name}Editor";

			// Add imports
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

			return classCode;
		}

		public MonoScript Generate ()
		{
			Path = EditorUtility.SaveFilePanelInProject ("Save script", $"{script.name}Editor", "cs", "", Path);

			if (string.IsNullOrEmpty (Path))
				return null;

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